using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize]
    public class PersonelController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public PersonelController(AtlasRandevuDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // İşletme kimliği formdan alınmaz: oturumdaki aktif işletme sahibi üzerinden bulunur.
        private async Task<int?> YetkiliIsletmeId()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || await _userManager.IsInRoleAsync(user, "PlatformAdmin") ||
                await _userManager.IsInRoleAsync(user, "Musteri")) return null;

            return await _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == user.Id && x.AktifMi && x.Rol == "IsletmeSahibi" &&
                            x.Isletme.Durum == "Onaylandi")
                .Select(x => (int?)x.IsletmeId)
                .FirstOrDefaultAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var isletmeId = await YetkiliIsletmeId();
            if (isletmeId == null) return Forbid();

            var personeller = await _db.Personeller.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value)
                .Include(x => x.Sube)
                .OrderByDescending(x => x.AktifMi)
                .ThenBy(x => x.AdSoyad)
                .ToListAsync();
            return View(personeller);
        }

        [HttpGet]
        public async Task<IActionResult> Ekle()
        {
            var isletmeId = await YetkiliIsletmeId();
            if (isletmeId == null) return Forbid();
            await SubeleriYukle(isletmeId.Value);
            return View(new PersonelFormViewModel { AktifMi = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(PersonelFormViewModel model)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (isletmeId == null) return Forbid();
            await Dogrula(model, isletmeId.Value);
            if (!ModelState.IsValid)
            {
                await SubeleriYukle(isletmeId.Value);
                return View(model);
            }

            var personel = new Personel
            {
                IsletmeId = isletmeId.Value,
                SubeId = model.SubeId,
                AdSoyad = model.AdSoyad.Trim(),
                Unvan = model.Unvan.Trim(),
                Telefon = string.IsNullOrWhiteSpace(model.Telefon) ? null : model.Telefon.Trim(),
                AktifMi = model.AktifMi,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _db.Personeller.Add(personel);
            await _db.SaveChangesAsync();
            TempData["PersonelMesaj"] = "Personel başarıyla eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (isletmeId == null) return Forbid();
            var personel = await _db.Personeller.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsletmeId == isletmeId.Value);
            if (personel == null) return NotFound();

            await SubeleriYukle(isletmeId.Value);
            return View(new PersonelFormViewModel
            {
                Id = personel.Id,
                SubeId = personel.SubeId,
                AdSoyad = personel.AdSoyad,
                Unvan = personel.Unvan,
                Telefon = personel.Telefon,
                AktifMi = personel.AktifMi
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(int id, PersonelFormViewModel model)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (isletmeId == null) return Forbid();
            if (id != model.Id) return BadRequest();

            var personel = await _db.Personeller
                .FirstOrDefaultAsync(x => x.Id == id && x.IsletmeId == isletmeId.Value);
            if (personel == null) return NotFound();
            await Dogrula(model, isletmeId.Value);
            if (!ModelState.IsValid)
            {
                await SubeleriYukle(isletmeId.Value);
                return View(model);
            }

            // Başka şubeye taşıma, gelecekteki randevuları etkileyebilir.
            if (personel.SubeId != model.SubeId && await _db.Randevular.AnyAsync(r =>
                r.PersonelId == id && r.BaslangicTarihi >= DateTime.Now &&
                (r.Durum == "OnayBekliyor" || r.Durum == "Onaylandi")))
            {
                ModelState.AddModelError(nameof(model.SubeId),
                    "Bu personelin gelecekte aktif randevuları var. Önce bu randevuları düzenleyin.");
                await SubeleriYukle(isletmeId.Value);
                return View(model);
            }

            personel.SubeId = model.SubeId;
            personel.AdSoyad = model.AdSoyad.Trim();
            personel.Unvan = model.Unvan.Trim();
            personel.Telefon = string.IsNullOrWhiteSpace(model.Telefon) ? null : model.Telefon.Trim();
            personel.AktifMi = model.AktifMi;
            await _db.SaveChangesAsync();
            TempData["PersonelMesaj"] = "Personel bilgileri güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DurumDegistir(int id)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (isletmeId == null) return Forbid();
            var personel = await _db.Personeller
                .FirstOrDefaultAsync(x => x.Id == id && x.IsletmeId == isletmeId.Value);
            if (personel == null) return NotFound();

            if (personel.AktifMi && await _db.Randevular.AnyAsync(r =>
                r.PersonelId == id && r.BaslangicTarihi >= DateTime.Now &&
                (r.Durum == "OnayBekliyor" || r.Durum == "Onaylandi")))
            {
                TempData["PersonelHata"] = "Personelin gelecekte aktif randevuları var. Önce bunları düzenleyin.";
                return RedirectToAction(nameof(Index));
            }

            personel.AktifMi = !personel.AktifMi;
            await _db.SaveChangesAsync();
            TempData["PersonelMesaj"] = personel.AktifMi ? "Personel aktifleştirildi." : "Personel pasifleştirildi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task Dogrula(PersonelFormViewModel model, int isletmeId)
        {
            if (!await _db.Subeler.AsNoTracking().AnyAsync(x =>
                x.Id == model.SubeId && x.IsletmeId == isletmeId && x.Durum == "Aktif"))
                ModelState.AddModelError(nameof(model.SubeId), "Geçerli ve aktif bir şube seçin.");
            if (string.IsNullOrWhiteSpace(model.AdSoyad))
                ModelState.AddModelError(nameof(model.AdSoyad), "Ad soyad boş bırakılamaz.");
            if (string.IsNullOrWhiteSpace(model.Unvan))
                ModelState.AddModelError(nameof(model.Unvan), "Unvan boş bırakılamaz.");
        }

        private async Task SubeleriYukle(int isletmeId)
        {
            var subeler = await _db.Subeler.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId && x.Durum == "Aktif")
                .OrderBy(x => x.Ad)
                .Select(x => new { x.Id, x.Ad })
                .ToListAsync();
            ViewBag.Subeler = new SelectList(subeler, "Id", "Ad");
        }
    }

    public class PersonelFormViewModel
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Şube seçin.")]
        public int SubeId { get; set; }

        [Required(ErrorMessage = "Ad soyad girin.")]
        [StringLength(150)]
        public string AdSoyad { get; set; } = "";

        [Required(ErrorMessage = "Unvan girin.")]
        [StringLength(100)]
        public string Unvan { get; set; } = "";

        [StringLength(20)]
        public string? Telefon { get; set; }

        public bool AktifMi { get; set; } = true;
    }
}
