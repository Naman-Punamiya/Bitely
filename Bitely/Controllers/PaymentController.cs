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
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }

        // GET: Payment/Checkout?orderId=5
        public async Task<IActionResult> Checkout(int orderId)
        {
            int currentUserId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.FoodStall)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.ConsumerId != currentUserId)
            {
                return NotFound();
            }

            if (order.Payment != null)
            {
                // Payment already created
                return RedirectToAction(nameof(Success), new { id = order.Payment.Id });
            }

            return View(order);
        }

        // POST: Payment/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int orderId, string method)
        {
            int currentUserId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.ConsumerId != currentUserId)
            {
                return NotFound();
            }

            if (order.Payment != null)
            {
                return RedirectToAction(nameof(Success), new { id = order.Payment.Id });
            }

            var payment = new Payment
            {
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Method = string.Equals(method, "Online", System.StringComparison.OrdinalIgnoreCase) ? "Online" : "Offline",
                Status = "Completed" // Placeholder completion for both Online and Offline for now
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Success), new { id = payment.Id });
        }

        // GET: Payment/Success/5
        public async Task<IActionResult> Success(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Order)
                .ThenInclude(o => o!.FoodStall)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }
    }
}
