using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Roles = "Musteri")]
    public class MusteriYorumController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;
        public MusteriYorumController(AtlasRandevuDbContext db, UserManager<IdentityUser> users)
        { _db = db; _users = users; }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var uid = _users.GetUserId(User);
            var randevular = await _db.Randevular.AsNoTracking()
                .Where(x => x.MusteriKullaniciId == uid && x.Durum == "Tamamlandi")
                .OrderByDescending(x => x.BaslangicTarihi)
                .Select(x => new MusteriYorumRandevuSatiri
                {
                    Id = x.Id,
                    IsletmeAdi = x.Isletme.Ad,
                    HizmetAdi = x.Hizmet.Ad,
                    Tarih = x.BaslangicTarihi,
                    YorumYapildi = _db.MusteriYorumlari.Any(y => y.RandevuId == x.Id)
                }).Take(200).ToListAsync();
            return View(randevular);
        }

        [HttpGet]
        public async Task<IActionResult> Yaz(int randevuId)
        {
            var uid = _users.GetUserId(User);
            var r = await _db.Randevular.AsNoTracking()
                .Where(x => x.Id == randevuId && x.MusteriKullaniciId == uid && x.Durum == "Tamamlandi")
                .Select(x => new { x.Id, x.IsletmeId, IsletmeAdi = x.Isletme.Ad, HizmetAdi = x.Hizmet.Ad })
                .FirstOrDefaultAsync();
            if (r == null) return NotFound();
            if (await _db.MusteriYorumlari.AnyAsync(x => x.RandevuId == r.Id))
            { TempData["YorumHata"] = "Bu randevu için zaten yorum yapılmış."; return RedirectToAction(nameof(Index)); }
            return View(new MusteriYorumYazModel { RandevuId = r.Id, IsletmeAdi = r.IsletmeAdi, HizmetAdi = r.HizmetAdi });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Yaz(MusteriYorumYazModel model)
        {
            var uid = _users.GetUserId(User);
            var r = await _db.Randevular.AsNoTracking()
                .Where(x => x.Id == model.RandevuId && x.MusteriKullaniciId == uid && x.Durum == "Tamamlandi")
                .Select(x => new { x.Id, x.IsletmeId, IsletmeAdi = x.Isletme.Ad, HizmetAdi = x.Hizmet.Ad })
                .FirstOrDefaultAsync();
            if (r == null) return NotFound();
            model.IsletmeAdi = r.IsletmeAdi;
            model.HizmetAdi = r.HizmetAdi;
            if (string.IsNullOrWhiteSpace(model.Yorum)) ModelState.AddModelError(nameof(model.Yorum), "Yorum yazın.");
            if (!ModelState.IsValid) return View(model);
            if (await _db.MusteriYorumlari.AnyAsync(x => x.RandevuId == r.Id))
            { TempData["YorumHata"] = "Bu randevu için zaten yorum yapılmış."; return RedirectToAction(nameof(Index)); }
            _db.MusteriYorumlari.Add(new MusteriYorumu
            {
                RandevuId = r.Id,
                IsletmeId = r.IsletmeId,
                MusteriKullaniciId = uid!,
                Puan = model.Puan,
                Yorum = model.Yorum.Trim(),
                YayinlandiMi = true,
                OlusturmaTarihi = DateTime.UtcNow
            });
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                TempData["YorumHata"] = "Yorum kaydedilemedi veya bu randevu zaten değerlendirilmiş.";
                return RedirectToAction(nameof(Index));
            }
            TempData["YorumMesaj"] = "Değerlendirmeniz yayınlandı. Teşekkürler!";
            return RedirectToAction(nameof(Index));
        }
    }

    public class MusteriYorumYazModel
    {
        [Range(1, int.MaxValue)] public int RandevuId { get; set; }
        [Range(1, 5, ErrorMessage = "1 ile 5 arasında yıldız seçin.")] public int Puan { get; set; } = 5;
        [Required(ErrorMessage = "Yorum yazın."), StringLength(3000, MinimumLength = 3, ErrorMessage = "Yorum 3–3000 karakter olmalı.")]
        public string Yorum { get; set; } = "";
        public string IsletmeAdi { get; set; } = "";
        public string HizmetAdi { get; set; } = "";
    }
    public class MusteriYorumRandevuSatiri
    {
        public int Id { get; set; }
        public string IsletmeAdi { get; set; } = "";
        public string HizmetAdi { get; set; } = "";
        public DateTime Tarih { get; set; }
        public bool YorumYapildi { get; set; }
    }
}
