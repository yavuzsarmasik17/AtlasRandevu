
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize]
    public class HizmetlerController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public HizmetlerController(
            AtlasRandevuDbContext db,
            UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private async Task<int?> IsletmeIdGetir()
        {
            var kullaniciId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(kullaniciId))
                return null;

            return await _db.IsletmeKullanicilari
                .AsNoTracking()
                .Where(x =>
                    x.KullaniciId == kullaniciId &&
                    x.AktifMi &&
                    x.Rol == "IsletmeSahibi" &&
                    x.Isletme.Durum == "Onaylandi")
                .Select(x => (int?)x.IsletmeId)
                .FirstOrDefaultAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            var hizmetler = await _db.Hizmetler
                .AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value)
                .OrderBy(x => x.Ad)
                .ToListAsync();

            return View(hizmetler);
        }

        [HttpGet]
        public async Task<IActionResult> Ekle()
        {
            if (await IsletmeIdGetir() == null)
                return Forbid();

            return View(new HizmetFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(
            HizmetFormViewModel model)
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            if (!ModelState.IsValid)
                return View(model);

            var hizmet = new AtlasRandevu.Domain.Entities.Hizmet
            {
                IsletmeId = isletmeId.Value,
                Ad = model.Ad.Trim(),
                Fiyat = model.Fiyat,
                SureDakika = model.SureDakika,
                AktifMi = true
            };

            _db.Hizmetler.Add(hizmet);
            await _db.SaveChangesAsync();

            TempData["Basari"] = "Hizmet başarıyla eklendi.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            var hizmet = await _db.Hizmetler
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsletmeId == isletmeId.Value);

            if (hizmet == null)
                return NotFound();

            return View(new HizmetFormViewModel
            {
                Ad = hizmet.Ad,
                Fiyat = hizmet.Fiyat,
                SureDakika = hizmet.SureDakika
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(
            int id,
            HizmetFormViewModel model)
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            if (!ModelState.IsValid)
                return View(model);

            var hizmet = await _db.Hizmetler
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsletmeId == isletmeId.Value);

            if (hizmet == null)
                return NotFound();

            hizmet.Ad = model.Ad.Trim();
            hizmet.Fiyat = model.Fiyat;
            hizmet.SureDakika = model.SureDakika;

            await _db.SaveChangesAsync();

            TempData["Basari"] = "Hizmet güncellendi.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DurumDegistir(int id)
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            var hizmet = await _db.Hizmetler
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsletmeId == isletmeId.Value);

            if (hizmet == null)
                return NotFound();

            hizmet.AktifMi = !hizmet.AktifMi;

            await _db.SaveChangesAsync();

            TempData["Basari"] = hizmet.AktifMi
                ? "Hizmet aktif edildi."
                : "Hizmet pasife alındı.";

            return RedirectToAction(nameof(Index));
        }
    }

    public class HizmetFormViewModel
    {
        [Required(ErrorMessage = "Hizmet adı zorunludur.")]
        [StringLength(150)]
        [Display(Name = "Hizmet Adı")]
        public string Ad { get; set; } = "";

        [Range(typeof(decimal), "0,01", "9999999",
            ErrorMessage = "Geçerli bir fiyat giriniz.")]
        [Display(Name = "Fiyat (TL)")]
        public decimal Fiyat { get; set; }

        [Range(5, 600,
            ErrorMessage = "Süre 5 ile 600 dakika arasında olmalıdır.")]
        [Display(Name = "Süre (Dakika)")]
        public int SureDakika { get; set; } = 30;
    }
}
