using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    // Ücretsiz test: kurallı metin yorumlama + gerçek veritabanı araması.
    // Bu sınıf OpenAI/LLM kullanmaz ve randevu oluşturmaz.
    public class AtlasAiController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        public AtlasAiController(AtlasRandevuDbContext db) => _db = db;

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Ara(string? metin)
        {
            metin = (metin ?? "").Trim();
            if (metin.Length < 2 || metin.Length > 160)
                return BadRequest(new { mesaj = "Lütfen 2-160 karakterlik bir mesaj yazın." });

            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var yazi = metin.ToLower(tr);
            var fiyatEslesme = Regex.Match(yazi,
                @"(?:(?:en fazla|en çok|maksimum|bütçem|butcem|altında|geçmesin|gecmesin)\s*[: ]*\s*(?<f>\d{1,6}(?:[.,]\d{1,2})?)|(?<f>\d{1,6}(?:[.,]\d{1,2})?)\s*(?:tl|₺)\s*(?:altı|altında|geçmesin|gecmesin))",
                RegexOptions.IgnoreCase);
            decimal? ustFiyat = null;
            if (fiyatEslesme.Success && decimal.TryParse(
                fiyatEslesme.Groups["f"].Value.Replace(',', '.'), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var tutar) && tutar > 0)
                ustFiyat = tutar;

            var subeYerleri = await _db.Subeler.AsNoTracking()
                .Where(s => s.Durum == "Aktif" && s.Isletme.Durum == "Onaylandi")
                .Select(s => new { s.Il, s.Ilce }).Distinct().ToListAsync();
            // Uzun yer adlarını önce kontrol et: 'Gaziosmanpaşa' gibi.
            var yer = subeYerleri.SelectMany(s => new[] { s.Il, s.Ilce })
                .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length >= 3)
                .Distinct().OrderByDescending(s => s.Length)
                .FirstOrDefault(s => yazi.Contains(s.ToLower(tr)));

            // Bu kelimeler doğal dil bağlaçlarıdır; arama terimi değildir.
            var duraklar = new[] { "bana", "ben", "bir", "için", "icin", "istiyorum", "arıyorum", "ariyorum",
                "bul", "bulur", "musun", "mısın", "misin", "müsait", "musait", "yarın", "yarin",
                "bugün", "bugun", "saat", "sonra", "önce", "ogle", "öğleden", "akşam", "aksam",
                "sabah", "tl", "lira", "bütçem", "butcem", "fazla", "maksimum", "geçmesin", "gecmesin",
                "altında", "altı", "en", "çok", "cok", "kadar", "yakın", "yakin", "nerede", "olan",
                "yapan", "yaptırmak", "yaptirmak", "randevu", "almak", "alabilir", "miyim", "bütçe" };
            var kelimeler = Regex.Matches(yazi, @"[\p{L}]+")
                .Select(m => m.Value).Where(s => s.Length >= 3 && !duraklar.Contains(s)
                    && (yer == null || !yer.ToLower(tr).Contains(s)))
                .Distinct().Take(5).ToArray();

            var sorgu = _db.Isletmeler.AsNoTracking()
                .Where(x => x.Durum == "Onaylandi" &&
                    _db.Subeler.Any(s => s.IsletmeId == x.Id && s.Durum == "Aktif"));

            if (yer != null)
            {
                var yerFiltre = yer;
                sorgu = sorgu.Where(x => _db.Subeler.Any(s => s.IsletmeId == x.Id &&
                    s.Durum == "Aktif" && (s.Il == yerFiltre || s.Ilce == yerFiltre)));
            }
            if (ustFiyat.HasValue)
            {
                var fiyatFiltre = ustFiyat.Value;
                sorgu = sorgu.Where(x => _db.Hizmetler.Any(h => h.IsletmeId == x.Id &&
                    h.AktifMi && h.Fiyat <= fiyatFiltre));
            }
            // Tüm kelimeleri AND ile zorlamak yerine en az bir anlamlı eşleşme ararız.
            if (kelimeler.Length > 0)
            {
                var k0 = kelimeler[0];
                sorgu = sorgu.Where(x => x.Ad.Contains(k0) || x.Sektor.Ad.Contains(k0) ||
                    _db.Hizmetler.Any(h => h.IsletmeId == x.Id && h.AktifMi && h.Ad.Contains(k0)));
            }

            var bulunanlar = await sorgu.OrderBy(x => x.Ad).Take(12)
                .Select(x => new
                {
                    id = x.Id,
                    ad = x.Ad,
                    sektor = x.Sektor.Ad,
                    il = _db.Subeler.Where(s => s.IsletmeId == x.Id && s.Durum == "Aktif" &&
                        (yer == null || s.Il == yer || s.Ilce == yer))
                        .OrderBy(s => s.Id).Select(s => s.Il).FirstOrDefault(),
                    ilce = _db.Subeler.Where(s => s.IsletmeId == x.Id && s.Durum == "Aktif" &&
                        (yer == null || s.Il == yer || s.Ilce == yer))
                        .OrderBy(s => s.Id).Select(s => s.Ilce).FirstOrDefault(),
                    fiyat = _db.Hizmetler.Where(h => h.IsletmeId == x.Id && h.AktifMi &&
                        (!ustFiyat.HasValue || h.Fiyat <= ustFiyat.Value))
                        .Min(h => (decimal?)h.Fiyat)
                }).ToListAsync();

            bool zamanIstegi = new[] { "yarın", "yarin", "bugün", "bugun", "saat", "hafta", "pazartesi",
                "salı", "sali", "çarşamba", "carsamba", "perşembe", "persembe", "cuma", "cumartesi", "pazar" }
                .Any(s => yazi.Contains(s));
            var ozet = (yer != null ? yer + " konumunda " : "") +
                (ustFiyat.HasValue ? ustFiyat.Value.ToString("N0", tr) + " TL bütçeye kadar " : "");
            var mesaj = bulunanlar.Count == 0
                ? "Belirttiğiniz koşullara uyan kayıtlı işletme bulamadım. Hizmet veya konum bilgisini değiştirerek tekrar deneyin."
                : $"{ozet}{bulunanlar.Count} işletme buldum. Ayrıntıları inceleyip randevu ekranına geçebilirsiniz.";
            if (zamanIstegi)
                mesaj += " Bu test sürümü tarih ve saat müsaitliğini henüz kontrol etmiyor; gerçek saatleri randevu ekranından görebilirsiniz.";
            return Json(new { mesaj, isletmeler = bulunanlar, testModu = true });
        }
    }
}
