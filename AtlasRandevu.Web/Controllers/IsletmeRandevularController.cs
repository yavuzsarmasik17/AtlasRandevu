using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using AtlasRandevu.Web.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize]
    public class IsletmeRandevularController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHubContext<IsletmeBildirimHub> _hub;

        public IsletmeRandevularController(
            AtlasRandevuDbContext db,
            UserManager<IdentityUser> userManager,
            IHubContext<IsletmeBildirimHub> hub)
        {
            _db = db;
            _userManager = userManager;
            _hub = hub;
        }

        private async Task<int?> YetkiliIsletmeId()
        {
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return null;
            return await _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == kullaniciId && x.AktifMi &&
                            x.Rol == "IsletmeSahibi" && x.Isletme.Durum == "Onaylandi")
                .Select(x => (int?)x.IsletmeId).FirstOrDefaultAsync();
        }

        // Yalnızca uygulama içindeki güvenli adreslere geri dön.
        private IActionResult IslemSonrasiYonlendir(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl) &&
                (returnUrl.StartsWith("/IsletmeTakvim", StringComparison.OrdinalIgnoreCase) ||
                 returnUrl.StartsWith("/IsletmeRandevular", StringComparison.OrdinalIgnoreCase)))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? durum = null, string? ara = null,
            string gorunum = "tum", DateTime? tarih = null)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();

            string[] izinliDurumlar = { "OnayBekliyor", "Onaylandi", "Geldi", "Gelmedi", "Reddedildi", "IptalEdildi", "Tamamlandi" };
            var sorgu = _db.Randevular.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value);

            var durumSayilari = await sorgu.GroupBy(x => x.Durum)
                .Select(x => new { Durum = x.Key, Sayi = x.Count() }).ToListAsync();
            ViewBag.Toplam = durumSayilari.Sum(x => x.Sayi);
            ViewBag.Bekleyen = durumSayilari.Where(x => x.Durum == "OnayBekliyor").Sum(x => x.Sayi);
            ViewBag.Onaylanan = durumSayilari.Where(x => x.Durum == "Onaylandi").Sum(x => x.Sayi);
            ViewBag.Gelen = durumSayilari.Where(x => x.Durum == "Geldi").Sum(x => x.Sayi);
            ViewBag.Gelmeyen = durumSayilari.Where(x => x.Durum == "Gelmedi").Sum(x => x.Sayi);
            ViewBag.Tamamlanan = durumSayilari.Where(x => x.Durum == "Tamamlandi").Sum(x => x.Sayi);

            if (!string.IsNullOrWhiteSpace(durum) && izinliDurumlar.Contains(durum))
                sorgu = sorgu.Where(x => x.Durum == durum);

            var seciliTarih = (tarih ?? DateTime.Today).Date;
            if (gorunum == "gun")
            {
                var bitis = seciliTarih.AddDays(1);
                sorgu = sorgu.Where(x => x.BaslangicTarihi >= seciliTarih && x.BaslangicTarihi < bitis);
            }
            else if (gorunum == "hafta")
            {
                var fark = ((int)seciliTarih.DayOfWeek + 6) % 7;
                var haftaBaslangic = seciliTarih.AddDays(-fark);
                var haftaBitis = haftaBaslangic.AddDays(7);
                sorgu = sorgu.Where(x => x.BaslangicTarihi >= haftaBaslangic && x.BaslangicTarihi < haftaBitis);
            }
            else gorunum = "tum";

            if (!string.IsNullOrWhiteSpace(ara))
            {
                var kelime = ara.Trim();
                var profilIdleri = _db.MusteriProfilleri.AsNoTracking()
                    .Where(p => (p.Ad + " " + p.Soyad).Contains(kelime))
                    .Select(p => p.KullaniciId);
                var kullaniciIdleri = _db.Users.AsNoTracking()
                    .Where(u => (u.Email != null && u.Email.Contains(kelime)) ||
                                (u.PhoneNumber != null && u.PhoneNumber.Contains(kelime)))
                    .Select(u => u.Id);

                sorgu = sorgu.Where(x =>
                    x.Hizmet.Ad.Contains(kelime) ||
                    x.Sube.Ad.Contains(kelime) ||
                    (x.MusteriKullaniciId != null &&
                        (profilIdleri.Contains(x.MusteriKullaniciId) ||
                         kullaniciIdleri.Contains(x.MusteriKullaniciId))) ||
                    (x.ManuelMusteriAdi != null &&
                        x.ManuelMusteriAdi.Contains(kelime)) ||
                    (x.ManuelMusteriTelefonu != null &&
                        x.ManuelMusteriTelefonu.Contains(kelime)));

            }

            var randevular = await sorgu.Include(x => x.Hizmet).Include(x => x.Sube)
                .Include(x => x.Personel)
                .OrderByDescending(x => x.BaslangicTarihi).Take(300).ToListAsync();
            var ids = randevular
    .Where(x => !string.IsNullOrEmpty(x.MusteriKullaniciId))
    .Select(x => x.MusteriKullaniciId!)
    .Distinct()
    .ToList();
            var profiller = await _db.MusteriProfilleri.AsNoTracking()
                .Where(x => ids.Contains(x.KullaniciId)).ToDictionaryAsync(x => x.KullaniciId);
            var kullanicilar = await _db.Users.AsNoTracking()
                .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

            var randevuIdleri = randevular.Select(x => x.Id).ToList();
            var hizmetler = await _db.RandevuHizmetleri.AsNoTracking()
                .Where(x => randevuIdleri.Contains(x.RandevuId))
                .Select(x => new { x.RandevuId, Ad = x.Hizmet.Ad })
                .ToListAsync();
            var hizmetAdlari = hizmetler.GroupBy(x => x.RandevuId)
                .ToDictionary(x => x.Key, x => string.Join(" + ", x.Select(h => h.Ad)));

            var liste = randevular.Select(x =>
            {

                MusteriProfili? profil = null;
                IdentityUser? kullanici = null;

                if (!string.IsNullOrWhiteSpace(x.MusteriKullaniciId))
                {
                    profiller.TryGetValue(x.MusteriKullaniciId, out profil);
                    kullanicilar.TryGetValue(x.MusteriKullaniciId, out kullanici);
                }

                var adSoyad = profil == null
                    ? ""
                    : (profil.Ad + " " + profil.Soyad).Trim();

                var manuelMi = x.RandevuKaynagi == "Telefon" ||
                               x.RandevuKaynagi == "Elden";

                return new IsletmeRandevuSatiri
                {
                    Id = x.Id,

                    MusteriAdi = manuelMi
    ? (x.ManuelMusteriAdi ?? "Manuel müşteri")
    : (string.IsNullOrWhiteSpace(adSoyad)
        ? "İsim belirtilmemiş"
        : adSoyad),

                    Eposta = manuelMi ? "" : (kullanici?.Email ?? ""),

                    Telefon = manuelMi
    ? (x.ManuelMusteriTelefonu ?? "")
    : (kullanici?.PhoneNumber ?? ""),

                    HizmetAdi = hizmetAdlari.TryGetValue(x.Id, out var adlar) ? adlar : (x.Hizmet?.Ad ?? "—"),
                    SubeAdi = x.Sube?.Ad ?? "—",
                    PersonelAdi = x.Personel?.AdSoyad ?? "—",
                    BaslangicTarihi = x.BaslangicTarihi,
                    BitisTarihi = x.BitisTarihi,
                    Durum = x.Durum,
                    MusteriNotu = x.MusteriNotu ?? "",
                    GelisTarihi = x.GelisTarihi,
                    GelisYontemi = x.GelisYontemi,
                    GelisDogrulandiMi = x.GelisDogrulandiMi,
                    RandevuKaynagi = x.RandevuKaynagi
                };
            }).ToList();

            ViewBag.SeciliDurum = durum ?? "";
            ViewBag.Ara = ara ?? "";
            ViewBag.Gorunum = gorunum;
            ViewBag.Tarih = seciliTarih.ToString("yyyy-MM-dd");
            return View(liste);
        }

        // Eski sayfalardan gelen onaylama ve ret istekleri artık durum değiştirmez.
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Onayla(int id)
        {
            TempData["RandevuHata"] = "Randevular artık otomatik onaylanıyor.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Reddet(int id)
        {
            TempData["RandevuHata"] = "Randevu onay/red işlemi kaldırıldı.";
            return RedirectToAction(nameof(Index));
        }

        // Müşteri gelmedi: sadece onaylı, giriş doğrulaması olmayan ve bitişi geçen randevular.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Gelmedi(int id, string? returnUrl = null)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();

            var simdi = DateTime.Now;
            var etkilenen = await _db.Randevular
                .Where(x => x.Id == id && x.IsletmeId == isletmeId.Value &&
                            x.Durum == "Onaylandi" && !x.GelisDogrulandiMi &&
                            x.BitisTarihi <= simdi)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Durum, "Gelmedi"));

            if (etkilenen == 0)
            {
                TempData["RandevuHata"] = "Gelmedi işlemi yalnızca bitiş saati geçmiş ve giriş yapmamış onaylı randevular için kullanılabilir.";
                return IslemSonrasiYonlendir(returnUrl);
            }

            try
            {
                await _hub.Clients
                    .Group(IsletmeBildirimHub.GrupAdi(isletmeId.Value))
                    .SendAsync("RandevuGelmedi", new { randevuId = id });
            }
            catch (Exception)
            {
                // Bildirim başarısız olsa bile veritabanındaki durum korunur.
            }

            TempData["RandevuMesaj"] = "Müşteri gelmedi olarak işaretlendi.";
            return IslemSonrasiYonlendir(returnUrl);
        }


        // Manuel randevularda işletme tarafından müşteri girişi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManuelGeldi(int id, string? returnUrl = null)
        {
            var isletmeId = await YetkiliIsletmeId();

            if (!isletmeId.HasValue)
                return Forbid();

            var simdi = DateTime.Now;

            var etkilenen = await _db.Randevular
                .Where(x =>
                    x.Id == id &&
                    x.IsletmeId == isletmeId.Value &&
                    (x.RandevuKaynagi == "Telefon" ||
                     x.RandevuKaynagi == "Elden") &&
                    x.MusteriKullaniciId == null &&
                    x.Durum == "Onaylandi" &&
                    x.BaslangicTarihi <= simdi &&
                    x.BitisTarihi >= simdi)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Durum, "Geldi")
                    .SetProperty(x => x.GelisTarihi, DateTime.UtcNow)
                    .SetProperty(x => x.GelisYontemi, "Manuel")
                    .SetProperty(x => x.GelisDogrulandiMi, true));

            if (etkilenen == 0)
            {
                TempData["RandevuHata"] =
                    "Bu manuel randevu için müşteri girişi yapılamıyor.";

                return IslemSonrasiYonlendir(returnUrl);
            }

            try
            {
                await _hub.Clients
                    .Group(IsletmeBildirimHub.GrupAdi(isletmeId.Value))
                    .SendAsync("RandevuGeldi", new { randevuId = id });
            }
            catch (Exception)
            {
                // Bildirim hatası randevu işlemini geri almaz.
            }

            TempData["RandevuMesaj"] =
                "Manuel randevu müşterisi geldi olarak işaretlendi.";

            return IslemSonrasiYonlendir(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManuelIptal(int id, string? returnUrl = null)
        {
            var isletmeId = await YetkiliIsletmeId();

            if (!isletmeId.HasValue)
                return Forbid();

            var etkilenen = await _db.Randevular
                .Where(x =>
                    x.Id == id &&
                    x.IsletmeId == isletmeId.Value &&
                    (x.RandevuKaynagi == "Telefon" ||
                     x.RandevuKaynagi == "Elden") &&
                    x.MusteriKullaniciId == null &&
                    x.Durum == "Onaylandi" &&
                    x.BaslangicTarihi > DateTime.Now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Durum, "IptalEdildi"));

            if (etkilenen == 0)
            {
                TempData["RandevuHata"] =
                    "Bu randevu iptal edilemiyor. Yalnızca henüz başlamamış, onaylı manuel randevular iptal edilebilir.";

                return IslemSonrasiYonlendir(returnUrl);
            }

            try
            {
                await _hub.Clients
                    .Group(IsletmeBildirimHub.GrupAdi(isletmeId.Value))
                    .SendAsync("RandevuIptalEdildi", new { randevuId = id });
            }
            catch (Exception)
            {
                // Bildirim hatası iptal işlemini geri almaz.
            }

            TempData["RandevuMesaj"] = "Manuel randevu iptal edildi.";

            return IslemSonrasiYonlendir(returnUrl);
        }


        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Tamamla(int id, string? returnUrl = null)
        {


            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();

            var simdi = DateTime.Now;
            // İşlem sadece QR/kod ile giriş yapmış ve randevu bitiş saati geçmiş
            // müşteriler için tamamlanabilir. Koşullu güncelleme yarışları önler.
            var etkilenen = await _db.Randevular
                .Where(x => x.Id == id && x.IsletmeId == isletmeId.Value &&
                            x.Durum == "Geldi" && x.GelisDogrulandiMi &&
                            x.BitisTarihi <= simdi)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Durum, "Tamamlandi")
                    .SetProperty(x => x.TamamlanmaTarihi, DateTime.UtcNow));

            if (etkilenen == 0)
            {
                TempData["RandevuHata"] = "Tamamlamak için müşterinin giriş yapmış olması ve randevu bitiş saatinin gelmesi gerekir.";
                return IslemSonrasiYonlendir(returnUrl);
            }

            // Veritabanı güncellemesi başarılı. SignalR ile sadece bu işletmenin
            // bağlı ekranlarına yenileme sinyali gönderilir.
            try
            {
                await _hub.Clients
                    .Group(IsletmeBildirimHub.GrupAdi(isletmeId.Value))
                    .SendAsync("RandevuTamamlandi", new { randevuId = id });
            }
            catch (Exception)
            {
                // Bildirim gönderilemese bile tamamlanan işlem geri alınmaz.
            }

            TempData["RandevuMesaj"] = "İşlem tamamlandı.";
            return IslemSonrasiYonlendir(returnUrl);
        }
    }

    public class IsletmeRandevuSatiri
    {
        public int Id { get; set; }
        public string MusteriAdi { get; set; } = "";
        public string Eposta { get; set; } = "";
        public string Telefon { get; set; } = "";
        public string HizmetAdi { get; set; } = "";
        public string SubeAdi { get; set; } = "";
        public string PersonelAdi { get; set; } = "";
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
        public string Durum { get; set; } = "";
        public string MusteriNotu { get; set; } = "";
        public DateTime? GelisTarihi { get; set; }
        public string? GelisYontemi { get; set; }
        public bool GelisDogrulandiMi { get; set; }
        public string RandevuKaynagi { get; set; } = "Online";
    }
}
