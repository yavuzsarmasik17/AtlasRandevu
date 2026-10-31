
using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "OnayliIsletmeSahibi")]
    public class IsletmeManuelRandevuController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public IsletmeManuelRandevuController(
            AtlasRandevuDbContext db,
            UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        private async Task<int?> YetkiliIsletmeId()
        {
            var uid = _users.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return null;

            return await _db.IsletmeKullanicilari
                .AsNoTracking()
                .Where(x =>
                    x.KullaniciId == uid &&
                    x.AktifMi &&
                    x.Rol == "IsletmeSahibi" &&
                    x.Isletme.Durum == "Onaylandi")
                .Select(x => (int?)x.IsletmeId)
                .FirstOrDefaultAsync();
        }

        private async Task SecenekleriYukle(
            ManuelRandevuViewModel model,
            int isletmeId)
        {
            model.Subeler = await _db.Subeler
                .AsNoTracking()
                .Where(x =>
                    x.IsletmeId == isletmeId &&
                    x.Durum == "Aktif")
                .OrderBy(x => x.Ad)
                .Select(x => new ManuelSecenek
                {
                    Id = x.Id,
                    Ad = x.Ad
                })
                .ToListAsync();

            model.Personeller = await _db.Personeller
                .AsNoTracking()
                .Where(x =>
                    x.IsletmeId == isletmeId &&
                    x.AktifMi)
                .OrderBy(x => x.AdSoyad)
                .Select(x => new ManuelPersonelSecenek
                {
                    Id = x.Id,
                    SubeId = x.SubeId,
                    Ad = x.AdSoyad
                })
                .ToListAsync();

            model.Hizmetler = await _db.Hizmetler
                .AsNoTracking()
                .Where(x =>
                    x.IsletmeId == isletmeId &&
                    x.AktifMi)
                .OrderBy(x => x.Ad)
                .Select(x => new ManuelSecenek
                {
                    Id = x.Id,
                    Ad = x.Ad
                })
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Ekle(
            int? subeId,
            int? personelId,
            DateTime? tarih,
            TimeSpan? saat)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();

            var model = new ManuelRandevuViewModel
            {
                Tarih = DateTime.Today.AddDays(1),
                Saat = new TimeSpan(10, 0, 0),
                RandevuKaynagi = "Telefon"
            };

            await SecenekleriYukle(model, isletmeId.Value);

            if (subeId.HasValue &&
                model.Subeler.Any(x => x.Id == subeId.Value))
                model.SubeId = subeId.Value;

            if (model.SubeId > 0 && personelId.HasValue &&
                model.Personeller.Any(x => x.Id == personelId.Value &&
                                          x.SubeId == model.SubeId))
                model.PersonelId = personelId.Value;

            if (tarih.HasValue &&
                tarih.Value.Date >= DateTime.Today &&
                tarih.Value.Date <= DateTime.Today.AddMonths(3))
                model.Tarih = tarih.Value.Date;

            if (saat.HasValue &&
                saat.Value >= TimeSpan.Zero &&
                saat.Value < TimeSpan.FromDays(1) &&
                saat.Value.Ticks % TimeSpan.FromMinutes(15).Ticks == 0)
                model.Saat = saat.Value;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(
            ManuelRandevuViewModel model)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();

            await SecenekleriYukle(model, isletmeId.Value);

            if (model.RandevuKaynagi != "Telefon" &&
                model.RandevuKaynagi != "Elden")
            {
                ModelState.AddModelError(
                    nameof(model.RandevuKaynagi),
                    "Geçerli bir randevu kaynağı seçin.");
            }

            if (model.Tarih.Date != model.Tarih ||
                model.Tarih.Date < DateTime.Today ||
                model.Tarih.Date > DateTime.Today.AddMonths(3) ||
                model.Saat < TimeSpan.Zero ||
                model.Saat >= TimeSpan.FromDays(1) ||
                model.Saat.Ticks % TimeSpan.TicksPerMinute != 0)
            {
                ModelState.AddModelError(
                    nameof(model.Tarih),
                    "Geçerli bir tarih ve saat seçin.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var baslangic = model.Tarih.Date.Add(model.Saat);

            if (baslangic <= DateTime.Now)
            {
                ModelState.AddModelError(
                    nameof(model.Saat),
                    "Gelecekteki bir saat seçin.");

                return View(model);
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var isletmeAktif = await _db.Isletmeler
                    .AnyAsync(x =>
                        x.Id == isletmeId.Value &&
                        x.Durum == "Onaylandi");

                var subeAktif = await _db.Subeler
                    .AnyAsync(x =>
                        x.Id == model.SubeId &&
                        x.IsletmeId == isletmeId.Value &&
                        x.Durum == "Aktif");

                var personelAktif = await _db.Personeller
                    .AnyAsync(x =>
                        x.Id == model.PersonelId &&
                        x.IsletmeId == isletmeId.Value &&
                        x.SubeId == model.SubeId &&
                        x.AktifMi);

                var hizmet = await _db.Hizmetler
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == model.HizmetId &&
                        x.IsletmeId == isletmeId.Value &&
                        x.AktifMi);

                if (!isletmeAktif ||
                    !subeAktif ||
                    !personelAktif ||
                    hizmet == null ||
                    hizmet.SureDakika <= 0 ||
                    hizmet.SureDakika > 1440)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Şube, personel veya hizmet geçerli değil.");

                    await transaction.RollbackAsync();
                    return View(model);
                }

                var bitis = baslangic.AddMinutes(
                    hizmet.SureDakika);

                var calisma = await _db.SubeCalismaSaatleri
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.SubeId == model.SubeId &&
                        x.Gun == baslangic.DayOfWeek);

                if (calisma == null ||
                    !calisma.AcikMi ||
                    baslangic.TimeOfDay < calisma.Acilis ||
                    bitis.Date != baslangic.Date ||
                    bitis.TimeOfDay > calisma.Kapanis ||
                    (baslangic.TimeOfDay - calisma.Acilis).Ticks %
                        TimeSpan.FromMinutes(15).Ticks != 0)
                {
                    ModelState.AddModelError(
                        nameof(model.Saat),
                        "Seçilen saat çalışma saatlerine uygun değil.");

                    await transaction.RollbackAsync();
                    return View(model);
                }

                var cakisma = await _db.Randevular.AnyAsync(x =>
                    x.SubeId == model.SubeId &&
                    (x.PersonelId == model.PersonelId ||
                     x.PersonelId == null) &&
                    (x.Durum == "OnayBekliyor" ||
                     x.Durum == "Onaylandi" ||
                     x.Durum == "Geldi") &&
                    x.BaslangicTarihi < bitis &&
                    x.BitisTarihi > baslangic);

                var kapali = await _db.PersonelKapaliZamanlar
                    .AnyAsync(x =>
                        x.IsletmeId == isletmeId.Value &&
                        x.SubeId == model.SubeId &&
                        x.PersonelId == model.PersonelId &&
                        x.BaslangicTarihi < bitis &&
                        x.BitisTarihi > baslangic);

                if (cakisma || kapali)
                {
                    ModelState.AddModelError(
                        nameof(model.Saat),
                        "Bu berber seçilen saatte dolu.");

                    await transaction.RollbackAsync();
                    return View(model);
                }

                var randevu = new Randevu
                {
                    IsletmeId = isletmeId.Value,
                    SubeId = model.SubeId,
                    PersonelId = model.PersonelId,
                    HizmetId = hizmet.Id,

                    MusteriKullaniciId = null,
                    ManuelMusteriAdi = model.MusteriAdi.Trim(),
                    ManuelMusteriTelefonu =
                        model.MusteriTelefonu?.Trim(),

                    RandevuKaynagi = model.RandevuKaynagi,

                    BaslangicTarihi = baslangic,
                    BitisTarihi = bitis,
                    Durum = "Onaylandi",
                    OnayTarihi = DateTime.UtcNow,
                    OlusturmaTarihi = DateTime.UtcNow
                };

                _db.Randevular.Add(randevu);
                await _db.SaveChangesAsync();

                _db.RandevuHizmetleri.Add(
                    new RandevuHizmeti
                    {
                        RandevuId = randevu.Id,
                        HizmetId = hizmet.Id,
                        Fiyat = hizmet.Fiyat,
                        SureDakika = hizmet.SureDakika
                    });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["RandevuMesaj"] =
                    "Manuel randevu başarıyla oluşturuldu.";

                return RedirectToAction(
                    "Index",
                    "IsletmeRandevular");
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 1205 ||
                      ex.Number == 1222 ||
                      ex.Number == 3960)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(model.Saat),
                    "Bu saat için başka bir işlem yapıldı. Tekrar deneyin.");

                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();

            var randevu = await _db.Randevular.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id &&
                    x.IsletmeId == isletmeId.Value &&
                    (x.RandevuKaynagi == "Telefon" || x.RandevuKaynagi == "Elden") &&
                    x.MusteriKullaniciId == null &&
                    x.Durum == "Onaylandi" && x.BaslangicTarihi > DateTime.Now);

            if (randevu == null) return NotFound();

            var model = new ManuelRandevuViewModel
            {
                Id = randevu.Id,
                SubeId = randevu.SubeId,
                PersonelId = randevu.PersonelId ?? 0,
                HizmetId = randevu.HizmetId,
                MusteriAdi = randevu.ManuelMusteriAdi ?? "",
                MusteriTelefonu = randevu.ManuelMusteriTelefonu,
                RandevuKaynagi = randevu.RandevuKaynagi,
                Tarih = randevu.BaslangicTarihi.Date,
                Saat = randevu.BaslangicTarihi.TimeOfDay
            };
            await SecenekleriYukle(model, isletmeId.Value);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(ManuelRandevuViewModel model)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();
            await SecenekleriYukle(model, isletmeId.Value);

            if (model.Id <= 0)
                return BadRequest();

            if (model.RandevuKaynagi != "Telefon" && model.RandevuKaynagi != "Elden")
                ModelState.AddModelError(nameof(model.RandevuKaynagi), "Geçerli kaynak seçin.");

            if (model.Tarih.Date != model.Tarih ||
                model.Tarih.Date < DateTime.Today ||
                model.Tarih.Date > DateTime.Today.AddMonths(3) ||
                model.Saat < TimeSpan.Zero || model.Saat >= TimeSpan.FromDays(1) ||
                model.Saat.Ticks % TimeSpan.TicksPerMinute != 0)
                ModelState.AddModelError(nameof(model.Tarih), "Geçerli tarih ve saat seçin.");

            if (!ModelState.IsValid) return View(model);

            var baslangic = model.Tarih.Date.Add(model.Saat);
            if (baslangic <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(model.Saat), "Gelecekteki bir saat seçin.");
                return View(model);
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var randevu = await _db.Randevular.FirstOrDefaultAsync(x =>
                    x.Id == model.Id && x.IsletmeId == isletmeId.Value &&
                    (x.RandevuKaynagi == "Telefon" || x.RandevuKaynagi == "Elden") &&
                    x.MusteriKullaniciId == null && x.Durum == "Onaylandi" &&
                    x.BaslangicTarihi > DateTime.Now);

                if (randevu == null)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Randevu bulunamadı veya artık düzenlenemiyor.");
                    return View(model);
                }

                var subeAktif = await _db.Subeler.AnyAsync(x =>
                    x.Id == model.SubeId && x.IsletmeId == isletmeId.Value && x.Durum == "Aktif");
                var personelAktif = await _db.Personeller.AnyAsync(x =>
                    x.Id == model.PersonelId && x.IsletmeId == isletmeId.Value &&
                    x.SubeId == model.SubeId && x.AktifMi);
                var hizmet = await _db.Hizmetler.AsNoTracking().FirstOrDefaultAsync(x =>
                    x.Id == model.HizmetId && x.IsletmeId == isletmeId.Value && x.AktifMi);

                if (!subeAktif || !personelAktif || hizmet == null ||
                    hizmet.SureDakika <= 0 || hizmet.SureDakika > 1440)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Şube, personel veya hizmet geçerli değil.");
                    return View(model);
                }

                var bitis = baslangic.AddMinutes(hizmet.SureDakika);
                var calisma = await _db.SubeCalismaSaatleri.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SubeId == model.SubeId && x.Gun == baslangic.DayOfWeek);

                if (calisma == null || !calisma.AcikMi ||
                    baslangic.TimeOfDay < calisma.Acilis ||
                    bitis.Date != baslangic.Date ||
                    bitis.TimeOfDay > calisma.Kapanis ||
                    (baslangic.TimeOfDay - calisma.Acilis).Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(nameof(model.Saat), "Seçilen saat çalışma saatlerine uygun değil.");
                    return View(model);
                }

                var cakisma = await _db.Randevular.AnyAsync(x =>
                    x.Id != randevu.Id && x.SubeId == model.SubeId &&
                    (x.PersonelId == model.PersonelId || x.PersonelId == null) &&
                    (x.Durum == "OnayBekliyor" || x.Durum == "Onaylandi" || x.Durum == "Geldi") &&
                    x.BaslangicTarihi < bitis && x.BitisTarihi > baslangic);

                var kapali = await _db.PersonelKapaliZamanlar.AnyAsync(x =>
                    x.IsletmeId == isletmeId.Value && x.SubeId == model.SubeId &&
                    x.PersonelId == model.PersonelId &&
                    x.BaslangicTarihi < bitis && x.BitisTarihi > baslangic);

                if (cakisma || kapali)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(nameof(model.Saat), "Bu personel seçilen saatte dolu.");
                    return View(model);
                }

                randevu.SubeId = model.SubeId;
                randevu.PersonelId = model.PersonelId;
                randevu.HizmetId = hizmet.Id;
                randevu.ManuelMusteriAdi = model.MusteriAdi.Trim();
                randevu.ManuelMusteriTelefonu = model.MusteriTelefonu?.Trim();
                randevu.RandevuKaynagi = model.RandevuKaynagi;
                randevu.BaslangicTarihi = baslangic;
                randevu.BitisTarihi = bitis;

                var eskiHizmetler = await _db.RandevuHizmetleri
                    .Where(x => x.RandevuId == randevu.Id).ToListAsync();
                _db.RandevuHizmetleri.RemoveRange(eskiHizmetler);
                _db.RandevuHizmetleri.Add(new RandevuHizmeti
                {
                    RandevuId = randevu.Id,
                    HizmetId = hizmet.Id,
                    Fiyat = hizmet.Fiyat,
                    SureDakika = hizmet.SureDakika
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["RandevuMesaj"] = "Manuel randevu güncellendi.";
                return RedirectToAction("Index", "IsletmeRandevular");
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 1205 || ex.Number == 1222 || ex.Number == 3960)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(nameof(model.Saat),
                    "Bu saat için başka bir işlem yapıldı. Tekrar deneyin.");
                return View(model);
            }
        }
    }

    public class ManuelRandevuViewModel
    {
        public int Id { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Şube seçin.")]
        public int SubeId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Personel seçin.")]
        public int PersonelId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Hizmet seçin.")]
        public int HizmetId { get; set; }

        [Required(ErrorMessage = "Müşteri adını girin.")]
        [StringLength(150)]
        public string MusteriAdi { get; set; } = "";

        [StringLength(20)]
        public string? MusteriTelefonu { get; set; }

        [Required]
        public string RandevuKaynagi { get; set; } = "Telefon";

        [DataType(DataType.Date)]
        public DateTime Tarih { get; set; }

        public TimeSpan Saat { get; set; }

        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public List<ManuelSecenek> Subeler { get; set; } = new();

        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public List<ManuelPersonelSecenek> Personeller { get; set; } = new();

        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public List<ManuelSecenek> Hizmetler { get; set; } = new();
    }

    public class ManuelSecenek
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
    }

    public class ManuelPersonelSecenek
    {
        public int Id { get; set; }
        public int SubeId { get; set; }
        public string Ad { get; set; } = "";
    }
}
