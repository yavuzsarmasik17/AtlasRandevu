using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "PlatformYoneticisi")]
    public class PlatformYorumlarController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        public PlatformYorumlarController(AtlasRandevuDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index(string? ara, string? durum, int? puan)
        {
            var query = from y in _db.MusteriYorumlari.AsNoTracking()
                        join i in _db.Isletmeler.AsNoTracking() on y.IsletmeId equals i.Id
                        join u in _db.Users.AsNoTracking() on y.MusteriKullaniciId equals u.Id
                        join p0 in _db.MusteriProfilleri.AsNoTracking() on y.MusteriKullaniciId equals p0.KullaniciId into profiles
                        from p in profiles.DefaultIfEmpty()
                        select new PlatformYorumSatiri
                        {
                            Id = y.Id,
                            IsletmeId = y.IsletmeId,
                            IsletmeAdi = i.Ad,
                            MusteriAdi = p == null ? "" : p.Ad + " " + p.Soyad,
                            MusteriEposta = u.Email ?? "",
                            Puan = y.Puan,
                            Yorum = y.Yorum,
                            Tarih = y.OlusturmaTarihi,
                            YayinlandiMi = y.YayinlandiMi,
                            IsletmeCevabi = y.IsletmeCevabi,
                            CevapTarihi = y.CevapTarihi
                        };

            ViewBag.Toplam = await query.CountAsync();
            ViewBag.Yayinda = await query.CountAsync(x => x.YayinlandiMi);
            ViewBag.Gizli = await query.CountAsync(x => !x.YayinlandiMi);
            ViewBag.Ortalama = await query.AnyAsync() ? await query.AverageAsync(x => (double)x.Puan) : 0d;

            if (!string.IsNullOrWhiteSpace(ara))
            {
                var q = ara.Trim();
                query = query.Where(x => x.IsletmeAdi.Contains(q) || x.MusteriAdi.Contains(q) ||
                                         x.MusteriEposta.Contains(q) || (x.Yorum != null && x.Yorum.Contains(q)));
            }
            if (durum == "yayinda") query = query.Where(x => x.YayinlandiMi);
            if (durum == "gizli") query = query.Where(x => !x.YayinlandiMi);
            if (puan.HasValue && puan.Value >= 1 && puan.Value <= 5)
                query = query.Where(x => x.Puan == puan.Value);

            ViewBag.Ara = ara ?? "";
            ViewBag.Durum = durum ?? "";
            ViewBag.Puan = puan;
            var liste = await query.OrderByDescending(x => x.Tarih).ThenByDescending(x => x.Id)
                .Take(300).ToListAsync();
            return View(liste);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YayinDurumuDegistir(int id, bool yayinla)
        {
            var yorum = await _db.MusteriYorumlari.FirstOrDefaultAsync(x => x.Id == id);
            if (yorum == null) return NotFound();
            if (yayinla)
            {
                var tamamlandi = await _db.Randevular.AnyAsync(x => x.Id == yorum.RandevuId &&
                    x.MusteriKullaniciId == yorum.MusteriKullaniciId &&
                    x.IsletmeId == yorum.IsletmeId && x.Durum == "Tamamlandi");
                if (!tamamlandi)
                {
                    TempData["Hata"] = "Tamamlanmış randevusu doğrulanamayan yorum yayınlanamaz.";
                    return RedirectToAction(nameof(Index));
                }
            }
            yorum.YayinlandiMi = yayinla;
            await _db.SaveChangesAsync();
            TempData["Basari"] = yayinla ? "Yorum yeniden yayınlandı." : "Yorum yayından kaldırıldı.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class PlatformYorumSatiri
    {
        public int Id { get; set; }
        public int IsletmeId { get; set; }
        public string IsletmeAdi { get; set; } = "";
        public string MusteriAdi { get; set; } = "";
        public string MusteriEposta { get; set; } = "";
        public int Puan { get; set; }
        public string? Yorum { get; set; }
        public DateTime Tarih { get; set; }
        public bool YayinlandiMi { get; set; }
        public string? IsletmeCevabi { get; set; }
        public DateTime? CevapTarihi { get; set; }
    }
}
