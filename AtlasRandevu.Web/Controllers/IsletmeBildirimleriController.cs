using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "OnayliIsletmeSahibi")]
    [Route("isletme-bildirimleri")]
    public class IsletmeBildirimleriController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public IsletmeBildirimleriController(AtlasRandevuDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        private IQueryable<int> YetkiliIsletmeIdleri(string kullaniciId) =>
            _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == kullaniciId && x.AktifMi &&
                    x.Rol == "IsletmeSahibi" && x.Isletme.Durum == "Onaylandi")
                .Select(x => x.IsletmeId);

        // Mevcut bildirim zili API'si değişmedi.
        [HttpGet("")]
        public async Task<IActionResult> Liste()
        {
            var uid = _users.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
            var ids = YetkiliIsletmeIdleri(uid);
            var kayitlar = await _db.IsletmeBildirimleri.AsNoTracking()
                .Where(x => ids.Contains(x.IsletmeId))
                .OrderByDescending(x => x.OlusturulmaTarihi).ThenByDescending(x => x.Id)
                .Take(30)
                .Select(x => new {
                    x.Id,
                    x.IsletmeId,
                    x.Tur,
                    x.Baslik,
                    x.Mesaj,
                    x.OlusturulmaTarihi,
                    x.OkunduMu,
                    x.RandevuId
                }).ToListAsync();
            var okunmamis = await _db.IsletmeBildirimleri.AsNoTracking()
                .CountAsync(x => ids.Contains(x.IsletmeId) && !x.OkunduMu);
            return Json(new { bildirimler = kayitlar, okunmamis });
        }

        [HttpGet("gecmis")]
        public async Task<IActionResult> Gecmis(string? tur, string? durum,
            DateTime? baslangic, DateTime? bitis, int sayfa = 1)
        {
            var uid = _users.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
            if (sayfa < 1) sayfa = 1;
            if (sayfa > 1000000) sayfa = 1000000;

            // Formdaki tarihler Türkiye yerel takvim günüdür; DB zamanı UTC'dir.
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            DateTime? utcBaslangic = null, utcBitis = null;
            if (baslangic.HasValue)
                utcBaslangic = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(baslangic.Value.Date, DateTimeKind.Unspecified), tz);
            if (bitis.HasValue && bitis.Value.Date < DateTime.MaxValue.Date)
                utcBitis = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(bitis.Value.Date.AddDays(1), DateTimeKind.Unspecified), tz);
            if (baslangic.HasValue && bitis.HasValue && baslangic.Value.Date > bitis.Value.Date)
                return BadRequest("Başlangıç tarihi bitiş tarihinden sonra olamaz.");

            var ids = YetkiliIsletmeIdleri(uid);
            var sorgu = _db.IsletmeBildirimleri.AsNoTracking()
                .Where(x => ids.Contains(x.IsletmeId));
            if (tur == "YeniRandevu" || tur == "MusteriGeldi")
                sorgu = sorgu.Where(x => x.Tur == tur);
            else tur = null;
            if (durum == "okunmadi") sorgu = sorgu.Where(x => !x.OkunduMu);
            else if (durum == "okundu") sorgu = sorgu.Where(x => x.OkunduMu);
            else durum = null;
            if (utcBaslangic.HasValue) sorgu = sorgu.Where(x => x.OlusturulmaTarihi >= utcBaslangic.Value);
            if (utcBitis.HasValue) sorgu = sorgu.Where(x => x.OlusturulmaTarihi < utcBitis.Value);

            const int sayfaBoyutu = 20;
            var toplam = await sorgu.CountAsync();
            var toplamSayfa = Math.Max(1, (int)Math.Ceiling(toplam / (double)sayfaBoyutu));
            sayfa = Math.Min(sayfa, toplamSayfa);
            var kayitlar = await sorgu.OrderByDescending(x => x.OlusturulmaTarihi)
                .ThenByDescending(x => x.Id)
                .Skip((sayfa - 1) * sayfaBoyutu).Take(sayfaBoyutu)
                .Select(x => new BildirimGecmisiSatir
                {
                    Id = x.Id,
                    Baslik = x.Baslik,
                    Mesaj = x.Mesaj,
                    Tur = x.Tur,
                    OkunduMu = x.OkunduMu,
                    OlusturulmaTarihi = x.OlusturulmaTarihi,
                    RandevuId = x.RandevuId
                }).ToListAsync();

            return View(new BildirimGecmisiModel
            {
                Kayitlar = kayitlar,
                Tur = tur,
                Durum = durum,
                Baslangic = baslangic?.Date,
                Bitis = bitis?.Date,
                Sayfa = sayfa,
                ToplamSayfa = toplamSayfa,
                ToplamKayit = toplam
            });
        }

        [HttpPost("oku/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Oku(int id)
        {
            var uid = _users.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
            var ids = YetkiliIsletmeIdleri(uid);
            var etkilenen = await _db.IsletmeBildirimleri
                .Where(x => x.Id == id && ids.Contains(x.IsletmeId) && !x.OkunduMu)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.OkunduMu, true)
                    .SetProperty(x => x.OkunmaTarihi, DateTime.UtcNow));
            return Json(new { basarili = true, degisti = etkilenen > 0 });
        }

        [HttpPost("tumunu-oku")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TumunuOku()
        {
            var uid = _users.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
            var ids = YetkiliIsletmeIdleri(uid);
            await _db.IsletmeBildirimleri
                .Where(x => ids.Contains(x.IsletmeId) && !x.OkunduMu)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.OkunduMu, true)
                    .SetProperty(x => x.OkunmaTarihi, DateTime.UtcNow));
            return Json(new { basarili = true });
        }
    }

    public class BildirimGecmisiModel
    {
        public System.Collections.Generic.List<BildirimGecmisiSatir> Kayitlar { get; set; } = new();
        public string? Tur { get; set; }
        public string? Durum { get; set; }
        public DateTime? Baslangic { get; set; }
        public DateTime? Bitis { get; set; }
        public int Sayfa { get; set; }
        public int ToplamSayfa { get; set; }
        public int ToplamKayit { get; set; }
    }
    public class BildirimGecmisiSatir
    {
        public int Id { get; set; }
        public string Tur { get; set; } = "";
        public string Baslik { get; set; } = "";
        public string Mesaj { get; set; } = "";
        public DateTime OlusturulmaTarihi { get; set; }
        public bool OkunduMu { get; set; }
        public int? RandevuId { get; set; }
    }
}
