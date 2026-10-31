using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "PlatformYoneticisi")]
    public class PlatformController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public PlatformController(AtlasRandevuDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.ToplamIsletme = await _db.Isletmeler.CountAsync();
            ViewBag.BekleyenBasvuru = await _db.Isletmeler.CountAsync(x => x.Durum == "OnayBekliyor");
            ViewBag.OnayliIsletme = await _db.Isletmeler.CountAsync(x => x.Durum == "Onaylandi");
            ViewBag.ToplamRandevu = await _db.Randevular.CountAsync();
            ViewBag.TamamlananRandevu = await _db.Randevular.CountAsync(x => x.Durum == "Tamamlandi");

            // Müşteri sayısı Identity'deki Musteri rolünden hesaplanır.
            ViewBag.ToplamMusteri = await (
                from kullaniciRolu in _db.UserRoles
                join rol in _db.Roles on kullaniciRolu.RoleId equals rol.Id
                where rol.Name == "Musteri"
                select kullaniciRolu.UserId
            ).Distinct().CountAsync();

            var isletmeler = await _db.Isletmeler
                .AsNoTracking()
                .Include(x => x.Sektor)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();

            return View(isletmeler);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Onayla(int id)
        {
            var isletme = await _db.Isletmeler.FirstOrDefaultAsync(x => x.Id == id);
            if (isletme == null) return NotFound();

            if (isletme.Durum != "OnayBekliyor")
            {
                TempData["Hata"] = "Yalnızca bekleyen başvurular onaylanabilir.";
                return RedirectToAction(nameof(Index));
            }

            isletme.Durum = "Onaylandi";
            isletme.OnayTarihi = DateTime.UtcNow;
            isletme.OnaylayanKullaniciId = _userManager.GetUserId(User);

            var subeler = await _db.Subeler.Where(x => x.IsletmeId == id).ToListAsync();
            foreach (var sube in subeler) sube.Durum = "Aktif";

            await _db.SaveChangesAsync();
            TempData["Basari"] = "İşletme başvurusu onaylandı.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reddet(int id)
        {
            var isletme = await _db.Isletmeler.FirstOrDefaultAsync(x => x.Id == id);
            if (isletme == null) return NotFound();

            if (isletme.Durum != "OnayBekliyor")
            {
                TempData["Hata"] = "Yalnızca bekleyen başvurular reddedilebilir.";
                return RedirectToAction(nameof(Index));
            }

            isletme.Durum = "Reddedildi";
            await _db.SaveChangesAsync();
            TempData["Basari"] = "İşletme başvurusu reddedildi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
