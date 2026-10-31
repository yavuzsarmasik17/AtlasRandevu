
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize]
    public class IsletmeProfilController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public IsletmeProfilController(
            AtlasRandevuDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
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

            var isletme = await _db.Isletmeler
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == isletmeId.Value);

            if (isletme == null)
                return NotFound();

            return View(new IsletmeProfilViewModel
            {
                IsletmeAdi = isletme.Ad,
                Hakkimizda = isletme.Hakkimizda,
                Slogan = isletme.Slogan,
                WebSitesi = isletme.WebSitesi,
                InstagramAdresi = isletme.InstagramAdresi,
                LogoYolu = isletme.LogoYolu,
                KapakFotografiYolu = isletme.KapakFotografiYolu
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(
            IsletmeProfilViewModel model)
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            var isletme = await _db.Isletmeler
                .FirstOrDefaultAsync(x => x.Id == isletmeId.Value);

            if (isletme == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                model.IsletmeAdi = isletme.Ad;
                model.LogoYolu = isletme.LogoYolu;
                model.KapakFotografiYolu =
                    isletme.KapakFotografiYolu;

                return View(model);
            }

            isletme.Hakkimizda = model.Hakkimizda?.Trim();
            isletme.Slogan = model.Slogan?.Trim();
            isletme.WebSitesi = model.WebSitesi?.Trim();
            isletme.InstagramAdresi = model.InstagramAdresi?.Trim();
            isletme.ProfilGuncellemeTarihi = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Basari"] =
                "İşletme profiliniz başarıyla güncellendi.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> FotografYukle(
            IFormFile? dosya,
            string tur)
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            if (tur != "logo" && tur != "kapak")
                return BadRequest();

            if (dosya == null || dosya.Length == 0)
            {
                TempData["Hata"] = "Lütfen bir fotoğraf seçiniz.";
                return RedirectToAction(nameof(Index));
            }

            if (dosya.Length > 5 * 1024 * 1024)
            {
                TempData["Hata"] =
                    "Fotoğraf boyutu en fazla 5 MB olabilir.";

                return RedirectToAction(nameof(Index));
            }

            var uzanti = Path.GetExtension(dosya.FileName)
                .ToLowerInvariant();

            var izinliUzantilar =
                new[] { ".jpg", ".jpeg", ".png", ".webp" };

            if (!izinliUzantilar.Contains(uzanti))
            {
                TempData["Hata"] =
                    "Yalnızca JPG, PNG ve WebP yükleyebilirsiniz.";

                return RedirectToAction(nameof(Index));
            }

            var isletme = await _db.Isletmeler
                .FirstOrDefaultAsync(x => x.Id == isletmeId.Value);

            if (isletme == null)
                return NotFound();

            // Dosya imzası kontrolü
            byte[] baslik = new byte[12];
            int okunan;

            using (var stream = dosya.OpenReadStream())
            {
                okunan = await stream.ReadAsync(
                    baslik, 0, baslik.Length);
            }

            bool jpg = okunan >= 3 &&
                baslik[0] == 0xFF &&
                baslik[1] == 0xD8 &&
                baslik[2] == 0xFF;

            bool png = okunan >= 8 &&
                baslik[0] == 0x89 &&
                baslik[1] == 0x50 &&
                baslik[2] == 0x4E &&
                baslik[3] == 0x47 &&
                baslik[4] == 0x0D &&
                baslik[5] == 0x0A &&
                baslik[6] == 0x1A &&
                baslik[7] == 0x0A;

            bool webp = okunan >= 12 &&
                baslik[0] == 0x52 &&
                baslik[1] == 0x49 &&
                baslik[2] == 0x46 &&
                baslik[3] == 0x46 &&
                baslik[8] == 0x57 &&
                baslik[9] == 0x45 &&
                baslik[10] == 0x42 &&
                baslik[11] == 0x50;

            bool gecerli =
                (uzanti == ".jpg" || uzanti == ".jpeg") && jpg ||
                uzanti == ".png" && png ||
                uzanti == ".webp" && webp;

            if (!gecerli)
            {
                TempData["Hata"] =
                    "Dosyanın içeriği seçilen fotoğraf türüyle uyuşmuyor.";

                return RedirectToAction(nameof(Index));
            }

            var klasor = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "isletmeler",
                isletmeId.Value.ToString());

            Directory.CreateDirectory(klasor);

            var dosyaAdi = Guid.NewGuid().ToString("N") + uzanti;

            var tamYol = Path.Combine(klasor, dosyaAdi);

            await using (var stream = new FileStream(
                tamYol, FileMode.CreateNew))
            {
                await dosya.CopyToAsync(stream);
            }

            var webYolu =
                "/uploads/isletmeler/" +
                isletmeId.Value + "/" + dosyaAdi;

            if (tur == "logo")
                isletme.LogoYolu = webYolu;
            else
                isletme.KapakFotografiYolu = webYolu;

            isletme.ProfilGuncellemeTarihi = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Basari"] = tur == "logo"
                ? "İşletme logosu başarıyla yüklendi."
                : "Kapak fotoğrafı başarıyla yüklendi.";

            return RedirectToAction(nameof(Index));
        }
    }

    public class IsletmeProfilViewModel
    {
        public string IsletmeAdi { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Hakkimizda { get; set; }

        [StringLength(150)]
        public string? Slogan { get; set; }

        [StringLength(300)]
        [Url(ErrorMessage =
            "Geçerli bir web sitesi adresi giriniz.")]
        public string? WebSitesi { get; set; }

        [StringLength(300)]
        public string? InstagramAdresi { get; set; }

        public string? LogoYolu { get; set; }

        public string? KapakFotografiYolu { get; set; }
    }
}
