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
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }

        private async Task<Cart> GetOrCreateCartAsync(int consumerId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.MenuItem!)
                .ThenInclude(m => m.FoodStall)
                .FirstOrDefaultAsync(c => c.ConsumerId == consumerId);

            if (cart == null)
            {
                cart = new Cart
                {
                    ConsumerId = consumerId
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            int consumerId = GetCurrentUserId();
            var cart = await GetOrCreateCartAsync(consumerId);
            return View(cart);
        }

        // POST: Cart/AddItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(int menuItemId, int quantity = 1)
        {
            int consumerId = GetCurrentUserId();
            var menuItem = await _context.MenuItems.FindAsync(menuItemId);
            if (menuItem == null || !menuItem.IsAvailable)
            {
                return NotFound();
            }

            var cart = await GetOrCreateCartAsync(consumerId);

            // If cart contains items from another stall, clear them to keep 1 stall per cart/order simplicity
            var currentStallId = cart.CartItems.FirstOrDefault()?.MenuItem?.FoodStallId;
            if (currentStallId.HasValue && currentStallId.Value != menuItem.FoodStallId)
            {
                _context.CartItems.RemoveRange(cart.CartItems);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems.FirstOrDefault(ci => ci.MenuItemId == menuItemId);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    MenuItemId = menuItemId,
                    Quantity = quantity
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            int consumerId = GetCurrentUserId();
            var cart = await GetOrCreateCartAsync(consumerId);
            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.Id == cartItemId);

            if (cartItem != null)
            {
                if (quantity <= 0)
                {
                    _context.CartItems.Remove(cartItem);
                }
                else
                {
                    cartItem.Quantity = quantity;
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/RemoveItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            int consumerId = GetCurrentUserId();
            var cart = await GetOrCreateCartAsync(consumerId);
            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.Id == cartItemId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/ClearCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            int consumerId = GetCurrentUserId();
            var cart = await GetOrCreateCartAsync(consumerId);

            if (cart.CartItems.Any())
            {
                _context.CartItems.RemoveRange(cart.CartItems);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
