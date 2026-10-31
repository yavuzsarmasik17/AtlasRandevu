using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using AtlasRandevu.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    public class RandevuController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHubContext<IsletmeBildirimHub> _hub;
        private readonly ILogger<RandevuController> _logger;

        public RandevuController(AtlasRandevuDbContext db, UserManager<IdentityUser> userManager,
            IHubContext<IsletmeBildirimHub> hub, ILogger<RandevuController> logger)
        {
            _db = db;
            _userManager = userManager;
            _hub = hub;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Olustur(int isletmeId, int? hizmetId = null,
            [FromQuery] List<int>? hizmetIds = null, int? subeId = null,
            int? personelId = null, DateTime? tarih = null, TimeSpan? saat = null)
        {
            var isletme = await _db.Isletmeler.AsNoTracking()
                .Where(x => x.Id == isletmeId && x.Durum == "Onaylandi")
                .Select(x => new { x.Id, x.Ad }).FirstOrDefaultAsync();
            if (isletme == null) return NotFound();

            var secimler = (hizmetIds ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
            if (secimler.Count == 0 && hizmetId.GetValueOrDefault() > 0) secimler.Add(hizmetId!.Value);
            var model = new RandevuOlusturViewModel
            {
                IsletmeId = isletme.Id,
                IsletmeAdi = isletme.Ad,
                HizmetIds = secimler,
                Tarih = tarih.HasValue && tarih.Value.Date >= DateTime.Today &&
                        tarih.Value.Date <= DateTime.Today.AddMonths(3)
                    ? tarih.Value.Date : DateTime.Today.AddDays(1),
                SubeId = subeId.GetValueOrDefault(),
                PersonelId = personelId.GetValueOrDefault(),
                Saat = saat.HasValue && saat.Value >= TimeSpan.Zero && saat.Value < TimeSpan.FromDays(1)
                    ? saat.Value : TimeSpan.Zero
            };
            await SecenekleriYukle(model);
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Musteri")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Olustur(RandevuOlusturViewModel model)
        {
            await SecenekleriYukle(model);
            var kullaniciId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(kullaniciId)) return Challenge();

            var isletme = await _db.Isletmeler.AsNoTracking()
                .Where(x => x.Id == model.IsletmeId && x.Durum == "Onaylandi")
                .Select(x => new { x.Ad }).FirstOrDefaultAsync();
            if (isletme == null) return NotFound();
            model.IsletmeAdi = isletme.Ad;

            var secilenIdler = (model.HizmetIds ?? new List<int>()).Distinct().ToList();
            if (secilenIdler.Count == 0 || secilenIdler.Count > 20 || secilenIdler.Any(x => x <= 0))
                ModelState.AddModelError(nameof(model.HizmetIds), "1 ile 20 arasında hizmet seçin.");

            var hizmetler = await _db.Hizmetler.AsNoTracking()
                .Where(x => x.IsletmeId == model.IsletmeId && x.AktifMi && secilenIdler.Contains(x.Id))
                .ToListAsync();
            if (hizmetler.Count != secilenIdler.Count || hizmetler.Any(x => x.SureDakika <= 0 || x.Fiyat < 0))
                ModelState.AddModelError(nameof(model.HizmetIds), "Seçilen hizmetlerden biri geçerli değil.");

            var subeAktif = await _db.Subeler.AsNoTracking().AnyAsync(x =>
                x.Id == model.SubeId && x.IsletmeId == model.IsletmeId && x.Durum == "Aktif");
            if (!subeAktif) ModelState.AddModelError(nameof(model.SubeId), "Geçerli bir şube seçin.");

            var personelAktif = await _db.Personeller.AsNoTracking().AnyAsync(x =>
                x.Id == model.PersonelId && x.IsletmeId == model.IsletmeId &&
                x.SubeId == model.SubeId && x.AktifMi);
            if (!personelAktif) ModelState.AddModelError(nameof(model.PersonelId), "Seçilen şubeden aktif bir berber seçin.");

            if (model.Tarih.Date != model.Tarih || model.Tarih.Date < DateTime.Today ||
                model.Tarih.Date > DateTime.Today.AddMonths(3))
                ModelState.AddModelError(nameof(model.Tarih), "Bugünden itibaren en fazla 3 ay sonrası için tarih seçin.");
            if (model.Saat < TimeSpan.Zero || model.Saat >= TimeSpan.FromDays(1) ||
                model.Saat.Ticks % TimeSpan.TicksPerMinute != 0)
                ModelState.AddModelError(nameof(model.Saat), "Geçerli bir saat seçin.");
            if (!ModelState.IsValid) return View(model);

            var toplamSure = hizmetler.Sum(x => (long)x.SureDakika);
            if (toplamSure <= 0 || toplamSure > 1440)
            {
                ModelState.AddModelError(nameof(model.HizmetIds), "Toplam hizmet süresi bir günü aşamaz.");
                return View(model);
            }

            DateTime baslangic, bitis;
            try
            {
                baslangic = model.Tarih.Date.Add(model.Saat);
                bitis = baslangic.AddMinutes(toplamSure);
            }
            catch (ArgumentOutOfRangeException)
            {
                ModelState.AddModelError(string.Empty, "Tarih veya saat geçersiz.");
                return View(model);
            }
            if (baslangic <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(model.Saat), "Gelecekteki bir saat seçin.");
                return View(model);
            }

            var calisma = await _db.SubeCalismaSaatleri.AsNoTracking()
                .FirstOrDefaultAsync(x => x.SubeId == model.SubeId && x.Gun == baslangic.DayOfWeek);
            if (!CalismaUygun(calisma?.AcikMi == true, calisma?.Acilis ?? TimeSpan.Zero, calisma?.Kapanis ?? TimeSpan.Zero, baslangic, bitis))
            {
                ModelState.AddModelError(nameof(model.Saat), "Şube bu zaman aralığında açık değil.");
                return View(model);
            }

            // Saatler, şubenin açılışından itibaren 15 dakikalık dilimlerdir.
            // Tarayıcıdan elle gönderilen farklı saatler kabul edilmez.
            var acilisFarki = baslangic.TimeOfDay - calisma!.Acilis;
            if (acilisFarki < TimeSpan.Zero ||
                acilisFarki.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
            {
                ModelState.AddModelError(nameof(model.Saat), "Lütfen listeden 15 dakikalık uygun bir saat seçin.");
                return View(model);
            }

            // Serializable işlem: aynı personel için eşzamanlı çakışan talepleri önler.
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var halaAktif = await _db.Isletmeler.AnyAsync(x => x.Id == model.IsletmeId && x.Durum == "Onaylandi")
                    && await _db.Subeler.AnyAsync(x => x.Id == model.SubeId && x.IsletmeId == model.IsletmeId && x.Durum == "Aktif")
                    && await _db.Personeller.AnyAsync(x => x.Id == model.PersonelId && x.IsletmeId == model.IsletmeId && x.SubeId == model.SubeId && x.AktifMi);
                var guncelHizmetler = await _db.Hizmetler.AsNoTracking()
                    .Where(x => x.IsletmeId == model.IsletmeId && x.AktifMi && secilenIdler.Contains(x.Id)).ToListAsync();
                var guncelCalisma = await _db.SubeCalismaSaatleri.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SubeId == model.SubeId && x.Gun == baslangic.DayOfWeek);
                if (!halaAktif || guncelHizmetler.Count != secilenIdler.Count ||
                    guncelHizmetler.Sum(x => (long)x.SureDakika) != toplamSure ||
                    !CalismaUygun(guncelCalisma?.AcikMi == true, guncelCalisma?.Acilis ?? TimeSpan.Zero, guncelCalisma?.Kapanis ?? TimeSpan.Zero, baslangic, bitis) ||
                    (baslangic.TimeOfDay - (guncelCalisma?.Acilis ?? TimeSpan.Zero)).Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, "Hizmet veya çalışma bilgileri değişti. Yeniden saat seçin.");
                    return View(model);
                }

                // Personelin kapattığı zaman aralıklarını kontrol et.
                // Bu sorgu mevcut Serializable transaction içindedir.
                var kapaliSaatVar = await _db.PersonelKapaliZamanlar.AnyAsync(x =>
                    x.IsletmeId == model.IsletmeId &&
                    x.SubeId == model.SubeId &&
                    x.PersonelId == model.PersonelId &&
                    x.BaslangicTarihi < bitis &&
                    x.BitisTarihi > baslangic);

                if (kapaliSaatVar)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        nameof(model.Saat),
                        "Seçilen berber bu saat aralığında müsait değil.");

                    return View(model);
                }

                // Eski (personelsiz) randevular şubenin tamamını meşgul sayılır.
                var cakisma = await _db.Randevular.AnyAsync(r =>
                    r.SubeId == model.SubeId &&
                    (r.PersonelId == model.PersonelId || r.PersonelId == null) &&
                    (r.Durum == "OnayBekliyor" || r.Durum == "Onaylandi" || r.Durum == "Geldi") &&
                    r.BaslangicTarihi < bitis && r.BitisTarihi > baslangic);
                if (cakisma)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(nameof(model.Saat), "Seçilen berber bu saatte dolu.");
                    return View(model);
                }

                var randevu = new Randevu
                {
                    IsletmeId = model.IsletmeId,
                    SubeId = model.SubeId,
                    PersonelId = model.PersonelId,
                    // Eski ekranlarla uyum için ilk hizmet korunur.
                    HizmetId = secilenIdler[0],
                    MusteriKullaniciId = kullaniciId,
                    BaslangicTarihi = baslangic,
                    BitisTarihi = bitis,
                    MusteriNotu = model.MusteriNotu?.Trim(),
                    Durum = "Onaylandi",
                    OnayTarihi = DateTime.UtcNow,
                    OlusturmaTarihi = DateTime.UtcNow
                };
                _db.Randevular.Add(randevu);
                await _db.SaveChangesAsync();

                foreach (var id in secilenIdler)
                {
                    var h = guncelHizmetler.Single(x => x.Id == id);
                    _db.RandevuHizmetleri.Add(new RandevuHizmeti
                    {
                        RandevuId = randevu.Id,
                        HizmetId = h.Id,
                        Fiyat = h.Fiyat,
                        SureDakika = h.SureDakika
                    });
                }
                // Bildirim randevuyla aynı transaction içinde kaydedilir.
                var profil = await _db.MusteriProfilleri.AsNoTracking()
                    .Where(x => x.KullaniciId == kullaniciId)
                    .Select(x => new { x.Ad, x.Soyad })
                    .FirstOrDefaultAsync();
                var musteriAdi = profil == null ? "Müşteri" :
                    (profil.Ad + " " + profil.Soyad).Trim();
                if (string.IsNullOrWhiteSpace(musteriAdi)) musteriAdi = "Müşteri";

                var hizmetAdi = string.Join(" + ", secilenIdler.Select(id =>
                    guncelHizmetler.Single(h => h.Id == id).Ad));
                var bildirim = new IsletmeBildirimi
                {
                    IsletmeId = model.IsletmeId,
                    RandevuId = randevu.Id,
                    Tur = "YeniRandevu",
                    Baslik = "Yeni randevu alındı",
                    Mesaj = $"{musteriAdi} - {hizmetAdi} - {baslangic:dd.MM.yyyy HH:mm}",
                    OlusturulmaTarihi = DateTime.UtcNow,
                    OkunduMu = false
                };
                _db.IsletmeBildirimleri.Add(bildirim);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Canlı bildirim başarısız olsa bile onaylanmış randevu etkilenmez.
                try
                {
                    await _hub.Clients.Group(IsletmeBildirimHub.GrupAdi(model.IsletmeId))
                        .SendAsync("YeniRandevu", new
                        {
                            bildirimId = bildirim.Id,
                            randevuId = randevu.Id,
                            musteriAdi,
                            hizmetAdi,
                            randevuSaati = baslangic.ToString("HH:mm"),
                            randevuTarihi = baslangic.ToString("dd.MM.yyyy"),
                            durum = "Onaylandi"
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Randevu {RandevuId} canlı bildirimi gönderilemedi.", randevu.Id);
                }

                TempData["RandevuMesaj"] = "Randevunuz başarıyla oluşturuldu ve onaylandı.";

                return RedirectToAction(nameof(Basarili), new
                {
                    id = randevu.Id
                });
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Randevu kaydedilirken veritabanı hatası oluştu.");
                ModelState.AddModelError(string.Empty, "Randevu kaydedilemedi. Saat dolmuş olabilir, tekrar deneyin.");
                return View(model);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1205 || ex.Number == 1222 || ex.Number == 3960)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Eşzamanlı randevu işlemi çakıştı.");
                ModelState.AddModelError(nameof(model.Saat), "Bu saat için aynı anda başka bir işlem yapıldı. Lütfen müsait saatleri yenileyip tekrar deneyin.");
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> MusaitSaatler(int isletmeId, int subeId, int personelId,
            [FromQuery] List<int> hizmetIds, DateTime tarih)
        {
            if (tarih.Date != tarih || tarih.Date < DateTime.Today || tarih.Date > DateTime.Today.AddMonths(3))
                return BadRequest(new { mesaj = "En fazla 3 ay sonrası için tarih seçin." });
            var ids = (hizmetIds ?? new List<int>()).Distinct().ToList();
            if (ids.Count == 0 || ids.Count > 20 || ids.Any(x => x <= 0))
                return BadRequest(new { mesaj = "Geçerli hizmetler seçin." });

            var isletmeAktif = await _db.Isletmeler.AsNoTracking().AnyAsync(x => x.Id == isletmeId && x.Durum == "Onaylandi");
            var subeAktif = await _db.Subeler.AsNoTracking().AnyAsync(x => x.Id == subeId && x.IsletmeId == isletmeId && x.Durum == "Aktif");
            var personelAktif = await _db.Personeller.AsNoTracking().AnyAsync(x => x.Id == personelId && x.IsletmeId == isletmeId && x.SubeId == subeId && x.AktifMi);
            var sureler = await _db.Hizmetler.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId && x.AktifMi && ids.Contains(x.Id))
                .Select(x => x.SureDakika).ToListAsync();
            var toplamSure = sureler.Sum(x => (long)x);
            if (!isletmeAktif || !subeAktif || !personelAktif || sureler.Count != ids.Count ||
                sureler.Any(x => x <= 0) || toplamSure <= 0 || toplamSure > 1440)
                return BadRequest(new { mesaj = "İşletme, şube, personel veya hizmetler geçerli değil." });

            var calisma = await _db.SubeCalismaSaatleri.AsNoTracking()
                .FirstOrDefaultAsync(x => x.SubeId == subeId && x.Gun == tarih.DayOfWeek);
            if (calisma == null || !calisma.AcikMi)
                return Json(new { saatler = Array.Empty<object>(), mesaj = "Şube bu gün kapalı." });

            var gunBaslangic = tarih.Date;
            var gunBitis = gunBaslangic.AddDays(1);
            var doluAraliklar = await _db.Randevular.AsNoTracking()
                .Where(r => r.SubeId == subeId && (r.PersonelId == personelId || r.PersonelId == null) &&
                    (r.Durum == "OnayBekliyor" || r.Durum == "Onaylandi" || r.Durum == "Geldi") &&
                    r.BaslangicTarihi < gunBitis && r.BitisTarihi > gunBaslangic)
                .Select(r => new { r.BaslangicTarihi, r.BitisTarihi }).ToListAsync();

            // Personelin manuel kapattığı saatleri getir.
            var kapaliZamanlar = await _db.PersonelKapaliZamanlar
                .AsNoTracking()
                .Where(x =>
                    x.IsletmeId == isletmeId &&
                    x.SubeId == subeId &&
                    x.PersonelId == personelId &&
                    x.BaslangicTarihi < gunBitis &&
                    x.BitisTarihi > gunBaslangic)
                .Select(x => new
                {
                    x.BaslangicTarihi,
                    x.BitisTarihi
                })
                .ToListAsync();

            var saatler = new List<object>();
            var acilis = gunBaslangic.Add(calisma.Acilis);
            var kapanis = gunBaslangic.Add(calisma.Kapanis);
            for (var baslangic = acilis; baslangic.AddMinutes(toplamSure) <= kapanis &&
                baslangic.AddMinutes(toplamSure).Date == gunBaslangic.Date;
                baslangic = baslangic.AddMinutes(15))
            {
                var bitis = baslangic.AddMinutes(toplamSure);

                var cakisma =
                    doluAraliklar.Any(r =>
                        r.BaslangicTarihi < bitis &&
                        r.BitisTarihi > baslangic)
                    ||
                    kapaliZamanlar.Any(x =>
                        x.BaslangicTarihi < bitis &&
                        x.BitisTarihi > baslangic);

                saatler.Add(new { saat = baslangic.ToString("HH:mm"), uygun = baslangic > DateTime.Now && !cakisma });
            }
            return Json(new { saatler });
        }

        private static bool CalismaUygun(bool acikMi, TimeSpan acilis, TimeSpan kapanis, DateTime baslangic, DateTime bitis)
        {
            return acikMi && baslangic.TimeOfDay >= acilis &&
                bitis.Date == baslangic.Date && bitis.TimeOfDay <= kapanis;
        }

        [HttpGet]
        [Authorize(Roles = "Musteri")]
        public async Task<IActionResult> Basarili(int id)
        {
            var kullaniciId = _userManager.GetUserId(User);
            var randevu = await _db.Randevular.AsNoTracking()
                .Where(r => r.Id == id && r.MusteriKullaniciId == kullaniciId)
                .Select(r => new RandevuBasariliViewModel
                {
                    Id = r.Id,
                    IsletmeAdi = r.Isletme.Ad,
                    BaslangicTarihi = r.BaslangicTarihi,
                    Durum = r.Durum
                }).FirstOrDefaultAsync();
            if (randevu == null) return NotFound();

            // Şube telefonundan WhatsApp bağlantısı oluşturulur; yeni veritabanı alanı gerekmez.
            var iletisim = await _db.Randevular.AsNoTracking()
                .Where(r => r.Id == id && r.MusteriKullaniciId == kullaniciId)
                .Select(r => new { SubeAdi = r.Sube.Ad, Telefon = r.Sube.Telefon })
                .FirstOrDefaultAsync();

            var telefon = WhatsAppTelefonu(iletisim?.Telefon);
            if (telefon != null)
            {
                var profil = await _db.MusteriProfilleri.AsNoTracking()
                    .Where(p => p.KullaniciId == kullaniciId)
                    .Select(p => new { p.Ad, p.Soyad })
                    .FirstOrDefaultAsync();
                var musteri = profil == null ? "Müşteri" : (profil.Ad + " " + profil.Soyad).Trim();
                if (string.IsNullOrWhiteSpace(musteri)) musteri = "Müşteri";

                var hizmetAdlari = await _db.RandevuHizmetleri.AsNoTracking()
                    .Where(h => h.RandevuId == id)
                    .Select(h => h.Hizmet.Ad)
                    .ToListAsync();
                var hizmet = hizmetAdlari.Count > 0 ? string.Join(" + ", hizmetAdlari) : "Randevu";
                var mesaj = $"Merhaba, Atlas Randevu üzerinden işletmenizden randevu oluşturdum.\n\n" +
                    $"Müşteri: {musteri}\n" +
                    $"Randevu No: #{randevu.Id}\n" +
                    $"İşletme: {randevu.IsletmeAdi}\n" +
                    $"Şube: {iletisim?.SubeAdi}\n" +
                    $"Hizmet: {hizmet}\n" +
                    $"Tarih: {randevu.BaslangicTarihi:dd.MM.yyyy}\n" +
                    $"Saat: {randevu.BaslangicTarihi:HH:mm}\n\n" +
                    "Bu mesaj Atlas Randevu aracılığıyla hazırlanmıştır.";
                randevu.WhatsAppUrl = "https://wa.me/" + telefon + "?text=" + Uri.EscapeDataString(mesaj);
            }

            // Sadece POST sonrası ilk yönlendirmede otomatik aç; yenilemede tekrarlama.
            if (TempData.TryGetValue("WhatsAppRandevuId", out var otomatikId) &&
                int.TryParse(otomatikId?.ToString(), out var olusanId) && olusanId == id &&
                !string.IsNullOrEmpty(randevu.WhatsAppUrl))
            {
                randevu.WhatsAppOtomatikAc = true;
            }
            return View(randevu);
        }

        private static string? WhatsAppTelefonu(string? girilen)
        {
            if (string.IsNullOrWhiteSpace(girilen)) return null;
            // Yalnızca Türkiye numaraları; 05..., 5..., 90..., +90... biçimleri.
            var rakamlar = Regex.Replace(girilen, @"\D", "");
            if (rakamlar.Length == 11 && rakamlar.StartsWith("0")) rakamlar = "90" + rakamlar.Substring(1);
            else if (rakamlar.Length == 10 && rakamlar.StartsWith("5")) rakamlar = "90" + rakamlar;
            if (rakamlar.Length != 12 || !rakamlar.StartsWith("905")) return null;
            return rakamlar;
        }

        private async Task SecenekleriYukle(RandevuOlusturViewModel model)
        {
            model.Hizmetler = await _db.Hizmetler.AsNoTracking()
                .Where(h => h.IsletmeId == model.IsletmeId && h.AktifMi)
                .OrderBy(h => h.Ad).ToListAsync();
            model.Subeler = await _db.Subeler.AsNoTracking()
                .Where(s => s.IsletmeId == model.IsletmeId && s.Durum == "Aktif")
                .OrderBy(s => s.Ad).ToListAsync();
            model.Personeller = await _db.Personeller.AsNoTracking()
                .Where(p => p.IsletmeId == model.IsletmeId && p.AktifMi)
                .OrderBy(p => p.AdSoyad).ToListAsync();
        }
    }

    public class RandevuOlusturViewModel
    {
        [Range(1, int.MaxValue)]
        public int IsletmeId { get; set; }
        [ValidateNever]
        public string IsletmeAdi { get; set; } = "";
        public List<int> HizmetIds { get; set; } = new();
        [Range(1, int.MaxValue, ErrorMessage = "Şube seçin.")]
        public int SubeId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Berber seçin.")]
        public int PersonelId { get; set; }
        [DataType(DataType.Date)]
        public DateTime Tarih { get; set; }
        public TimeSpan Saat { get; set; }
        [StringLength(500)]
        public string? MusteriNotu { get; set; }
        [ValidateNever]
        public List<Hizmet> Hizmetler { get; set; } = new();
        [ValidateNever]
        public List<Sube> Subeler { get; set; } = new();
        [ValidateNever]
        public List<Personel> Personeller { get; set; } = new();
    }

    public class RandevuBasariliViewModel
    {
        public int Id { get; set; }
        public string IsletmeAdi { get; set; } = "";
        public DateTime BaslangicTarihi { get; set; }
        public string Durum { get; set; } = "";
        public string? WhatsAppUrl { get; set; }
        public bool WhatsAppOtomatikAc { get; set; }
    }
}
