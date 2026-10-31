using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize]
    public class IsletmePanelController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public IsletmePanelController(AtlasRandevuDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GunlukVeriler()
        {
            var uid = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null ||
                await _userManager.IsInRoleAsync(kullanici, "PlatformAdmin") ||
                await _userManager.IsInRoleAsync(kullanici, "Musteri")) return Forbid();

            var ids = _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == uid && x.AktifMi &&
                    x.Rol == "IsletmeSahibi" && x.Isletme.Durum == "Onaylandi")
                .Select(x => x.IsletmeId);
            var isletmeId = await ids.FirstOrDefaultAsync();
            if (isletmeId == 0) return Forbid();

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            var bugun = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            var yarin = bugun.AddDays(1);
            var satirlar = await _db.Randevular.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId &&
                    x.BaslangicTarihi >= bugun && x.BaslangicTarihi < yarin)
                .OrderBy(x => x.BaslangicTarihi)
                .Select(x => new GunlukRandevuSatiri
                {
                    Id = x.Id,
                    MusteriKullaniciId = x.MusteriKullaniciId,
                    HizmetAdi = x.Hizmet.Ad,
                    PersonelAdi = x.Personel != null ? x.Personel.AdSoyad : "Atanmamış",
                    BaslangicTarihi = x.BaslangicTarihi,
                    Durum = x.Durum,
                    GelisDogrulandiMi = x.GelisDogrulandiMi
                }).ToListAsync();

            var randevuIds = satirlar.Select(x => x.Id).ToList();
            if (randevuIds.Count > 0)
            {
                var hizmetler = await _db.RandevuHizmetleri.AsNoTracking()
                    .Where(x => randevuIds.Contains(x.RandevuId))
                    .Select(x => new { x.RandevuId, Ad = x.Hizmet.Ad }).ToListAsync();
                foreach (var s in satirlar)
                {
                    var adlar = hizmetler.Where(x => x.RandevuId == s.Id)
                        .Select(x => x.Ad).ToList();
                    if (adlar.Count > 0) s.HizmetAdi = string.Join(" + ", adlar);
                }
                var musteriIds = satirlar.Select(x => x.MusteriKullaniciId).Distinct().ToList();
                var profiller = await _db.MusteriProfilleri.AsNoTracking()
                    .Where(x => musteriIds.Contains(x.KullaniciId))
                    .Select(x => new { x.KullaniciId, x.Ad, x.Soyad }).ToListAsync();
                foreach (var s in satirlar)
                {
                    var profil = profiller.FirstOrDefault(x => x.KullaniciId == s.MusteriKullaniciId);
                    if (profil != null)
                    {
                        var ad = (profil.Ad + " " + profil.Soyad).Trim();
                        if (!string.IsNullOrWhiteSpace(ad)) s.MusteriAdi = ad;
                    }
                }
            }
            Response.Headers["Cache-Control"] = "no-store";
            return Json(new
            {
                tarih = bugun.ToString("dd.MM.yyyy"),
                toplam = satirlar.Count,
                gelen = satirlar.Count(x => x.GelisDogrulandiMi),
                beklenen = satirlar.Count(x =>
                    (x.Durum == "Onaylandi" || x.Durum == "OnayBekliyor") && !x.GelisDogrulandiMi),
                tamamlanan = satirlar.Count(x => x.Durum == "Tamamlandi"),
                randevular = satirlar.Select(x => new {
                    x.Id,
                    x.MusteriAdi,
                    x.HizmetAdi,
                    x.PersonelAdi,
                    x.Durum,
                    x.GelisDogrulandiMi,
                    saat = x.BaslangicTarihi.ToString("HH:mm")
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null ||
                await _userManager.IsInRoleAsync(kullanici, "PlatformAdmin") ||
                await _userManager.IsInRoleAsync(kullanici, "Musteri"))
                return Forbid();

            var isletmeKaydi = await _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == kullaniciId && x.AktifMi && x.Rol == "IsletmeSahibi")
                .Select(x => new
                {
                    x.IsletmeId,
                    IsletmeAdi = x.Isletme.Ad,
                    IsletmeDurumu = x.Isletme.Durum
                })
                .FirstOrDefaultAsync();

            if (isletmeKaydi == null) return Forbid();
            if (isletmeKaydi.IsletmeDurumu != "Onaylandi")
                return RedirectToAction("BasvuruDurumu", "Hesap");

            var isletmeId = isletmeKaydi.IsletmeId;
            ViewBag.IsletmeAdi = isletmeKaydi.IsletmeAdi;
            ViewBag.ToplamHizmet = await _db.Hizmetler.CountAsync(x => x.IsletmeId == isletmeId);
            ViewBag.BekleyenRandevu = await _db.Randevular.CountAsync(x => x.IsletmeId == isletmeId && x.Durum == "OnayBekliyor");
            ViewBag.OnayliRandevu = await _db.Randevular.CountAsync(x => x.IsletmeId == isletmeId && x.Durum == "Onaylandi");
            ViewBag.TamamlananRandevu = await _db.Randevular.CountAsync(x => x.IsletmeId == isletmeId && x.Durum == "Tamamlandi");
            ViewBag.ToplamYorum = await _db.MusteriYorumlari.CountAsync(x => x.IsletmeId == isletmeId && x.YayinlandiMi);

            // Randevu tarihleri uygulamada Türkiye yerel saati olarak tutuluyor.
            var saatDilimi = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            var bugun = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saatDilimi).Date;
            var yarin = bugun.AddDays(1);
            var bugunkuler = await _db.Randevular.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId &&
                    x.BaslangicTarihi >= bugun && x.BaslangicTarihi < yarin)
                .OrderBy(x => x.BaslangicTarihi)
                .Select(x => new GunlukRandevuSatiri
                {
                    Id = x.Id,
                    MusteriKullaniciId = x.MusteriKullaniciId,
                    HizmetAdi = x.Hizmet.Ad,
                    PersonelAdi = x.Personel != null ? x.Personel.AdSoyad : "Atanmamış",
                    BaslangicTarihi = x.BaslangicTarihi,
                    Durum = x.Durum,
                    GelisDogrulandiMi = x.GelisDogrulandiMi
                }).ToListAsync();

            var randevuIdleri = bugunkuler.Select(x => x.Id).ToList();
            if (randevuIdleri.Count > 0)
            {
                var hizmetAdlari = await _db.RandevuHizmetleri.AsNoTracking()
                    .Where(x => randevuIdleri.Contains(x.RandevuId))
                    .Select(x => new { x.RandevuId, Ad = x.Hizmet.Ad }).ToListAsync();
                foreach (var r in bugunkuler)
                {
                    var adlar = hizmetAdlari.Where(x => x.RandevuId == r.Id).Select(x => x.Ad).ToList();
                    if (adlar.Count > 0) r.HizmetAdi = string.Join(" + ", adlar);
                }

                var musteriIdleri = bugunkuler.Select(x => x.MusteriKullaniciId).Distinct().ToList();
                var profiller = await _db.MusteriProfilleri.AsNoTracking()
                    .Where(x => musteriIdleri.Contains(x.KullaniciId))
                    .Select(x => new { x.KullaniciId, x.Ad, x.Soyad }).ToListAsync();
                foreach (var r in bugunkuler)
                {
                    var profil = profiller.FirstOrDefault(x => x.KullaniciId == r.MusteriKullaniciId);
                    if (profil != null)
                    {
                        var ad = (profil.Ad + " " + profil.Soyad).Trim();
                        if (!string.IsNullOrWhiteSpace(ad)) r.MusteriAdi = ad;
                    }
                }
            }

            // İptal/red olan randevular toplam kayıt içinde görünür, beklenen sayılmaz.
            ViewBag.BugunToplam = bugunkuler.Count;
            ViewBag.BugunGelen = bugunkuler.Count(x => x.GelisDogrulandiMi);
            ViewBag.BugunBeklenen = bugunkuler.Count(x =>
                (x.Durum == "Onaylandi" || x.Durum == "OnayBekliyor") && !x.GelisDogrulandiMi);
            ViewBag.BugunTamamlanan = bugunkuler.Count(x => x.Durum == "Tamamlandi");
            ViewBag.BugunTarih = bugun;
            ViewBag.BugunRandevular = bugunkuler;

            var randevular = await _db.Randevular.AsNoTracking()
                .Include(x => x.Hizmet)
                .Where(x => x.IsletmeId == isletmeId)
                .OrderByDescending(x => x.BaslangicTarihi)
                .Take(10)
                .ToListAsync();

            return View(randevular);
        }
    }

    public class GunlukRandevuSatiri
    {
        public int Id { get; set; }
        public string MusteriKullaniciId { get; set; } = "";
        public string MusteriAdi { get; set; } = "Müşteri";
        public string HizmetAdi { get; set; } = "";
        public string PersonelAdi { get; set; } = "";
        public DateTime BaslangicTarihi { get; set; }
        public string Durum { get; set; } = "";
        public bool GelisDogrulandiMi { get; set; }
    }
}
