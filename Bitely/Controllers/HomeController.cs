using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Bitely.Data;
using Bitely.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bitely.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var openStallsCount = await _context.FoodStalls.CountAsync(s => s.IsOpen);
            var totalDishesCount = await _context.MenuItems.CountAsync(m => m.IsAvailable);
            var popularCategories = await _context.MenuItems
                .Where(m => !string.IsNullOrEmpty(m.Category))
                .Select(m => m.Category)
                .Distinct()
                .Take(6)
                .ToListAsync();

            ViewBag.OpenStallsCount = openStallsCount;
            ViewBag.TotalDishesCount = totalDishesCount;
            ViewBag.Categories = popularCategories;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
