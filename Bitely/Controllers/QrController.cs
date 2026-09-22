using System.Security.Claims;
using System.Threading.Tasks;
using Bitely.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace Bitely.Controllers
{
    public class QrController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QrController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Qr/Index?foodStallId=5
        public async Task<IActionResult> Index(int foodStallId)
        {
            var foodStall = await _context.FoodStalls.FirstOrDefaultAsync(f => f.Id == foodStallId);
            if (foodStall == null)
            {
                return NotFound();
            }

            var targetUrl = $"{Request.Scheme}://{Request.Host}/Menu?foodStallId={foodStallId}";
            ViewBag.TargetUrl = targetUrl;
            return View(foodStall);
        }

        // GET: Qr/Generate?foodStallId=5
        public async Task<IActionResult> Generate(int foodStallId)
        {
            var foodStall = await _context.FoodStalls.FirstOrDefaultAsync(f => f.Id == foodStallId);
            if (foodStall == null)
            {
                return NotFound();
            }

            var targetUrl = $"{Request.Scheme}://{Request.Host}/Menu?foodStallId={foodStallId}";

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(targetUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeGraphic = qrCode.GetGraphic(10);

            return File(qrCodeGraphic, "image/png");
        }
    }
}
