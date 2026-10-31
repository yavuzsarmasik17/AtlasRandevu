using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRandevu.Web.Controllers;

[ApiController]
[Route("api/mobil/kesfet")]
[AllowAnonymous]
public class MobilKesfetController : ControllerBase
{
    private readonly AtlasRandevuDbContext _db;
    public MobilKesfetController(AtlasRandevuDbContext db) => _db = db;

    [HttpGet("filtreler")]
    public async Task<IActionResult> Filtreler(string? il = null)
    {
        il = il?.Trim();
        var subeler = _db.Subeler.AsNoTracking()
            .Where(s => s.Durum == "Aktif" && s.Isletme.Durum == "Onaylandi");
        return Ok(new
        {
            sektorler = await _db.Sektorler.AsNoTracking().Where(s => s.AktifMi)
                .OrderBy(s => s.Sira).Select(s => new { s.Id, s.Ad }).ToListAsync(),
            iller = await subeler.Where(s => s.Il != "").Select(s => s.Il)
                .Distinct().OrderBy(x => x).ToListAsync(),
            ilceler = await subeler.Where(s => s.Ilce != "" && (il == null || il == "" || s.Il == il))
                .Select(s => s.Ilce).Distinct().OrderBy(x => x).ToListAsync()
        });
    }

    [HttpGet("isletmeler")]
    public async Task<IActionResult> Isletmeler(
        string? arama, int? sektorId, string? il, string? ilce,
        string? hizmet, decimal? minFiyat, decimal? maxFiyat,
        decimal? minPuan, int? minYorum, bool yorumlu = false,
        bool fotografli = false, string siralama = "onerilen")
    {
        arama = arama?.Trim(); il = il?.Trim(); ilce = ilce?.Trim(); hizmet = hizmet?.Trim();
        var q = _db.Isletmeler.AsNoTracking().Where(x => x.Durum == "Onaylandi");
        if (!string.IsNullOrWhiteSpace(arama))
            q = q.Where(x => x.Ad.Contains(arama) ||
                _db.Hizmetler.Any(h => h.IsletmeId == x.Id && h.AktifMi && h.Ad.Contains(arama)));
        if (sektorId.HasValue) q = q.Where(x => x.SektorId == sektorId.Value);
        if (!string.IsNullOrWhiteSpace(il))
            q = q.Where(x => _db.Subeler.Any(s => s.IsletmeId == x.Id && s.Il == il && s.Durum == "Aktif"));
        if (!string.IsNullOrWhiteSpace(ilce))
            q = q.Where(x => _db.Subeler.Any(s => s.IsletmeId == x.Id && s.Ilce == ilce &&
                s.Durum == "Aktif" && (string.IsNullOrEmpty(il) || s.Il == il)));
        if (!string.IsNullOrWhiteSpace(hizmet))
            q = q.Where(x => _db.Hizmetler.Any(h => h.IsletmeId == x.Id && h.AktifMi && h.Ad.Contains(hizmet)));
        if (minFiyat.HasValue || maxFiyat.HasValue)
            q = q.Where(x => _db.Hizmetler.Any(h => h.IsletmeId == x.Id && h.AktifMi &&
                (!minFiyat.HasValue || h.Fiyat >= minFiyat.Value) &&
                (!maxFiyat.HasValue || h.Fiyat <= maxFiyat.Value)));
        if (fotografli)
            q = q.Where(x => _db.IsletmeFotograflari.Any(f => f.IsletmeId == x.Id) || x.KapakFotografiYolu != null);
        if (yorumlu || minYorum.HasValue)
        {
            var gerekli = Math.Max(yorumlu ? 1 : 0, minYorum ?? 0);
            q = q.Where(x => _db.MusteriYorumlari.Count(y => y.IsletmeId == x.Id && y.YayinlandiMi) >= gerekli);
        }
        if (minPuan.HasValue)
            q = q.Where(x => _db.MusteriYorumlari.Any(y => y.IsletmeId == x.Id && y.YayinlandiMi) &&
                _db.MusteriYorumlari.Where(y => y.IsletmeId == x.Id && y.YayinlandiMi)
                    .Average(y => (decimal)y.Puan) >= minPuan.Value);

        var list = await q.Select(x => new
        {
            x.Id,
            x.Ad,
            SektorAdi = x.Sektor.Ad,
            x.Slogan,
            x.LogoYolu,
            x.KapakFotografiYolu,
            Il = _db.Subeler.Where(s => s.IsletmeId == x.Id && s.Durum == "Aktif" &&
                    (string.IsNullOrEmpty(il) || s.Il == il) && (string.IsNullOrEmpty(ilce) || s.Ilce == ilce))
                .OrderBy(s => s.Id).Select(s => s.Il).FirstOrDefault(),
            Ilce = _db.Subeler.Where(s => s.IsletmeId == x.Id && s.Durum == "Aktif" &&
                    (string.IsNullOrEmpty(il) || s.Il == il) && (string.IsNullOrEmpty(ilce) || s.Ilce == ilce))
                .OrderBy(s => s.Id).Select(s => s.Ilce).FirstOrDefault(),
            YorumSayisi = _db.MusteriYorumlari.Count(y => y.IsletmeId == x.Id && y.YayinlandiMi),
            OrtalamaPuan = _db.MusteriYorumlari.Where(y => y.IsletmeId == x.Id && y.YayinlandiMi)
                .Average(y => (decimal?)y.Puan),
            BaslangicFiyati = _db.Hizmetler.Where(h => h.IsletmeId == x.Id && h.AktifMi)
                .Min(h => (decimal?)h.Fiyat)
        }).ToListAsync();

        var ordered = siralama switch
        {
            "puan" => list.OrderByDescending(x => x.OrtalamaPuan.HasValue).ThenByDescending(x => x.OrtalamaPuan).ThenByDescending(x => x.YorumSayisi),
            "yorum" => list.OrderByDescending(x => x.YorumSayisi),
            "fiyat" => list.OrderBy(x => x.BaslangicFiyati ?? decimal.MaxValue),
            "isim" => list.OrderBy(x => x.Ad),
            _ => list.OrderByDescending(x => x.YorumSayisi > 0).ThenByDescending(x => x.OrtalamaPuan).ThenByDescending(x => x.YorumSayisi).ThenBy(x => x.Ad)
        };
        return Ok(ordered);
    }

    [HttpGet("isletmeler/{id:int}")]
    public async Task<IActionResult> Detay(int id)
    {
        var isletme = await _db.Isletmeler.AsNoTracking()
            .Where(x => x.Id == id && x.Durum == "Onaylandi")
            .Select(x => new {
                x.Id,
                x.Ad,
                SektorAdi = x.Sektor.Ad,
                x.Slogan,
                x.Hakkimizda,
                x.LogoYolu,
                x.KapakFotografiYolu,
                x.WebSitesi,
                x.InstagramAdresi
            })
            .FirstOrDefaultAsync();
        if (isletme == null) return NotFound();
        return Ok(new
        {
            isletme,
            hizmetler = await _db.Hizmetler.AsNoTracking().Where(x => x.IsletmeId == id && x.AktifMi)
                .OrderBy(x => x.Ad).Select(x => new { x.Id, x.Ad, x.Fiyat }).ToListAsync(),
            subeler = await _db.Subeler.AsNoTracking().Where(x => x.IsletmeId == id && x.Durum == "Aktif")
                .OrderBy(x => x.Id).Select(x => new { x.Id, x.Il, x.Ilce }).ToListAsync(),
            fotograflar = await _db.IsletmeFotograflari.AsNoTracking().Where(x => x.IsletmeId == id)
                .OrderBy(x => x.Sira).ThenBy(x => x.Id).Select(x => new { x.Id }).ToListAsync(),
            yorumlar = await _db.MusteriYorumlari.AsNoTracking().Where(x => x.IsletmeId == id && x.YayinlandiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .Select(x => new { x.Puan, x.OlusturmaTarihi }).ToListAsync()
        });
    }
}
