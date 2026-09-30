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

            return View(cart);
        }

        // Buy full cart and notify all related stall owners
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(string? customerNote)
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
                    var notification = new Notification
                    {
                        UserId = stall.OwnerId,
                        Title = $"New Order #{orderNumber} Received!",
                        Message = $"{customerName} placed an order for '{stall.Name}': {itemSummary}. Total: ${totalAmount:F2}.",
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

            TempData["SuccessMessage"] = $"Thank you! Your order has been placed successfully for {createdOrders.Count} stall(s). The stall owners have been notified.";
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
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
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
            if (!allowedStatuses.Contains(newStatus))
            {
                TempData["ErrorMessage"] = "Invalid status update.";
                return RedirectToAction(nameof(StallOrders), new { foodStallId = order.FoodStallId });
            }

            order.Status = newStatus;

            // Notify the customer about status change
            if (!string.IsNullOrEmpty(order.CustomerId))
            {
                var notification = new Notification
                {
                    UserId = order.CustomerId,
                    Title = $"Order #{order.OrderNumber} Update: {newStatus}",
                    Message = $"Your order from '{order.FoodStall.Name}' is now '{newStatus}'.",
                    FoodStallId = order.FoodStallId,
                    OrderId = order.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Order #{order.OrderNumber} status updated to '{newStatus}'.";
            return RedirectToAction(nameof(StallOrders), new { foodStallId = order.FoodStallId });
        }
    }
}
