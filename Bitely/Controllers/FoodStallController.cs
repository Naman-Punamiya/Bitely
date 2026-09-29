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
    public class FoodStallController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FoodStallController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public async Task<IActionResult> Index()
        {
            var stalls = await _context.FoodStalls
                .Include(f => f.Owner)
                .ToListAsync();
            return View(stalls);
        }

        public async Task<IActionResult> Details(int id)
        {
            var stall = await _context.FoodStalls
                .Include(f => f.Owner)
                .Include(f => f.MenuItems)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (stall == null)
            {
                return NotFound();
            }

            return View(stall);
        }

        [Authorize(Roles = Roles.FoodStallOwner)]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Create(FoodStall foodStall)
        {
            if (ModelState.IsValid)
            {
                foodStall.OwnerId = GetCurrentUserId() ?? string.Empty;
                _context.FoodStalls.Add(foodStall);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(foodStall);
        }

        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Edit(int id)
        {
            var stall = await _context.FoodStalls.FindAsync(id);
            if (stall == null)
            {
                return NotFound();
            }

            if (stall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            return View(stall);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Edit(int id, FoodStall foodStall)
        {
            if (id != foodStall.Id)
            {
                return NotFound();
            }

            var existingStall = await _context.FoodStalls.FindAsync(id);
            if (existingStall == null)
            {
                return NotFound();
            }

            if (existingStall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                existingStall.Name = foodStall.Name;
                existingStall.Description = foodStall.Description;
                existingStall.Location = foodStall.Location;
                existingStall.IsOpen = foodStall.IsOpen;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(foodStall);
        }

        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> Delete(int id)
        {
            var stall = await _context.FoodStalls
                .Include(f => f.Owner)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (stall == null)
            {
                return NotFound();
            }

            if (stall.OwnerId != GetCurrentUserId())
            {
                return Forbid();
            }

            return View(stall);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.FoodStallOwner)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var stall = await _context.FoodStalls.FindAsync(id);
            if (stall != null)
            {
                if (stall.OwnerId != GetCurrentUserId())
                {
                    return Forbid();
                }

                _context.FoodStalls.Remove(stall);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
