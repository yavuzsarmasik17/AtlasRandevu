using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    public class KesfetController : Controller
    {
        private readonly AtlasRandevuDbContext _db;

        public KesfetController(AtlasRandevuDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? arama, int? sektorId, string? il, string? ilce,
            string? hizmet, decimal? minFiyat, decimal? maxFiyat,
            decimal? minPuan, int? minYorum, bool yorumlu = false,
            bool fotografli = false, string siralama = "onerilen")
        {
            arama = arama?.Trim();
            il = il?.Trim();
            ilce = ilce?.Trim();
            hizmet = hizmet?.Trim();

            var sorgu = _db.Isletmeler.AsNoTracking()
                .Where(x => x.Durum == "Onaylandi");

            if (!string.IsNullOrWhiteSpace(arama))
                sorgu = sorgu.Where(x => x.Ad.Contains(arama) ||
                    _db.Hizmetler.Any(h => h.IsletmeId == x.Id && h.AktifMi && h.Ad.Contains(arama)));

            if (sektorId.HasValue)
                sorgu = sorgu.Where(x => x.SektorId == sektorId.Value);

            if (!string.IsNullOrWhiteSpace(il))
                sorgu = sorgu.Where(x => _db.Subeler.Any(s =>
                    s.IsletmeId == x.Id && s.Il == il && s.Durum == "Aktif"));

            if (!string.IsNullOrWhiteSpace(ilce))
                sorgu = sorgu.Where(x => _db.Subeler.Any(s =>
                    s.IsletmeId == x.Id && s.Ilce == ilce && s.Durum == "Aktif" &&
                    (string.IsNullOrEmpty(il) || s.Il == il)));

            if (!string.IsNullOrWhiteSpace(hizmet))
                sorgu = sorgu.Where(x => _db.Hizmetler.Any(h =>
                    h.IsletmeId == x.Id && h.AktifMi && h.Ad.Contains(hizmet)));

            if (minFiyat.HasValue || maxFiyat.HasValue)
                sorgu = sorgu.Where(x => _db.Hizmetler.Any(h =>
                    h.IsletmeId == x.Id && h.AktifMi &&
                    (!minFiyat.HasValue || h.Fiyat >= minFiyat.Value) &&
                    (!maxFiyat.HasValue || h.Fiyat <= maxFiyat.Value)));

            if (fotografli)
                sorgu = sorgu.Where(x =>
                    _db.IsletmeFotograflari.Any(f => f.IsletmeId == x.Id) ||
                    x.KapakFotografiYolu != null);

            if (yorumlu || minYorum.HasValue)
            {
                var gerekliYorum = Math.Max(yorumlu ? 1 : 0, minYorum ?? 0);
                sorgu = sorgu.Where(x => _db.MusteriYorumlari.Count(y =>
                    y.IsletmeId == x.Id && y.YayinlandiMi) >= gerekliYorum);
            }

            if (minPuan.HasValue)
                sorgu = sorgu.Where(x =>
                    _db.MusteriYorumlari.Any(y => y.IsletmeId == x.Id && y.YayinlandiMi) &&
                    _db.MusteriYorumlari.Where(y => y.IsletmeId == x.Id && y.YayinlandiMi)
                        .Average(y => (decimal)y.Puan) >= minPuan.Value);

            var sonuclar = await sorgu.Select(x => new KesfetIsletmeViewModel
            {
                Id = x.Id,
                Ad = x.Ad,
                SektorAdi = x.Sektor.Ad,
                Slogan = x.Slogan,
                LogoYolu = x.LogoYolu,
                KapakFotografiYolu = x.KapakFotografiYolu,
                Il = _db.Subeler.Where(s => s.IsletmeId == x.Id && s.Durum == "Aktif" &&
                        (string.IsNullOrEmpty(il) || s.Il == il) &&
                        (string.IsNullOrEmpty(ilce) || s.Ilce == ilce))
                    .OrderBy(s => s.Id).Select(s => s.Il).FirstOrDefault(),
                Ilce = _db.Subeler.Where(s => s.IsletmeId == x.Id && s.Durum == "Aktif" &&
                        (string.IsNullOrEmpty(il) || s.Il == il) &&
                        (string.IsNullOrEmpty(ilce) || s.Ilce == ilce))
                    .OrderBy(s => s.Id).Select(s => s.Ilce).FirstOrDefault(),
                YorumSayisi = _db.MusteriYorumlari.Count(y => y.IsletmeId == x.Id && y.YayinlandiMi),
                OrtalamaPuan = _db.MusteriYorumlari.Where(y => y.IsletmeId == x.Id && y.YayinlandiMi)
                    .Average(y => (decimal?)y.Puan),
                BaslangicFiyati = _db.Hizmetler.Where(h => h.IsletmeId == x.Id && h.AktifMi)
                    .Min(h => (decimal?)h.Fiyat)
            }).ToListAsync();

            sonuclar = siralama switch
            {
                "puan" => sonuclar.OrderByDescending(x => x.OrtalamaPuan.HasValue)
                    .ThenByDescending(x => x.OrtalamaPuan).ThenByDescending(x => x.YorumSayisi).ToList(),
                "yorum" => sonuclar.OrderByDescending(x => x.YorumSayisi).ToList(),
                "fiyat" => sonuclar.OrderBy(x => x.BaslangicFiyati ?? decimal.MaxValue).ToList(),
                "isim" => sonuclar.OrderBy(x => x.Ad).ToList(),
                _ => sonuclar.OrderByDescending(x => x.YorumSayisi > 0)
                    .ThenByDescending(x => x.OrtalamaPuan).ThenByDescending(x => x.YorumSayisi)
                    .ThenBy(x => x.Ad).ToList()
            };

            var aktifSubeler = _db.Subeler.AsNoTracking()
                .Where(s => s.Durum == "Aktif" && s.Isletme.Durum == "Onaylandi");

            ViewBag.Iller = await aktifSubeler.Where(s => s.Il != "")
                .Select(s => s.Il).Distinct().OrderBy(x => x).ToListAsync();
            ViewBag.Ilceler = await aktifSubeler.Where(s => s.Ilce != "" &&
                    (string.IsNullOrEmpty(il) || s.Il == il))
                .Select(s => s.Ilce).Distinct().OrderBy(x => x).ToListAsync();
            ViewBag.Sektorler = await _db.Sektorler.AsNoTracking()
                .Where(x => x.AktifMi).OrderBy(x => x.Sira).ToListAsync();

            ViewBag.Arama = arama;
            ViewBag.SektorId = sektorId;
            ViewBag.Il = il;
            ViewBag.Ilce = ilce;
            ViewBag.Hizmet = hizmet;
            ViewBag.MinFiyat = minFiyat;
            ViewBag.MaxFiyat = maxFiyat;
            ViewBag.MinPuan = minPuan;
            ViewBag.MinYorum = minYorum;
            ViewBag.Yorumlu = yorumlu;
            ViewBag.Fotografli = fotografli;
            ViewBag.Siralama = siralama;
            return View(sonuclar);
        }

        [HttpGet]
        public async Task<IActionResult> Detay(int id)
        {
            var isletme = await _db.Isletmeler.AsNoTracking()
                .Where(x => x.Id == id && x.Durum == "Onaylandi")
                .Select(x => new KesfetDetayViewModel
                {
                    Id = x.Id,
                    Ad = x.Ad,
                    SektorAdi = x.Sektor.Ad,
                    Slogan = x.Slogan,
                    Hakkimizda = x.Hakkimizda,
                    LogoYolu = x.LogoYolu,
                    KapakFotografiYolu = x.KapakFotografiYolu,
                    WebSitesi = x.WebSitesi,
                    InstagramAdresi = x.InstagramAdresi
                }).FirstOrDefaultAsync();

            if (isletme == null)
                return NotFound();

            isletme.Hizmetler = await _db.Hizmetler.AsNoTracking()
                .Where(x => x.IsletmeId == id && x.AktifMi)
                .OrderBy(x => x.Ad).ToListAsync();
            isletme.Subeler = await _db.Subeler.AsNoTracking()
                .Where(x => x.IsletmeId == id && x.Durum == "Aktif")
                .OrderBy(x => x.Id).ToListAsync();
            isletme.Fotograflar = await _db.IsletmeFotograflari.AsNoTracking()
                .Where(x => x.IsletmeId == id)
                .OrderBy(x => x.Sira).ThenBy(x => x.Id).ToListAsync();
            isletme.Yorumlar = await _db.MusteriYorumlari.AsNoTracking()
                .Where(x => x.IsletmeId == id && x.YayinlandiMi)
                .OrderByDescending(x => x.OlusturmaTarihi).ToListAsync();
            isletme.YorumSayisi = isletme.Yorumlar.Count;
            isletme.OrtalamaPuan = isletme.Yorumlar.Count == 0
                ? null : isletme.Yorumlar.Average(x => (decimal)x.Puan);

            return View(isletme);
        }
    }

    public class KesfetIsletmeViewModel
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
        public string SektorAdi { get; set; } = "";
        public string? Slogan { get; set; }
        public string? LogoYolu { get; set; }
        public string? KapakFotografiYolu { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public int YorumSayisi { get; set; }
        public decimal? OrtalamaPuan { get; set; }
        public decimal? BaslangicFiyati { get; set; }
    }

    public class KesfetDetayViewModel
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
        public string SektorAdi { get; set; } = "";
        public string? Slogan { get; set; }
        public string? Hakkimizda { get; set; }
        public string? LogoYolu { get; set; }
        public string? KapakFotografiYolu { get; set; }
        public string? WebSitesi { get; set; }
        public string? InstagramAdresi { get; set; }
        public int YorumSayisi { get; set; }
        public decimal? OrtalamaPuan { get; set; }
        public List<Hizmet> Hizmetler { get; set; } = new();
        public List<Sube> Subeler { get; set; } = new();
        public List<IsletmeFotografi> Fotograflar { get; set; } = new();
        public List<MusteriYorumu> Yorumlar { get; set; } = new();
    }
}
