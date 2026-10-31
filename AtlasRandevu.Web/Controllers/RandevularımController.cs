using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using AtlasRandevu.Web.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Roles = "Musteri")]
    public class RandevularimController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHubContext<IsletmeBildirimHub> _hub;
        private readonly ILogger<RandevularimController> _logger;
        private const string OrtakQrDegeri = "ATLAS-RANDEVU-ORTAK-GIRIS-V1";
        private const string OrtakGirisKodu = "ATLAS2026";

        public RandevularimController(AtlasRandevuDbContext db,
            UserManager<IdentityUser> userManager,
            IHubContext<IsletmeBildirimHub> hub,
            ILogger<RandevularimController> logger)
        {
            _db = db;
            _userManager = userManager;
            _hub = hub;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();

            var kayitlar = await _db.Randevular.AsNoTracking()
                .Where(r => r.MusteriKullaniciId == kullaniciId)
                .OrderByDescending(r => r.BaslangicTarihi)
                .Select(r => new RandevularimSatirViewModel
                {
                    Id = r.Id,
                    IsletmeAdi = r.Isletme.Ad,
                    SubeAdi = r.Sube.Ad,
                    HizmetAdi = r.Hizmet.Ad,
                    PersonelAdi = r.Personel != null ? r.Personel.AdSoyad : null,
                    BaslangicTarihi = r.BaslangicTarihi,
                    BitisTarihi = r.BitisTarihi,
                    Durum = r.Durum,
                    IsletmeAciklamasi = r.IsletmeAciklamasi
                }).ToListAsync();

            var randevuIdleri = kayitlar.Select(x => x.Id).ToList();
            if (randevuIdleri.Count > 0)
            {
                var hizmetler = await _db.RandevuHizmetleri.AsNoTracking()
                    .Where(x => randevuIdleri.Contains(x.RandevuId))
                    .Select(x => new { x.RandevuId, Ad = x.Hizmet.Ad, x.Fiyat, x.SureDakika })
                    .ToListAsync();
                foreach (var r in kayitlar)
                {
                    var secilenler = hizmetler.Where(x => x.RandevuId == r.Id).ToList();
                    if (secilenler.Count == 0) continue;
                    r.HizmetAdi = string.Join(" + ", secilenler.Select(x => x.Ad));
                    r.ToplamFiyat = secilenler.Sum(x => x.Fiyat);
                    r.ToplamSure = secilenler.Sum(x => x.SureDakika);
                }
            }
            return View(kayitlar);
        }

        // Müşteri yalnızca başlamamış, onaylı ve giriş yapılmamış randevusunu iptal edebilir.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IptalEt(int id)
        {
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();

            var simdi = TurkiyeSimdi();
            int isletmeId;
            int bildirimId;

            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var etkilenen = await _db.Randevular
                    .Where(r => r.Id == id && r.MusteriKullaniciId == kullaniciId
                        && r.Durum == "Onaylandi" && !r.GelisDogrulandiMi
                        && r.BaslangicTarihi > simdi)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(r => r.Durum, "IptalEdildi"));

                if (etkilenen == 0)
                {
                    await transaction.RollbackAsync();
                    TempData["RandevuHata"] = "Yalnızca başlamamış ve giriş yapılmamış onaylı randevular iptal edilebilir.";
                    return RedirectToAction(nameof(Index));
                }

                var bilgi = await _db.Randevular.AsNoTracking()
                    .Where(r => r.Id == id && r.MusteriKullaniciId == kullaniciId)
                    .Select(r => new
                    {
                        r.IsletmeId,
                        r.BaslangicTarihi,
                        HizmetAdi = r.Hizmet.Ad,
                        SubeAdi = r.Sube.Ad
                    }).SingleAsync();

                var profil = await _db.MusteriProfilleri.AsNoTracking()
                    .Where(p => p.KullaniciId == kullaniciId)
                    .Select(p => new { p.Ad, p.Soyad }).FirstOrDefaultAsync();
                var musteriAdi = profil == null ? "Müşteri" : (profil.Ad + " " + profil.Soyad).Trim();
                if (string.IsNullOrWhiteSpace(musteriAdi)) musteriAdi = "Müşteri";

                isletmeId = bilgi.IsletmeId;
                var bildirim = new IsletmeBildirimi
                {
                    IsletmeId = isletmeId,
                    RandevuId = id,
                    Tur = "RandevuIptalEdildi",
                    Baslik = "Müşteri randevusunu iptal etti",
                    Mesaj = $"{musteriAdi} — {bilgi.HizmetAdi} ({bilgi.SubeAdi}), {bilgi.BaslangicTarihi:dd.MM.yyyy HH:mm} randevusunu iptal etti.",
                    OlusturulmaTarihi = DateTime.UtcNow,
                    OkunduMu = false
                };
                _db.IsletmeBildirimleri.Add(bildirim);
                await _db.SaveChangesAsync();
                bildirimId = bildirim.Id;
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Randevu {RandevuId} iptal edilemedi.", id);
                TempData["RandevuHata"] = "Randevu iptal edilemedi. Lütfen tekrar deneyin.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _hub.Clients.Group($"isletme-{isletmeId}")
                    .SendAsync("RandevuIptalEdildi", new { bildirimId, randevuId = id, durum = "IptalEdildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Randevu {RandevuId} iptal canlı bildirimi gönderilemedi.", id);
            }

            TempData["RandevuMesaj"] = "Randevunuz iptal edildi.";
            return RedirectToAction(nameof(Index));
        }

        // Müşteri yalnızca kendi, başlamamış ve giriş yapılmamış onaylı randevusunu erteleyebilir.
        [HttpGet]
        public async Task<IActionResult> Ertele(int id)
        {
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();
            var r = await _db.Randevular.AsNoTracking()
                .Where(x => x.Id == id && x.MusteriKullaniciId == kullaniciId)
                .Select(x => new {
                    x.Id,
                    x.IsletmeId,
                    x.SubeId,
                    x.PersonelId,
                    x.BaslangicTarihi,
                    x.BitisTarihi,
                    x.Durum,
                    x.GelisDogrulandiMi,
                    IsletmeAdi = x.Isletme.Ad,
                    SubeAdi = x.Sube.Ad,
                    PersonelAdi = x.Personel != null ? x.Personel.AdSoyad : "Atanmamış"
                })
                .FirstOrDefaultAsync();
            if (r == null) return NotFound();
            if (r.Durum != "Onaylandi" || r.GelisDogrulandiMi || r.BaslangicTarihi <= TurkiyeSimdi())
            {
                TempData["RandevuHata"] = "Bu randevu artık ertelenemez.";
                return RedirectToAction(nameof(Index));
            }
            var hizmetler = await _db.RandevuHizmetleri.AsNoTracking()
                .Where(x => x.RandevuId == id)
                .Select(x => new { x.Hizmet.Ad, x.SureDakika }).ToListAsync();
            return View(new RandevuErteleViewModel
            {
                Id = r.Id,
                Tarih = r.BaslangicTarihi.Date,
                Saat = r.BaslangicTarihi.TimeOfDay,
                EskiBaslangic = r.BaslangicTarihi,
                IsletmeAdi = r.IsletmeAdi,
                SubeAdi = r.SubeAdi,
                PersonelAdi = r.PersonelAdi,
                HizmetAdi = string.Join(" + ", hizmetler.Select(x => x.Ad)),
                ToplamSure = hizmetler.Count > 0 ? hizmetler.Sum(x => x.SureDakika) :
                    (int)(r.BitisTarihi - r.BaslangicTarihi).TotalMinutes
            });
        }

        [HttpGet]
        public async Task<IActionResult> ErteleMusaitSaatler(int id, DateTime tarih)
        {
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();
            var bugun = TurkiyeSimdi().Date;
            if (tarih.Date != tarih || tarih < bugun || tarih > bugun.AddMonths(3))
                return BadRequest(new { mesaj = "Bugünden itibaren en fazla 3 ay seçilebilir." });
            var r = await _db.Randevular.AsNoTracking()
                .Where(x => x.Id == id && x.MusteriKullaniciId == kullaniciId)
                .Select(x => new {
                    x.Id,
                    x.IsletmeId,
                    x.SubeId,
                    x.PersonelId,
                    x.Durum,
                    x.GelisDogrulandiMi,
                    x.BaslangicTarihi,
                    x.BitisTarihi
                }).FirstOrDefaultAsync();
            if (r == null) return NotFound();
            if (r.Durum != "Onaylandi" || r.GelisDogrulandiMi || r.BaslangicTarihi <= TurkiyeSimdi())
                return BadRequest(new { mesaj = "Bu randevu ertelenemez." });
            var sureler = await _db.RandevuHizmetleri.AsNoTracking()
                .Where(x => x.RandevuId == id).Select(x => x.SureDakika).ToListAsync();
            var sure = sureler.Count > 0 ? sureler.Sum(x => (long)x) :
                (long)(r.BitisTarihi - r.BaslangicTarihi).TotalMinutes;
            if (sure <= 0 || sure > 1440) return BadRequest(new { mesaj = "Hizmet süresi geçersiz." });
            var aktif = await _db.Isletmeler.AsNoTracking().AnyAsync(x => x.Id == r.IsletmeId && x.Durum == "Onaylandi")
                && await _db.Subeler.AsNoTracking().AnyAsync(x => x.Id == r.SubeId && x.IsletmeId == r.IsletmeId && x.Durum == "Aktif")
                && (r.PersonelId == null || await _db.Personeller.AsNoTracking().AnyAsync(x => x.Id == r.PersonelId && x.SubeId == r.SubeId && x.IsletmeId == r.IsletmeId && x.AktifMi));
            if (!aktif) return BadRequest(new { mesaj = "İşletme, şube veya personel artık aktif değil." });
            var calisma = await _db.SubeCalismaSaatleri.AsNoTracking()
                .FirstOrDefaultAsync(x => x.SubeId == r.SubeId && x.Gun == tarih.DayOfWeek);
            if (calisma == null || !calisma.AcikMi)
                return Json(new { saatler = Array.Empty<object>(), mesaj = "Şube bu gün kapalı." });
            var gunSonu = tarih.AddDays(1);
            var dolu = await _db.Randevular.AsNoTracking()
                .Where(x => x.Id != id && x.SubeId == r.SubeId
                    && (r.PersonelId == null || x.PersonelId == r.PersonelId || x.PersonelId == null)
                    && (x.Durum == "OnayBekliyor" || x.Durum == "Onaylandi" || x.Durum == "Geldi")
                    && x.BaslangicTarihi < gunSonu && x.BitisTarihi > tarih)
                .Select(x => new { x.BaslangicTarihi, x.BitisTarihi }).ToListAsync();
            var saatler = new List<object>();
            var acilis = tarih.Add(calisma.Acilis);
            var kapanis = tarih.Add(calisma.Kapanis);
            for (var bas = acilis; bas.AddMinutes(sure) <= kapanis && bas.AddMinutes(sure).Date == tarih.Date;
                bas = bas.AddMinutes(15))
            {
                var bit = bas.AddMinutes(sure);
                saatler.Add(new
                {
                    saat = bas.ToString("HH:mm"),
                    uygun = bas > TurkiyeSimdi() && !dolu.Any(x => x.BaslangicTarihi < bit && x.BitisTarihi > bas)
                });
            }
            return Json(new { saatler });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ertele(RandevuErteleViewModel model)
        {
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();
            var simdi = TurkiyeSimdi();
            if (model.Tarih.Date != model.Tarih || model.Tarih < simdi.Date ||
                model.Tarih > simdi.Date.AddMonths(3) || model.Saat < TimeSpan.Zero ||
                model.Saat >= TimeSpan.FromDays(1) || model.Saat.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
            {
                TempData["RandevuHata"] = "Geçerli bir tarih ve 15 dakikalık saat seçin.";
                return RedirectToAction(nameof(Ertele), new { id = model.Id });
            }
            var yeniBas = model.Tarih.Add(model.Saat);
            if (yeniBas <= simdi)
            {
                TempData["RandevuHata"] = "Gelecekteki bir saat seçin.";
                return RedirectToAction(nameof(Ertele), new { id = model.Id });
            }
            int isletmeId = 0, bildirimId = 0;
            DateTime eskiBas = default;
            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var r = await _db.Randevular.FirstOrDefaultAsync(x => x.Id == model.Id && x.MusteriKullaniciId == kullaniciId);
                if (r == null) return NotFound();
                if (r.Durum != "Onaylandi" || r.GelisDogrulandiMi || r.BaslangicTarihi <= TurkiyeSimdi())
                {
                    TempData["RandevuHata"] = "Bu randevu artık ertelenemez.";
                    return RedirectToAction(nameof(Index));
                }
                var sureler = await _db.RandevuHizmetleri.AsNoTracking()
                    .Where(x => x.RandevuId == r.Id).Select(x => x.SureDakika).ToListAsync();
                var sure = sureler.Count > 0 ? sureler.Sum(x => (long)x) :
                    (long)(r.BitisTarihi - r.BaslangicTarihi).TotalMinutes;
                if (sure <= 0 || sure > 1440) throw new InvalidOperationException("Hizmet süresi geçersiz.");
                var yeniBit = yeniBas.AddMinutes(sure);
                var aktif = await _db.Isletmeler.AnyAsync(x => x.Id == r.IsletmeId && x.Durum == "Onaylandi")
                    && await _db.Subeler.AnyAsync(x => x.Id == r.SubeId && x.IsletmeId == r.IsletmeId && x.Durum == "Aktif")
                    && (r.PersonelId == null || await _db.Personeller.AnyAsync(x => x.Id == r.PersonelId && x.SubeId == r.SubeId && x.IsletmeId == r.IsletmeId && x.AktifMi));
                var calisma = await _db.SubeCalismaSaatleri.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SubeId == r.SubeId && x.Gun == yeniBas.DayOfWeek);
                if (!aktif || calisma == null || !calisma.AcikMi ||
                    yeniBas.TimeOfDay < calisma.Acilis || yeniBit.Date != yeniBas.Date || yeniBit.TimeOfDay > calisma.Kapanis ||
                    (yeniBas - model.Tarih.Add(calisma.Acilis)).Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
                {
                    TempData["RandevuHata"] = "Seçilen saat şubenin çalışma saatlerine uygun değil.";
                    return RedirectToAction(nameof(Ertele), new { id = model.Id });
                }
                if (yeniBas == r.BaslangicTarihi)
                {
                    TempData["RandevuHata"] = "Yeni tarih ve saat mevcut randevuyla aynı.";
                    return RedirectToAction(nameof(Ertele), new { id = model.Id });
                }
                var cakisma = await _db.Randevular.AnyAsync(x => x.Id != r.Id && x.SubeId == r.SubeId
                    && (r.PersonelId == null || x.PersonelId == r.PersonelId || x.PersonelId == null)
                    && (x.Durum == "OnayBekliyor" || x.Durum == "Onaylandi" || x.Durum == "Geldi")
                    && x.BaslangicTarihi < yeniBit && x.BitisTarihi > yeniBas);
                if (cakisma)
                {
                    TempData["RandevuHata"] = "Bu saat artık dolu. Başka bir saat seçin.";
                    return RedirectToAction(nameof(Ertele), new { id = model.Id });
                }
                eskiBas = r.BaslangicTarihi;
                isletmeId = r.IsletmeId;
                r.BaslangicTarihi = yeniBas;
                r.BitisTarihi = yeniBit;
                var profil = await _db.MusteriProfilleri.AsNoTracking()
                    .Where(x => x.KullaniciId == kullaniciId)
                    .Select(x => new { x.Ad, x.Soyad }).FirstOrDefaultAsync();
                var ad = profil == null ? "Müşteri" : (profil.Ad + " " + profil.Soyad).Trim();
                if (string.IsNullOrWhiteSpace(ad)) ad = "Müşteri";
                var bildirim = new IsletmeBildirimi
                {
                    IsletmeId = isletmeId,
                    RandevuId = r.Id,
                    Tur = "RandevuErtelendi",
                    Baslik = "Müşteri randevusunu erteledi",
                    Mesaj = $"{ad} randevusunu {eskiBas:dd.MM.yyyy HH:mm} tarihinden {yeniBas:dd.MM.yyyy HH:mm} tarihine erteledi.",
                    OlusturulmaTarihi = DateTime.UtcNow,
                    OkunduMu = false
                };
                _db.IsletmeBildirimleri.Add(bildirim);
                await _db.SaveChangesAsync();
                bildirimId = bildirim.Id;
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Randevu {RandevuId} ertelenemedi.", model.Id);
                TempData["RandevuHata"] = "Randevu ertelenemedi. Saat dolmuş olabilir, tekrar deneyin.";
                return RedirectToAction(nameof(Ertele), new { id = model.Id });
            }
            try
            {
                await _hub.Clients.Group(IsletmeBildirimHub.GrupAdi(isletmeId))
                    .SendAsync("RandevuErtelendi", new
                    {
                        bildirimId,
                        randevuId = model.Id,
                        eskiTarih = eskiBas.ToString("dd.MM.yyyy HH:mm"),
                        yeniTarih = yeniBas.ToString("dd.MM.yyyy HH:mm"),
                        durum = "Onaylandi"
                    });
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Erteleme canlı bildirimi gönderilemedi."); }
            TempData["RandevuMesaj"] = "Randevunuz başarıyla ertelendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult QrOkut() => View();

        private static bool GecerliKod(string? metin)
        {
            var kod = metin?.Trim();
            return kod == OrtakQrDegeri ||
                string.Equals(kod, OrtakGirisKodu, StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime TurkiyeSimdi()
        {
            var saatDilimi = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saatDilimi);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QrDogrula([FromForm] string qrMetni)
        {
            if (!GecerliKod(qrMetni))
            {
                TempData["QrHata"] = "Geçersiz QR kodu veya giriş kodu.";
                return RedirectToAction(nameof(QrOkut));
            }

            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();

            var simdi = TurkiyeSimdi();
            var uygunlar = await _db.Randevular.AsNoTracking()
                .Where(r => r.MusteriKullaniciId == kullaniciId && r.Durum == "Onaylandi"
                    && !r.GelisDogrulandiMi
                    && r.BaslangicTarihi >= simdi.AddMinutes(-15)
                    && r.BaslangicTarihi <= simdi.AddMinutes(30))
                .Select(r => new QrRandevuSecimViewModel
                {
                    Id = r.Id,
                    IsletmeAdi = r.Isletme.Ad,
                    SubeAdi = r.Sube.Ad,
                    BaslangicTarihi = r.BaslangicTarihi
                }).OrderBy(r => r.BaslangicTarihi).ToListAsync();

            if (uygunlar.Count == 0)
            {
                TempData["QrHata"] = "Şu anda giriş yapılabilecek onaylı randevunuz bulunmuyor.";
                return RedirectToAction(nameof(QrOkut));
            }
            if (uygunlar.Count > 1)
            {
                ViewBag.QrMetni = qrMetni.Trim();
                return View("QrRandevuSec", uygunlar);
            }
            return await GirisYap(uygunlar[0].Id, kullaniciId, simdi, qrMetni);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QrRandevuSec(int randevuId, string qrMetni)
        {
            if (!GecerliKod(qrMetni)) return BadRequest("Geçersiz QR kodu veya giriş kodu.");
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();
            return await GirisYap(randevuId, kullaniciId, TurkiyeSimdi(), qrMetni);
        }

        private async Task<IActionResult> GirisYap(int randevuId, string kullaniciId,
            DateTime simdi, string qrMetni)
        {
            var girisYontemi = string.Equals(qrMetni.Trim(), OrtakQrDegeri, StringComparison.Ordinal)
                ? "OrtakQR" : "ManuelKod";

            // Randevu durumu ve kalıcı bildirim aynı transaction içinde kaydedilir.
            // Böylece güncelleme başarılı, bildirim kaydı başarısız gibi yarım işlemler oluşmaz.
            int isletmeId;
            string musteriAdi;
            string hizmetAdi;
            string subeAdi;
            DateTime randevuBaslangici;
            int bildirimId;

            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                var etkilenen = await _db.Randevular
                    .Where(r => r.Id == randevuId
                        && r.MusteriKullaniciId == kullaniciId
                        && r.Durum == "Onaylandi"
                        && !r.GelisDogrulandiMi
                        && r.BaslangicTarihi >= simdi.AddMinutes(-15)
                        && r.BaslangicTarihi <= simdi.AddMinutes(30))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(r => r.Durum, "Geldi")
                        .SetProperty(r => r.GelisDogrulandiMi, true)
                        .SetProperty(r => r.GelisYontemi, girisYontemi)
                        .SetProperty(r => r.GelisTarihi, simdi));

                if (etkilenen == 0)
                {
                    await transaction.RollbackAsync();
                    TempData["QrHata"] = "Giriş yapılamadı. Randevu durumu veya giriş zamanı değişmiş olabilir.";
                    return RedirectToAction(nameof(QrOkut));
                }

                var bilgi = await _db.Randevular.AsNoTracking()
                    .Where(r => r.Id == randevuId && r.MusteriKullaniciId == kullaniciId)
                    .Select(r => new
                    {
                        r.IsletmeId,
                        r.BaslangicTarihi,
                        HizmetAdi = r.Hizmet.Ad,
                        SubeAdi = r.Sube.Ad
                    }).SingleAsync();

                var profil = await _db.MusteriProfilleri.AsNoTracking()
                    .Where(p => p.KullaniciId == kullaniciId)
                    .Select(p => new { p.Ad, p.Soyad })
                    .FirstOrDefaultAsync();

                musteriAdi = profil == null ? "Müşteri" : (profil.Ad + " " + profil.Soyad).Trim();
                if (string.IsNullOrWhiteSpace(musteriAdi)) musteriAdi = "Müşteri";
                isletmeId = bilgi.IsletmeId;
                hizmetAdi = bilgi.HizmetAdi;
                subeAdi = bilgi.SubeAdi;
                randevuBaslangici = bilgi.BaslangicTarihi;

                var bildirim = new IsletmeBildirimi
                {
                    IsletmeId = isletmeId,
                    RandevuId = randevuId,
                    Tur = "MusteriGeldi",
                    Baslik = "Müşteri işletmeye geldi",
                    Mesaj = $"{musteriAdi} — {hizmetAdi} ({subeAdi}), randevu saati: {randevuBaslangici:dd.MM.yyyy HH:mm}. Giriş: {(girisYontemi == "OrtakQR" ? "QR" : "Manuel kod")}.",
                    OlusturulmaTarihi = DateTime.UtcNow,
                    OkunduMu = false
                };
                _db.IsletmeBildirimleri.Add(bildirim);
                await _db.SaveChangesAsync();
                bildirimId = bildirim.Id;
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Randevu {RandevuId} giriş/bildirim kaydı başarısız.", randevuId);
                TempData["QrHata"] = "Giriş kaydedilemedi. Lütfen tekrar deneyin.";
                return RedirectToAction(nameof(QrOkut));
            }

            // Canlı bildirim yalnızca commit sonrasında gönderilir.
            // SignalR hatası, başarılı müşteri girişini geri almaz.
            try
            {
                await _hub.Clients.Group($"isletme-{isletmeId}")
                    .SendAsync("MusteriGeldi", new
                    {
                        bildirimId,
                        randevuId,
                        musteriAdi,
                        hizmetAdi,
                        subeAdi,
                        randevuSaati = randevuBaslangici.ToString("HH:mm"),
                        gelisSaati = simdi.ToString("HH:mm"),
                        gelisYontemi = girisYontemi,
                        durum = "Geldi"
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Randevu {RandevuId} canlı bildirimi gönderilemedi.", randevuId);
            }

            TempData["QrBasari"] = "Randevunuza girişiniz kaydedildi. Durum: Geldi.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class RandevuErteleViewModel
    {
        [Range(1, int.MaxValue)] public int Id { get; set; }
        [DataType(DataType.Date)] public DateTime Tarih { get; set; }
        public TimeSpan Saat { get; set; }
        public DateTime EskiBaslangic { get; set; }
        public string IsletmeAdi { get; set; } = "";
        public string SubeAdi { get; set; } = "";
        public string PersonelAdi { get; set; } = "";
        public string HizmetAdi { get; set; } = "";
        public int ToplamSure { get; set; }
    }

    public class QrRandevuSecimViewModel
    {
        public int Id { get; set; }
        public string IsletmeAdi { get; set; } = "";
        public string SubeAdi { get; set; } = "";
        public DateTime BaslangicTarihi { get; set; }
    }

    public class RandevularimSatirViewModel
    {
        public int Id { get; set; }
        public string IsletmeAdi { get; set; } = "";
        public string SubeAdi { get; set; } = "";
        public string HizmetAdi { get; set; } = "";
        public string? PersonelAdi { get; set; }
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
        public string Durum { get; set; } = "";
        public string? IsletmeAciklamasi { get; set; }
        public decimal? ToplamFiyat { get; set; }
        public int? ToplamSure { get; set; }
    }
}
