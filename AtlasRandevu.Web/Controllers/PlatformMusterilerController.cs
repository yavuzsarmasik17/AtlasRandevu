using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "PlatformYoneticisi")]
    public class PlatformMusterilerController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public PlatformMusterilerController(AtlasRandevuDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? ara)
        {
            var rol = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Name == "Musteri");
            var ids = rol == null
                ? new List<string>()
                : await _db.UserRoles.AsNoTracking().Where(x => x.RoleId == rol.Id)
                    .Select(x => x.UserId).ToListAsync();

            var kullanicilar = await _db.Users.AsNoTracking()
                .Where(x => ids.Contains(x.Id)).OrderBy(x => x.Email).ToListAsync();

            var profiller = await _db.MusteriProfilleri.AsNoTracking()
                .Where(x => ids.Contains(x.KullaniciId))
                .ToDictionaryAsync(x => x.KullaniciId);

            var randevuSayilari = await _db.Randevular.AsNoTracking()
                .Where(x => ids.Contains(x.MusteriKullaniciId))
                .GroupBy(x => x.MusteriKullaniciId)
                .Select(x => new { KullaniciId = x.Key, Sayi = x.Count() })
                .ToDictionaryAsync(x => x.KullaniciId, x => x.Sayi);

            var liste = kullanicilar.Select(k =>
            {
                profiller.TryGetValue(k.Id, out var profil);
                return new PlatformMusteriSatiri
                {
                    KullaniciId = k.Id,
                    Ad = profil?.Ad ?? "",
                    Soyad = profil?.Soyad ?? "",
                    Eposta = k.Email ?? "",
                    Telefon = k.PhoneNumber ?? "",
                    KayitTarihi = profil?.OlusturmaTarihi,
                    RandevuSayisi = randevuSayilari.GetValueOrDefault(k.Id)
                };
            }).ToList();

            ViewBag.ToplamMusteri = liste.Count;
            ViewBag.RandevusuOlan = liste.Count(x => x.RandevuSayisi > 0);
            ViewBag.ToplamRandevu = liste.Sum(x => x.RandevuSayisi);
            ViewBag.EksikProfil = liste.Count(x => string.IsNullOrWhiteSpace(x.Ad) || string.IsNullOrWhiteSpace(x.Soyad));
            ViewBag.Ara = ara ?? "";

            if (!string.IsNullOrWhiteSpace(ara))
            {
                var aranan = ara.Trim();
                liste = liste.Where(x =>
                    (x.Ad + " " + x.Soyad).Contains(aranan, StringComparison.OrdinalIgnoreCase) ||
                    x.Eposta.Contains(aranan, StringComparison.OrdinalIgnoreCase) ||
                    x.Telefon.Contains(aranan, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            return View(liste);
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !await MusteriMi(id)) return NotFound();
            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound();
            var profil = await _db.MusteriProfilleri.AsNoTracking()
                .FirstOrDefaultAsync(x => x.KullaniciId == id);

            return View(new PlatformMusteriDuzenleViewModel
            {
                KullaniciId = id,
                Ad = profil?.Ad ?? "",
                Soyad = profil?.Soyad ?? "",
                Eposta = user.Email ?? "",
                Telefon = user.PhoneNumber ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(PlatformMusteriDuzenleViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.KullaniciId) || !await MusteriMi(model.KullaniciId))
                return NotFound();

            var user = await _users.FindByIdAsync(model.KullaniciId);
            if (user == null) return NotFound();
            model.Eposta = user.Email ?? "";
            model.Telefon = user.PhoneNumber ?? "";
            if (!ModelState.IsValid) return View(model);

            var profil = await _db.MusteriProfilleri
                .FirstOrDefaultAsync(x => x.KullaniciId == model.KullaniciId);
            if (profil == null)
            {
                profil = new MusteriProfili
                {
                    KullaniciId = model.KullaniciId,
                    OlusturmaTarihi = DateTime.UtcNow
                };
                _db.MusteriProfilleri.Add(profil);
            }

            profil.Ad = model.Ad.Trim();
            profil.Soyad = model.Soyad.Trim();
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Bilgiler kaydedilemedi. Lütfen yeniden deneyin.");
                return View(model);
            }

            TempData["Basari"] = "Müşterinin ad ve soyad bilgileri güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> MusteriMi(string id)
        {
            return await (from ur in _db.UserRoles
                          join r in _db.Roles on ur.RoleId equals r.Id
                          where ur.UserId == id && r.Name == "Musteri"
                          select ur.UserId).AnyAsync();
        }
    }

    public class PlatformMusteriSatiri
    {
        public string KullaniciId { get; set; } = "";
        public string Ad { get; set; } = "";
        public string Soyad { get; set; } = "";
        public string Eposta { get; set; } = "";
        public string Telefon { get; set; } = "";
        public DateTime? KayitTarihi { get; set; }
        public int RandevuSayisi { get; set; }
    }

    public class PlatformMusteriDuzenleViewModel
    {
        [Required]
        public string KullaniciId { get; set; } = "";
        [Required(ErrorMessage = "Ad zorunludur.")]
        [StringLength(60)]
        public string Ad { get; set; } = "";
        [Required(ErrorMessage = "Soyad zorunludur.")]
        [StringLength(60)]
        public string Soyad { get; set; } = "";
        public string Eposta { get; set; } = "";
        public string Telefon { get; set; } = "";
    }
}
