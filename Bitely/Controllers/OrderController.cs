using System;
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
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<QueueHub> _hubContext;

        public OrderController(ApplicationDbContext context, IHubContext<QueueHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }

        // GET: Order/Checkout
        public async Task<IActionResult> Checkout()
        {
            int consumerId = GetCurrentUserId();
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.MenuItem!)
                .ThenInclude(m => m.FoodStall)
                .FirstOrDefaultAsync(c => c.ConsumerId == consumerId);

            if (cart == null || !cart.CartItems.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            return View(cart);
        }

        // POST: Order/PlaceOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder()
        {
            int consumerId = GetCurrentUserId();

            // 1. Get the cart.
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.MenuItem)
                .FirstOrDefaultAsync(c => c.ConsumerId == consumerId);

            if (cart == null || !cart.CartItems.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            // 2. Get the menu item prices from database & 3. Calculate total.
            decimal totalAmount = 0;
            int foodStallId = 0;

            var menuItemIds = cart.CartItems.Select(ci => ci.MenuItemId).ToList();
            var dbMenuItems = await _context.MenuItems
                .Where(m => menuItemIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            foreach (var ci in cart.CartItems)
            {
                if (dbMenuItems.TryGetValue(ci.MenuItemId, out var item))
                {
                    totalAmount += item.Price * ci.Quantity;
                    if (foodStallId == 0)
                    {
                        foodStallId = item.FoodStallId;
                    }
                }
            }

            // 4. Create Order.
            var order = new Order
            {
                ConsumerId = consumerId,
                FoodStallId = foodStallId,
                TotalAmount = totalAmount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // 5. Create OrderItems.
            foreach (var ci in cart.CartItems)
            {
                if (dbMenuItems.TryGetValue(ci.MenuItemId, out var item))
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        MenuItemId = item.Id,
                        Quantity = ci.Quantity,
                        Price = item.Price
                    };
                    _context.OrderItems.Add(orderItem);
                }
            }

            // 6. Create QueueEntry for this stall.
            int lastQueueNum = await _context.QueueEntries
                .Where(q => q.FoodStallId == foodStallId)
                .MaxAsync(q => (int?)q.QueueNumber) ?? 0;

            var queueEntry = new QueueEntry
            {
                OrderId = order.Id,
                FoodStallId = foodStallId,
                QueueNumber = lastQueueNum + 1,
                Status = order.Status
            };

            _context.QueueEntries.Add(queueEntry);

            // 7. Clear Cart.
            _context.CartItems.RemoveRange(cart.CartItems);
            await _context.SaveChangesAsync();

            // Broadcast queue update to stall listeners
            await _hubContext.Clients.Group($"stall-{foodStallId}").SendAsync("ReceiveQueueUpdate", foodStallId.ToString());

            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }

        // GET: Order/Confirmation/5
        public async Task<IActionResult> Confirmation(int id)
        {
            int currentUserId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.FoodStall)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            var queueEntry = await _context.QueueEntries.FirstOrDefaultAsync(q => q.OrderId == id);
            ViewBag.QueueEntry = queueEntry;

            return View(order);
        }

        // GET: Order/Details/5
        public async Task<IActionResult> Details(int id)
        {
            int currentUserId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.Consumer)
                .Include(o => o.FoodStall)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            bool isConsumer = order.ConsumerId == currentUserId;
            bool isStallOwner = order.FoodStall != null && order.FoodStall.OwnerId == currentUserId;

            if (!isConsumer && !isStallOwner)
            {
                return Forbid();
            }

            var queueEntry = await _context.QueueEntries.FirstOrDefaultAsync(q => q.OrderId == id);
            ViewBag.QueueEntry = queueEntry;

            return View(order);
        }

        // GET: Order/History
        public async Task<IActionResult> History()
        {
            int currentUserId = GetCurrentUserId();
            var isOwnerRole = User.IsInRole(Roles.FoodStallOwner);

            if (isOwnerRole)
            {
                var ownerStallIds = await _context.FoodStalls
                    .Where(f => f.OwnerId == currentUserId)
                    .Select(f => f.Id)
                    .ToListAsync();

                var ownerOrders = await _context.Orders
                    .Include(o => o.Consumer)
                    .Include(o => o.FoodStall)
                    .Include(o => o.Payment)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                    .Where(o => ownerStallIds.Contains(o.FoodStallId))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return View(ownerOrders);
            }
            else
            {
                var consumerOrders = await _context.Orders
                    .Include(o => o.FoodStall)
                    .Include(o => o.Payment)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                    .Where(o => o.ConsumerId == currentUserId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return View(consumerOrders);
            }
        }

        // POST: Order/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            int currentUserId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.FoodStall)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            if (order.FoodStall == null || order.FoodStall.OwnerId != currentUserId)
            {
                return Forbid();
            }

            var validStatuses = new[] { "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
            if (validStatuses.Contains(status))
            {
                order.Status = status;

                var queueEntry = await _context.QueueEntries.FirstOrDefaultAsync(q => q.OrderId == id);
                if (queueEntry != null)
                {
                    queueEntry.Status = status;
                }

                await _context.SaveChangesAsync();

                // Send SignalR live status update to consumer
                await _hubContext.Clients.Group($"order-{id}").SendAsync("ReceiveStatusUpdate", id.ToString(), status);
                await _hubContext.Clients.Group($"stall-{order.FoodStallId}").SendAsync("ReceiveQueueUpdate", order.FoodStallId.ToString());
            }

            return RedirectToAction(nameof(History));
        }
    }
}
