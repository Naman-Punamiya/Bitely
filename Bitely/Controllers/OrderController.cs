using System;
using System.Collections.Generic;
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
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        // Customer's order list
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var orders = await _context.Orders
                .Include(o => o.FoodStall)
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // Checkout review page
        public async Task<IActionResult> Checkout()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.MenuItem)
                        .ThenInclude(m => m!.FoodStall)
                .FirstOrDefaultAsync(c => c.CustomerId == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty. Please add items before checking out.";
                return RedirectToAction("Index", "Cart");
            }

            var customer = await _context.Users.FindAsync(userId);
            ViewBag.CustomerName = customer?.Name ?? customer?.UserName ?? "Customer";
            ViewBag.CustomerEmail = customer?.Email ?? "";
            ViewBag.CustomerPhone = customer?.PhoneNumber ?? "";
            ViewBag.RazorpayKeyId = Environment.GetEnvironmentVariable("RAZORPAY_KEY_ID") ?? "rzp_test_YourKeyIdHere";

            return View(cart);
        }

        // Buy full cart and notify all related stall owners
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(string? customerNote, string paymentMethod = "Cash", string? paymentId = null, string paymentStatus = "Pending")
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var customer = await _context.Users.FindAsync(userId);
            var customerName = customer?.Name ?? customer?.UserName ?? "A customer";

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.MenuItem)
                        .ThenInclude(m => m!.FoodStall)
                .FirstOrDefaultAsync(c => c.CustomerId == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty. Please add items before checking out.";
                return RedirectToAction("Index", "Cart");
            }

            // Normalise payment info
            var isOnline = string.Equals(paymentMethod, "Online", StringComparison.OrdinalIgnoreCase);
            var actualPaymentMethod = isOnline ? "Online" : "Cash";
            var actualPaymentStatus = isOnline ? (string.IsNullOrEmpty(paymentStatus) ? "Paid" : paymentStatus) : "Pending";

            // Group items by Food Stall
            var stallGroups = cart.CartItems
                .Where(ci => ci.MenuItem != null && ci.MenuItem.FoodStall != null)
                .GroupBy(ci => ci.MenuItem!.FoodStall!);

            var createdOrders = new List<Order>();

            foreach (var group in stallGroups)
            {
                var stall = group.Key;
                var items = group.ToList();

                var orderNumber = $"ORD-{DateTime.UtcNow:yyMMddHHmmss}-{stall.Id}-{new Random().Next(100, 999)}";
                var totalAmount = items.Sum(i => i.UnitPrice * i.Quantity);

                var order = new Order
                {
                    OrderNumber = orderNumber,
                    CustomerId = userId,
                    FoodStallId = stall.Id,
                    TotalAmount = totalAmount,
                    Status = "Pending",
                    PaymentMethod = actualPaymentMethod,
                    PaymentId = paymentId,
                    PaymentStatus = actualPaymentStatus,
                    CustomerNote = customerNote,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var item in items)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        MenuItemId = item.MenuItemId,
                        ItemName = item.MenuItem?.Name ?? "Item",
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity,
                        Subtotal = item.UnitPrice * item.Quantity
                    });
                }

                _context.Orders.Add(order);
                createdOrders.Add(order);

                // Notify the Food Stall Owner
                if (!string.IsNullOrEmpty(stall.OwnerId))
                {
                    var itemSummary = string.Join(", ", items.Select(i => $"{i.Quantity}x {i.MenuItem?.Name}"));
                    var paymentLabel = actualPaymentMethod == "Online" ? $"Online Paid (ID: {paymentId})" : "Cash on Pickup";
                    var notification = new Notification
                    {
                        UserId = stall.OwnerId,
                        Title = $"New Order #{orderNumber} Received! ({actualPaymentMethod})",
                        Message = $"{customerName} placed an order for '{stall.Name}': {itemSummary}. Total: ₹{totalAmount:F2} [{paymentLabel}].",
                        FoodStallId = stall.Id,
                        Order = order,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);
                }
            }

            // Clear the cart
            _context.CartItems.RemoveRange(cart.CartItems);
            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var successMsg = actualPaymentMethod == "Online"
                ? $"Payment successful! Your order has been placed for {createdOrders.Count} stall(s). Razorpay Payment ID: {paymentId}."
                : $"Thank you! Your order has been placed for {createdOrders.Count} stall(s). Please pay cash at the counter upon pickup.";

            TempData["SuccessMessage"] = successMsg;
            return RedirectToAction(nameof(Index));
        }

        // Order details
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var order = await _context.Orders
                .Include(o => o.FoodStall)
                    .ThenInclude(f => f!.Owner)
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // Allowed only for the customer or the stall owner
            var isCustomer = order.CustomerId == userId;
            var isStallOwner = order.FoodStall != null && order.FoodStall.OwnerId == userId;

            if (!isCustomer && !isStallOwner)
            {
                return Forbid();
            }

            ViewBag.IsStallOwner = isStallOwner;
            return View(order);
        }

        // Stall Owner: View incoming orders for owned stalls
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> StallOrders(int? foodStallId = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var stalls = await _context.FoodStalls
                .Where(s => s.OwnerId == userId)
                .ToListAsync();

            ViewBag.Stalls = stalls;
            ViewBag.SelectedStallId = foodStallId;

            var ordersQuery = _context.Orders
                .Include(o => o.FoodStall)
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .Where(o => o.FoodStall!.OwnerId == userId);

            if (foodStallId.HasValue && foodStallId.Value > 0)
            {
                ordersQuery = ordersQuery.Where(o => o.FoodStallId == foodStallId.Value);
            }

            var orders = await ordersQuery
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // Stall Owner: Update order status (e.g. Pending -> Preparing -> Ready -> Completed)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> UpdateStatus(int id, string? newStatus = null, [FromForm(Name = "status")] string? status = null, string? returnUrl = null)
        {
            var targetStatus = !string.IsNullOrWhiteSpace(newStatus) ? newStatus : status;

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var order = await _context.Orders
                .Include(o => o.FoodStall)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            if (order.FoodStall == null || order.FoodStall.OwnerId != userId)
            {
                return Forbid();
            }

            var allowedStatuses = new[] { "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
            if (string.IsNullOrWhiteSpace(targetStatus) || !allowedStatuses.Contains(targetStatus))
            {
                TempData["ErrorMessage"] = "Invalid status update.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(StallOrders), new { foodStallId = order.FoodStallId });
            }

            order.Status = targetStatus;

            // Notify the customer about status change
            if (!string.IsNullOrEmpty(order.CustomerId))
            {
                var stallName = order.FoodStall?.Name ?? "Food Stall";
                var notification = new Notification
                {
                    UserId = order.CustomerId,
                    Title = $"Order #{order.OrderNumber} Update: {targetStatus}",
                    Message = $"Your order from '{stallName}' is now '{targetStatus}'.",
                    FoodStallId = order.FoodStallId,
                    OrderId = order.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Order #{order.OrderNumber} status updated to '{targetStatus}'.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(StallOrders), new { foodStallId = order.FoodStallId });
        }
    }
}
