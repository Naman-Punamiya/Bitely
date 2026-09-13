using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bitely.Data;
using Bitely.Hubs;
using Bitely.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Bitely.Controllers
{
    [Authorize]
    public class QueueController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<QueueHub> _hubContext;

        public QueueController(ApplicationDbContext context, IHubContext<QueueHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }

        // GET: Queue/Status?orderId=5
        public async Task<IActionResult> Status(int orderId)
        {
            var queueEntry = await _context.QueueEntries
                .Include(q => q.Order)
                .ThenInclude(o => o!.FoodStall)
                .FirstOrDefaultAsync(q => q.OrderId == orderId);

            if (queueEntry == null)
            {
                return NotFound();
            }

            int currentUserId = GetCurrentUserId();
            if (queueEntry.Order != null && queueEntry.Order.ConsumerId != currentUserId && User.IsInRole(Roles.Consumer))
            {
                return Forbid();
            }

            return View(queueEntry);
        }

        // GET: Queue/StallQueue?foodStallId=5
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> StallQueue(int foodStallId)
        {
            var foodStall = await _context.FoodStalls.FirstOrDefaultAsync(f => f.Id == foodStallId);
            if (foodStall == null || foodStall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            var entries = await _context.QueueEntries
                .Include(q => q.Order)
                .ThenInclude(o => o!.Consumer)
                .Where(q => q.FoodStallId == foodStallId)
                .OrderBy(q => q.QueueNumber)
                .ToListAsync();

            ViewBag.FoodStall = foodStall;
            return View(entries);
        }

        // POST: Queue/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> UpdateStatus(int orderId, string status)
        {
            var order = await _context.Orders
                .Include(o => o.FoodStall)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.FoodStall == null || order.FoodStall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            var validStatuses = new[] { "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
            if (validStatuses.Contains(status))
            {
                order.Status = status;

                var queueEntry = await _context.QueueEntries.FirstOrDefaultAsync(q => q.OrderId == orderId);
                if (queueEntry != null)
                {
                    queueEntry.Status = status;
                }

                await _context.SaveChangesAsync();

                // Send SignalR live status update to consumer listening on order-{orderId}
                await _hubContext.Clients.Group($"order-{orderId}").SendAsync("ReceiveStatusUpdate", orderId.ToString(), status);
                await _hubContext.Clients.Group($"stall-{order.FoodStallId}").SendAsync("ReceiveQueueUpdate", order.FoodStallId.ToString());
            }

            return RedirectToAction(nameof(StallQueue), new { foodStallId = order.FoodStallId });
        }
    }
}
