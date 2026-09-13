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

        private int GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }

        // GET: Menu?foodStallId=1
        public async Task<IActionResult> Index(int foodStallId)
        {
            var foodStall = await _context.FoodStalls
                .FirstOrDefaultAsync(f => f.Id == foodStallId);

            if (foodStall == null)
            {
                return NotFound();
            }

            var menuItems = await _context.MenuItems
                .Where(m => m.FoodStallId == foodStallId)
                .ToListAsync();

            ViewBag.FoodStall = foodStall;
            return View(menuItems);
        }

        // GET: Menu/Create?foodStallId=1
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

        // POST: Menu/Create
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

        // GET: Menu/Edit/5
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

        // POST: Menu/Edit/5
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

        // GET: Menu/Delete/5
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

        // POST: Menu/Delete/5
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
