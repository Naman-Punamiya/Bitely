using System;
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

        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public async Task<IActionResult> Index()
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

            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int menuItemId, int quantity = 1, string? returnUrl = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            if (quantity <= 0)
            {
                quantity = 1;
            }

            var menuItem = await _context.MenuItems
                .Include(m => m.FoodStall)
                .FirstOrDefaultAsync(m => m.Id == menuItemId);

            if (menuItem == null)
            {
                TempData["ErrorMessage"] = "Menu item not found.";
                return RedirectBack(returnUrl);
            }

            if (!menuItem.IsAvailable)
            {
                TempData["ErrorMessage"] = $"'{menuItem.Name}' is currently out of stock.";
                return RedirectBack(returnUrl);
            }

            if (menuItem.FoodStall == null || !menuItem.FoodStall.IsOpen)
            {
                TempData["ErrorMessage"] = $"'{menuItem.FoodStall?.Name ?? "Food Stall"}' is currently closed.";
                return RedirectBack(returnUrl);
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems.FirstOrDefault(ci => ci.MenuItemId == menuItemId);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                existingItem.UnitPrice = menuItem.Price;
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    MenuItemId = menuItemId,
                    Quantity = quantity,
                    UnitPrice = menuItem.Price
                };
                _context.CartItems.Add(cartItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Added '{menuItem.Name}' to your cart!";
            return RedirectBack(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateItemQuantity(int menuItemId, int quantity, string? returnUrl = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == userId);

            if (cart == null)
            {
                if (quantity <= 0)
                {
                    return RedirectBack(returnUrl);
                }

                cart = new Cart
                {
                    CustomerId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.MenuItemId == menuItemId);

            if (cartItem != null)
            {
                if (quantity <= 0)
                {
                    _context.CartItems.Remove(cartItem);
                    TempData["InfoMessage"] = "Item removed from your cart.";
                }
                else
                {
                    cartItem.Quantity = quantity;
                    TempData["SuccessMessage"] = "Cart updated successfully.";
                }
            }
            else if (quantity > 0)
            {
                var menuItem = await _context.MenuItems.FindAsync(menuItemId);
                if (menuItem != null)
                {
                    cartItem = new CartItem
                    {
                        CartId = cart.Id,
                        MenuItemId = menuItemId,
                        Quantity = quantity,
                        UnitPrice = menuItem.Price
                    };
                    _context.CartItems.Add(cartItem);
                    TempData["SuccessMessage"] = $"Added '{menuItem.Name}' to your cart!";
                }
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectBack(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.Cart!.CustomerId == userId);

            if (cartItem != null)
            {
                if (quantity <= 0)
                {
                    _context.CartItems.Remove(cartItem);
                    TempData["InfoMessage"] = "Item removed from your cart.";
                }
                else
                {
                    cartItem.Quantity = quantity;
                    if (cartItem.Cart != null)
                    {
                        cartItem.Cart.UpdatedAt = DateTime.UtcNow;
                    }
                    TempData["SuccessMessage"] = "Cart updated successfully.";
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.Cart!.CustomerId == userId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                if (cartItem.Cart != null)
                {
                    cartItem.Cart.UpdatedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                TempData["InfoMessage"] = "Item removed from your cart.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == userId);

            if (cart != null && cart.CartItems.Any())
            {
                _context.CartItems.RemoveRange(cart.CartItems);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["InfoMessage"] = "Your cart has been cleared.";
            }

            return RedirectToAction(nameof(Index));
        }

        private IActionResult RedirectBack(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
