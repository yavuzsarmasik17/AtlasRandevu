
using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "OnayliIsletmeSahibi")]
    public class IsletmeKapaliZamanlarController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public IsletmeKapaliZamanlarController(
            AtlasRandevuDbContext db,
            UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        private async Task<int?> YetkiliIsletmeId()
        {
            var uid = _users.GetUserId(User);

            if (string.IsNullOrWhiteSpace(uid))
                return null;

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

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var isletmeId = await YetkiliIsletmeId();

            if (!isletmeId.HasValue)
                return Forbid();

            var model = new KapaliZamanSayfaModel
            {
                Tarih = DateTime.Today,
                BaslangicSaati = new TimeSpan(13, 0, 0),
                BitisSaati = new TimeSpan(14, 0, 0),

                Subeler = await _db.Subeler.AsNoTracking()
                    .Where(x =>
                        x.IsletmeId == isletmeId.Value &&
                        x.Durum == "Aktif")
                    .OrderBy(x => x.Ad)
                    .Select(x => new KapaliZamanSecenek
                    {
                        Id = x.Id,
                        Ad = x.Ad
                    })
                    .ToListAsync(),

                Personeller = await _db.Personeller.AsNoTracking()
                    .Where(x =>
                        x.IsletmeId == isletmeId.Value &&
                        x.AktifMi)
                    .OrderBy(x => x.AdSoyad)
                    .Select(x => new KapaliZamanPersonelSecenek
                    {
                        Id = x.Id,
                        SubeId = x.SubeId,
                        Ad = x.AdSoyad
                    })
                    .ToListAsync(),

                Kayitlar = await _db.PersonelKapaliZamanlar
                    .AsNoTracking()
                    .Where(x =>
                        x.IsletmeId == isletmeId.Value &&
                        x.BitisTarihi >= DateTime.Now)
                    .OrderBy(x => x.BaslangicTarihi)
                    .Take(100)
                    .Select(x => new KapaliZamanSatiri
                    {
                        Id = x.Id,
                        PersonelAdi = _db.Personeller
                            .Where(p => p.Id == x.PersonelId)
                            .Select(p => p.AdSoyad)
                            .FirstOrDefault() ?? "",
                        Baslangic = x.BaslangicTarihi,
                        Bitis = x.BitisTarihi,
                        Aciklama = x.Aciklama
                    })
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(
            int subeId,
            int personelId,
            DateTime tarih,
            TimeSpan baslangicSaati,
            TimeSpan bitisSaati,
            [StringLength(300)] string? aciklama)
        {
            var isletmeId = await YetkiliIsletmeId();

            if (!isletmeId.HasValue)
                return Forbid();

            if (tarih.Date != tarih ||
                tarih.Date < DateTime.Today ||
                tarih.Date > DateTime.Today.AddMonths(3) ||
                baslangicSaati < TimeSpan.Zero ||
                bitisSaati > TimeSpan.FromDays(1) ||
                baslangicSaati >= bitisSaati ||
                baslangicSaati.Ticks % TimeSpan.TicksPerMinute != 0 ||
                bitisSaati.Ticks % TimeSpan.TicksPerMinute != 0)
            {
                TempData["RandevuHata"] =
                    "Geçerli bir tarih ve saat aralığı seçin.";

                return RedirectToAction(nameof(Index));
            }

            if (aciklama?.Length > 300)
            {
                TempData["RandevuHata"] =
                    "Açıklama en fazla 300 karakter olabilir.";

                return RedirectToAction(nameof(Index));
            }

            var baslangic = tarih.Date.Add(baslangicSaati);
            var bitis = tarih.Date.Add(bitisSaati);

            if (baslangic <= DateTime.Now)
            {
                TempData["RandevuHata"] =
                    "Geçmiş bir saat kapatılamaz.";

                return RedirectToAction(nameof(Index));
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var personelGecerli = await _db.Personeller
                    .AnyAsync(x =>
                        x.Id == personelId &&
                        x.IsletmeId == isletmeId.Value &&
                        x.SubeId == subeId &&
                        x.AktifMi &&
                        x.Sube.Durum == "Aktif");

                if (!personelGecerli)
                {
                    await transaction.RollbackAsync();

                    TempData["RandevuHata"] =
                        "Geçerli bir personel ve şube seçin.";

                    return RedirectToAction(nameof(Index));
                }

                var randevuVar = await _db.Randevular
                    .AnyAsync(x =>
                        x.SubeId == subeId &&
                        (x.PersonelId == personelId ||
                         x.PersonelId == null) &&
                        (x.Durum == "OnayBekliyor" ||
                         x.Durum == "Onaylandi" ||
                         x.Durum == "Geldi") &&
                        x.BaslangicTarihi < bitis &&
                        x.BitisTarihi > baslangic);

                var kapaliZamanVar =
                    await _db.PersonelKapaliZamanlar.AnyAsync(x =>
                        x.IsletmeId == isletmeId.Value &&
                        x.SubeId == subeId &&
                        x.PersonelId == personelId &&
                        x.BaslangicTarihi < bitis &&
                        x.BitisTarihi > baslangic);

                if (randevuVar || kapaliZamanVar)
                {
                    await transaction.RollbackAsync();

                    TempData["RandevuHata"] =
                        "Seçilen zaman aralığında randevu veya kapalı saat var.";

                    return RedirectToAction(nameof(Index));
                }

                _db.PersonelKapaliZamanlar.Add(
                    new PersonelKapaliZaman
                    {
                        IsletmeId = isletmeId.Value,
                        SubeId = subeId,
                        PersonelId = personelId,
                        BaslangicTarihi = baslangic,
                        BitisTarihi = bitis,
                        Aciklama = aciklama?.Trim(),
                        OlusturmaTarihi = DateTime.UtcNow
                    });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["RandevuMesaj"] =
                    "Seçilen saat aralığı kapatıldı.";

                return RedirectToAction(nameof(Index));
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 1205 ||
                      ex.Number == 1222 ||
                      ex.Number == 3960)
            {
                await transaction.RollbackAsync();

                TempData["RandevuHata"] =
                    "Aynı saat için başka bir işlem yapıldı. Tekrar deneyin.";

                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var isletmeId = await YetkiliIsletmeId();

            if (!isletmeId.HasValue)
                return Forbid();

            var kayit = await _db.PersonelKapaliZamanlar
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsletmeId == isletmeId.Value);

            if (kayit == null)
                return NotFound();

            _db.PersonelKapaliZamanlar.Remove(kayit);
            await _db.SaveChangesAsync();

            TempData["RandevuMesaj"] =
                "Kapalı saat kaldırıldı.";

            return RedirectToAction(nameof(Index));
        }
    }

    public class KapaliZamanSayfaModel
    {
        public int SubeId { get; set; }
        public int PersonelId { get; set; }
        public DateTime Tarih { get; set; }
        public TimeSpan BaslangicSaati { get; set; }
        public TimeSpan BitisSaati { get; set; }
        public string? Aciklama { get; set; }

        public List<KapaliZamanSecenek> Subeler { get; set; } = new();
        public List<KapaliZamanPersonelSecenek> Personeller { get; set; } = new();
        public List<KapaliZamanSatiri> Kayitlar { get; set; } = new();
    }

    public class KapaliZamanSecenek
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
    }

    public class KapaliZamanPersonelSecenek
    {
        public int Id { get; set; }
        public int SubeId { get; set; }
        public string Ad { get; set; } = "";
    }

    public class KapaliZamanSatiri
    {
        public int Id { get; set; }
        public string PersonelAdi { get; set; } = "";
        public DateTime Baslangic { get; set; }
        public DateTime Bitis { get; set; }
        public string? Aciklama { get; set; }
    }
}
