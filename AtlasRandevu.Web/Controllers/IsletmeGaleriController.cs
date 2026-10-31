
using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize]
    public class IsletmeGaleriController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public IsletmeGaleriController(
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

            var fotograflar = await _db.IsletmeFotograflari
                .AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value)
                .OrderBy(x => x.Sira)
                .ThenByDescending(x => x.YuklenmeTarihi)
                .ToListAsync();

            ViewBag.IsletmeAdi = await _db.Isletmeler
                .Where(x => x.Id == isletmeId.Value)
                .Select(x => x.Ad)
                .FirstOrDefaultAsync();

            return View(fotograflar);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public async Task<IActionResult> Yukle(
            List<IFormFile>? fotograflar)
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            if (fotograflar == null || fotograflar.Count == 0)
            {
                TempData["Hata"] = "Lütfen fotoğraf seçiniz.";
                return RedirectToAction(nameof(Index));
            }

            if (fotograflar.Count > 5)
            {
                TempData["Hata"] =
                    "Tek seferde en fazla 5 fotoğraf yükleyebilirsiniz.";
                return RedirectToAction(nameof(Index));
            }

            var mevcutSayi = await _db.IsletmeFotograflari
                .CountAsync(x => x.IsletmeId == isletmeId.Value);

            if (mevcutSayi + fotograflar.Count > 20)
            {
                TempData["Hata"] =
                    "Bir işletmenin galerisinde en fazla 20 fotoğraf olabilir.";
                return RedirectToAction(nameof(Index));
            }

            var izinliUzantilar =
                new[] { ".jpg", ".jpeg", ".png", ".webp" };

            foreach (var dosya in fotograflar)
            {
                if (dosya.Length == 0 ||
                    dosya.Length > 5 * 1024 * 1024)
                {
                    TempData["Hata"] =
                        "Her fotoğraf 5 MB veya daha küçük olmalıdır.";
                    return RedirectToAction(nameof(Index));
                }

                var uzanti = Path.GetExtension(dosya.FileName)
                    .ToLowerInvariant();

                if (!izinliUzantilar.Contains(uzanti))
                {
                    TempData["Hata"] =
                        "Yalnızca JPG, PNG ve WebP yükleyebilirsiniz.";
                    return RedirectToAction(nameof(Index));
                }

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
                    ((uzanti == ".jpg" || uzanti == ".jpeg") && jpg) ||
                    (uzanti == ".png" && png) ||
                    (uzanti == ".webp" && webp);

                if (!gecerli)
                {
                    TempData["Hata"] =
                        "Geçersiz fotoğraf içeriği tespit edildi.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var klasor = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "isletmeler",
                isletmeId.Value.ToString(),
                "galeri");

            Directory.CreateDirectory(klasor);

            var sonSira = await _db.IsletmeFotograflari
                .Where(x => x.IsletmeId == isletmeId.Value)
                .MaxAsync(x => (int?)x.Sira) ?? 0;

            var kaydedilenDosyalar = new List<string>();

            try
            {
                foreach (var dosya in fotograflar)
                {
                    var uzanti = Path.GetExtension(dosya.FileName)
                        .ToLowerInvariant();

                    var dosyaAdi =
                        Guid.NewGuid().ToString("N") + uzanti;

                    var tamYol = Path.Combine(klasor, dosyaAdi);

                    await using (var stream = new FileStream(
                        tamYol, FileMode.CreateNew))
                    {
                        await dosya.CopyToAsync(stream);
                    }

                    kaydedilenDosyalar.Add(tamYol);

                    sonSira++;

                    _db.IsletmeFotograflari.Add(
                        new IsletmeFotografi
                        {
                            IsletmeId = isletmeId.Value,
                            DosyaYolu =
                                "/uploads/isletmeler/" +
                                isletmeId.Value +
                                "/galeri/" + dosyaAdi,
                            Sira = sonSira,
                            YuklenmeTarihi = DateTime.UtcNow
                        });
                }

                await _db.SaveChangesAsync();
            }
            catch
            {
                foreach (var yol in kaydedilenDosyalar)
                {
                    if (System.IO.File.Exists(yol))
                        System.IO.File.Delete(yol);
                }

                TempData["Hata"] =
                    "Fotoğraflar yüklenirken bir hata oluştu.";

                return RedirectToAction(nameof(Index));
            }

            TempData["Basari"] =
                fotograflar.Count + " fotoğraf başarıyla yüklendi.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var isletmeId = await IsletmeIdGetir();

            if (isletmeId == null)
                return Forbid();

            var fotograf = await _db.IsletmeFotograflari
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsletmeId == isletmeId.Value);

            if (fotograf == null)
                return NotFound();

            _db.IsletmeFotograflari.Remove(fotograf);
            await _db.SaveChangesAsync();

            var dosyaAdi = Path.GetFileName(fotograf.DosyaYolu);

            var tamYol = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "isletmeler",
                isletmeId.Value.ToString(),
                "galeri",
                dosyaAdi);

            if (System.IO.File.Exists(tamYol))
                System.IO.File.Delete(tamYol);

            TempData["Basari"] = "Fotoğraf silindi.";

            return RedirectToAction(nameof(Index));
        }
    }
}
