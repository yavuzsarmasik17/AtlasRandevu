using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "PlatformYoneticisi")]
    public class PlatformRandevularController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        public PlatformRandevularController(AtlasRandevuDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index(string? ara, string? durum)
        {
            var query = from r in _db.Randevular.AsNoTracking()
                        join i in _db.Isletmeler.AsNoTracking() on r.IsletmeId equals i.Id
                        join h in _db.Hizmetler.AsNoTracking() on r.HizmetId equals h.Id
                        join s in _db.Subeler.AsNoTracking() on r.SubeId equals s.Id
                        join u in _db.Users.AsNoTracking() on r.MusteriKullaniciId equals u.Id
                        join p in _db.MusteriProfilleri.AsNoTracking()
                            on r.MusteriKullaniciId equals p.KullaniciId into profilGrubu
                        from p in profilGrubu.DefaultIfEmpty()
                        select new PlatformRandevuSatiri
                        {
                            Id = r.Id,
                            IsletmeAdi = i.Ad,
                            SubeAdi = s.Ad,
                            HizmetAdi = h.Ad,
                            MusteriAdi = p == null ? "" : p.Ad,
                            MusteriSoyadi = p == null ? "" : p.Soyad,
                            MusteriEposta = u.Email ?? "",
                            MusteriTelefon = u.PhoneNumber ?? "",
                            BaslangicTarihi = r.BaslangicTarihi,
                            BitisTarihi = r.BitisTarihi,
                            Durum = r.Durum
                        };

            ViewBag.ToplamRandevu = await query.CountAsync();
            ViewBag.BekleyenRandevu = await query.CountAsync(x => x.Durum == "OnayBekliyor");
            ViewBag.OnayliRandevu = await query.CountAsync(x => x.Durum == "Onaylandi");
            ViewBag.TamamlananRandevu = await query.CountAsync(x => x.Durum == "Tamamlandi");

            if (!string.IsNullOrWhiteSpace(durum))
                query = query.Where(x => x.Durum == durum);

            if (!string.IsNullOrWhiteSpace(ara))
            {
                var metin = ara.Trim();
                query = query.Where(x =>
                    x.IsletmeAdi.Contains(metin) ||
                    x.SubeAdi.Contains(metin) ||
                    x.HizmetAdi.Contains(metin) ||
                    x.MusteriAdi.Contains(metin) ||
                    x.MusteriSoyadi.Contains(metin) ||
                    x.MusteriEposta.Contains(metin) ||
                    x.MusteriTelefon.Contains(metin));
            }

            ViewBag.Ara = ara ?? "";
            ViewBag.Durum = durum ?? "";
            var liste = await query.OrderByDescending(x => x.BaslangicTarihi)
                .ThenByDescending(x => x.Id).Take(500).ToListAsync();
            return View(liste);
        }
    }

    public class PlatformRandevuSatiri
    {
        public int Id { get; set; }
        public string IsletmeAdi { get; set; } = "";
        public string SubeAdi { get; set; } = "";
        public string HizmetAdi { get; set; } = "";
        public string MusteriAdi { get; set; } = "";
        public string MusteriSoyadi { get; set; } = "";
        public string MusteriEposta { get; set; } = "";
        public string MusteriTelefon { get; set; } = "";
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
        public string Durum { get; set; } = "";
    }
}
