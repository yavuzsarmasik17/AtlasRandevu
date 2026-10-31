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
    [Authorize]
    public class IsletmeYorumlarController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;
        public IsletmeYorumlarController(AtlasRandevuDbContext db, UserManager<IdentityUser> users)
        { _db = db; _users = users; }

        private async Task<int?> YetkiliIsletmeId()
        {
            var uid = _users.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return null;
            return await _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == uid && x.AktifMi && x.Rol == "IsletmeSahibi" && x.Isletme.Durum == "Onaylandi")
                .Select(x => (int?)x.IsletmeId).FirstOrDefaultAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? durum = null)
        {
            var id = await YetkiliIsletmeId();
            if (!id.HasValue) return Forbid();
            var temel = _db.MusteriYorumlari.AsNoTracking().Where(x => x.IsletmeId == id.Value);
            ViewBag.Toplam = await temel.CountAsync();
            ViewBag.Ortalama = await temel.Where(x => x.YayinlandiMi).AverageAsync(x => (double?)x.Puan);
            ViewBag.Cevapsiz = await temel.CountAsync(x => x.YayinlandiMi && (x.IsletmeCevabi == null || x.IsletmeCevabi == ""));
            ViewBag.Yayinda = await temel.CountAsync(x => x.YayinlandiMi);
            ViewBag.Durum = durum ?? "";
            if (durum == "cevapsiz") temel = temel.Where(x => x.YayinlandiMi && (x.IsletmeCevabi == null || x.IsletmeCevabi == ""));
            else if (durum == "cevapli") temel = temel.Where(x => x.IsletmeCevabi != null && x.IsletmeCevabi != "");
            else if (durum == "gizli") temel = temel.Where(x => !x.YayinlandiMi);
            var yorumlar = await temel.OrderByDescending(x => x.OlusturmaTarihi).Take(300)
                .Select(x => new IsletmeYorumSatiri
                {
                    Id = x.Id,
                    RandevuId = x.RandevuId,
                    Puan = x.Puan,
                    Yorum = x.Yorum,
                    Tarih = x.OlusturmaTarihi,
                    Cevap = x.IsletmeCevabi,
                    YayinlandiMi = x.YayinlandiMi,
                    HizmetAdi = x.Randevu.Hizmet.Ad,
                    MusteriAdi = _db.MusteriProfilleri.Where(p => p.KullaniciId == x.MusteriKullaniciId)
                        .Select(p => p.Ad + " " + p.Soyad).FirstOrDefault() ?? "Müşteri"
                }).ToListAsync();
            return View(yorumlar);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cevapla(int id, [Required, StringLength(2000)] string cevap)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();
            var yorum = await _db.MusteriYorumlari.FirstOrDefaultAsync(x => x.Id == id && x.IsletmeId == isletmeId.Value);
            if (yorum == null) return NotFound();
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(cevap))
            { TempData["YorumHata"] = "Cevap boş olamaz ve 2000 karakteri geçemez."; return RedirectToAction(nameof(Index)); }
            yorum.IsletmeCevabi = cevap.Trim();
            yorum.CevapTarihi = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["YorumMesaj"] = "İşletme yanıtı kaydedildi.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class IsletmeYorumSatiri
    {
        public int Id { get; set; }
        public int RandevuId { get; set; }
        public int Puan { get; set; }
        public string Yorum { get; set; } = "";
        public DateTime Tarih { get; set; }
        public string? Cevap { get; set; }
        public bool YayinlandiMi { get; set; }
        public string HizmetAdi { get; set; } = "";
        public string MusteriAdi { get; set; } = "";
    }
}
