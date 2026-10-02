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
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public async Task<IActionResult> Index(int? foodStallId = null, string? searchString = null, string? category = null)
        {
            var query = _context.FoodStalls
                .Include(f => f.Owner)
                .Include(f => f.MenuItems)
                .AsQueryable();

            if (foodStallId.HasValue && foodStallId.Value > 0)
            {
                query = query.Where(f => f.Id == foodStallId.Value);
            }

            var foodStalls = await query.OrderBy(f => f.Name).ToListAsync();

            // Collect all unique categories across all items for category filtering
            var allCategories = await _context.MenuItems
                .Where(m => !string.IsNullOrEmpty(m.Category))
                .Select(m => m.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            // Collect all stalls for stall filtering
            var allStalls = await _context.FoodStalls
                .OrderBy(f => f.Name)
                .Select(f => new { f.Id, f.Name })
                .ToListAsync();

            // Apply search or category filtering on the items
            if (!string.IsNullOrWhiteSpace(searchString) || !string.IsNullOrWhiteSpace(category))
            {
                var searchLower = searchString?.Trim().ToLower() ?? string.Empty;
                var categoryTrim = category?.Trim() ?? string.Empty;

                foreach (var stall in foodStalls)
                {
                    stall.MenuItems = stall.MenuItems.Where(m =>
                        (string.IsNullOrEmpty(categoryTrim) || m.Category.Equals(categoryTrim, StringComparison.OrdinalIgnoreCase)) &&
                        (string.IsNullOrEmpty(searchLower) ||
                         m.Name.ToLower().Contains(searchLower) ||
                         m.Category.ToLower().Contains(searchLower) ||
                         m.Description.ToLower().Contains(searchLower) ||
                         stall.Name.ToLower().Contains(searchLower))
                    ).ToList();
                }

                // Filter out food stalls that have no matching items if search/category filter is active
                foodStalls = foodStalls.Where(f => f.MenuItems.Any()).ToList();
            }

            var currentUserId = GetCurrentUserId();
            var cartItemDict = new Dictionary<int, int>();
            if (!string.IsNullOrEmpty(currentUserId))
            {
                cartItemDict = await _context.Carts
                    .Where(c => c.CustomerId == currentUserId)
                    .SelectMany(c => c.CartItems)
                    .ToDictionaryAsync(ci => ci.MenuItemId, ci => ci.Quantity);
            }

            ViewBag.SelectedStallId = foodStallId;
            ViewBag.SearchString = searchString;
            ViewBag.SelectedCategory = category;
            ViewBag.Categories = allCategories;
            ViewBag.AllStalls = allStalls;
            ViewBag.CartItemQuantities = cartItemDict;

            return View(foodStalls);
        }

        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Create(int foodStallId)
        {
            var stall = await _context.FoodStalls.FindAsync(foodStallId);
            if (stall == null)
            {
                return NotFound();
            }

            if (stall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            var item = new MenuItem { FoodStallId = foodStallId, IsAvailable = true };
            ViewBag.FoodStall = stall;
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Create(MenuItem menuItem)
        {
            var stall = await _context.FoodStalls.FindAsync(menuItem.FoodStallId);
            if (stall == null)
            {
                return NotFound();
            }

            if (stall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                _context.MenuItems.Add(menuItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { foodStallId = menuItem.FoodStallId });
            }

            ViewBag.FoodStall = stall;
            return View(menuItem);
        }

        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Edit(int id)
        {
            var menuItem = await _context.MenuItems
                .Include(m => m.FoodStall)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menuItem == null)
            {
                return NotFound();
            }

            if (menuItem.FoodStall == null || menuItem.FoodStall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            return View(menuItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Edit(int id, MenuItem menuItem)
        {
            if (id != menuItem.Id)
            {
                return NotFound();
            }

            var existingItem = await _context.MenuItems
                .Include(m => m.FoodStall)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (existingItem == null)
            {
                return NotFound();
            }

            if (existingItem.FoodStall == null || existingItem.FoodStall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                existingItem.Name = menuItem.Name;
                existingItem.Description = menuItem.Description;
                existingItem.Price = menuItem.Price;
                existingItem.Category = menuItem.Category;
                existingItem.IsAvailable = menuItem.IsAvailable;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { foodStallId = existingItem.FoodStallId });
            }

            return View(menuItem);
        }

        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Delete(int id)
        {
            var menuItem = await _context.MenuItems
                .Include(m => m.FoodStall)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menuItem == null)
            {
                return NotFound();
            }

            if (menuItem.FoodStall == null || menuItem.FoodStall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            return View(menuItem);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var menuItem = await _context.MenuItems
                .Include(m => m.FoodStall)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menuItem != null)
            {
                if (menuItem.FoodStall == null || menuItem.FoodStall.OwnerId != GetCurrentUserId())
                {
                    return Forbid();
                }

                int foodStallId = menuItem.FoodStallId;
                _context.MenuItems.Remove(menuItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { foodStallId = foodStallId });
            }

            return RedirectToAction("Index", "FoodStall");
        }
    }
}
