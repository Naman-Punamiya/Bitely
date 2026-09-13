using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bitely.Data;
using Bitely.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bitely.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }

        // GET: Dashboard/Index
        public async Task<IActionResult> Index()
        {
            int currentUserId = GetCurrentUserId();
            var isOwner = User.IsInRole(Roles.FoodStallOwner);

            if (isOwner)
            {
                var myStalls = await _context.FoodStalls
                    .Where(f => f.OwnerId == currentUserId)
                    .ToListAsync();

                var stallIds = myStalls.Select(f => f.Id).ToList();

                var orders = await _context.Orders
                    .Include(o => o.Consumer)
                    .Include(o => o.FoodStall)
                    .Include(o => o.Payment)
                    .Where(o => stallIds.Contains(o.FoodStallId))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                var queueEntries = await _context.QueueEntries
                    .Include(q => q.Order)
                    .Include(q => q.FoodStall)
                    .Where(q => stallIds.Contains(q.FoodStallId))
                    .OrderBy(q => q.QueueNumber)
                    .ToListAsync();

                ViewBag.MyStalls = myStalls;
                ViewBag.QueueEntries = queueEntries;

                return View("OwnerDashboard", orders);
            }
            else
            {
                var recentOrders = await _context.Orders
                    .Include(o => o.FoodStall)
                    .Include(o => o.Payment)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                    .Where(o => o.ConsumerId == currentUserId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                var activeOrder = recentOrders.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled") ?? recentOrders.FirstOrDefault();

                QueueEntry? activeQueue = null;
                if (activeOrder != null)
                {
                    activeQueue = await _context.QueueEntries.FirstOrDefaultAsync(q => q.OrderId == activeOrder.Id);
                }

                ViewBag.ActiveOrder = activeOrder;
                ViewBag.ActiveQueue = activeQueue;

                return View("ConsumerDashboard", recentOrders);
            }
        }
    }
}
