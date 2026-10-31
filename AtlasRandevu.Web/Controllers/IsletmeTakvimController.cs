using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "OnayliIsletmeSahibi")]
    public class IsletmeTakvimController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public IsletmeTakvimController(AtlasRandevuDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
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

        [HttpGet]
        public async Task<IActionResult> Index(int? subeId, DateTime? tarih, int? hizmetId)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();
            var subeler = await _db.Subeler.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && x.Durum == "Aktif")
                .OrderBy(x => x.Ad).ToListAsync();
            var seciliSubeId = subeId ?? subeler.FirstOrDefault()?.Id;
            if (seciliSubeId.HasValue && !subeler.Any(x => x.Id == seciliSubeId.Value))
                return BadRequest("Geçersiz şube.");
            var gun = (tarih ?? DateTime.Today).Date;
            var ertesiGun = gun.AddDays(1);
            var personeller = await _db.Personeller.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && x.AktifMi &&
                            seciliSubeId.HasValue && x.SubeId == seciliSubeId.Value)
                .OrderBy(x => x.AdSoyad).ToListAsync();
            var randevular = await _db.Randevular.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && seciliSubeId.HasValue &&
                            x.SubeId == seciliSubeId.Value &&
                            x.BaslangicTarihi < ertesiGun && x.BitisTarihi > gun &&
                            x.Durum != "IptalEdildi" && x.Durum != "Reddedildi" && x.Durum != "Gelmedi")
                .OrderBy(x => x.BaslangicTarihi).ToListAsync();
            var kapaliZamanlar = await _db.PersonelKapaliZamanlar.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && seciliSubeId.HasValue &&
                            x.SubeId == seciliSubeId.Value &&
                            x.BaslangicTarihi < ertesiGun && x.BitisTarihi > gun)
                .ToListAsync();
            var calismaSaati = seciliSubeId.HasValue
                ? await _db.SubeCalismaSaatleri.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SubeId == seciliSubeId.Value && x.Gun == gun.DayOfWeek)
                : null;
            var hizmetler = await _db.Hizmetler.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && x.AktifMi &&
                            x.SureDakika > 0 && x.SureDakika <= 1440)
                .OrderBy(x => x.Ad).ToListAsync();
            if (hizmetId.HasValue && !hizmetler.Any(x => x.Id == hizmetId.Value))
                return BadRequest("Geçersiz hizmet.");
            ViewBag.Hizmetler = hizmetler;
            ViewBag.SeciliHizmetId = hizmetId;
            ViewBag.Subeler = subeler;
            ViewBag.SeciliSubeId = seciliSubeId;
            ViewBag.Tarih = gun;
            ViewBag.Personeller = personeller;
            ViewBag.Randevular = randevular;
            ViewBag.KapaliZamanlar = kapaliZamanlar;
            ViewBag.CalismaSaati = calismaSaati;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Haftalik(int? subeId, DateTime? tarih, int? personelId)
        {
            var isletmeId = await YetkiliIsletmeId();
            if (!isletmeId.HasValue) return Forbid();
            var subeler = await _db.Subeler.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && x.Durum == "Aktif")
                .OrderBy(x => x.Ad).ToListAsync();
            var seciliSubeId = subeId ?? subeler.FirstOrDefault()?.Id;
            if (seciliSubeId.HasValue && !subeler.Any(x => x.Id == seciliSubeId.Value))
                return BadRequest("Geçersiz şube.");
            var personeller = await _db.Personeller.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && x.AktifMi &&
                            seciliSubeId.HasValue && x.SubeId == seciliSubeId.Value)
                .OrderBy(x => x.AdSoyad).ToListAsync();
            if (personelId.HasValue && !personeller.Any(x => x.Id == personelId.Value))
                return BadRequest("Geçersiz personel.");
            var seciliGun = (tarih ?? DateTime.Today).Date;
            var haftaBaslangic = seciliGun.AddDays(-(((int)seciliGun.DayOfWeek + 6) % 7));
            var haftaBitis = haftaBaslangic.AddDays(7);
            var randevular = await _db.Randevular.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && seciliSubeId.HasValue &&
                            x.SubeId == seciliSubeId.Value &&
                            x.BaslangicTarihi < haftaBitis && x.BitisTarihi > haftaBaslangic &&
                            (!personelId.HasValue || x.PersonelId == personelId.Value) &&
                            x.Durum != "IptalEdildi" && x.Durum != "Reddedildi" && x.Durum != "Gelmedi")
                .OrderBy(x => x.BaslangicTarihi).ToListAsync();
            var kapaliZamanlar = await _db.PersonelKapaliZamanlar.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value && seciliSubeId.HasValue &&
                            x.SubeId == seciliSubeId.Value &&
                            x.BaslangicTarihi < haftaBitis && x.BitisTarihi > haftaBaslangic &&
                            (!personelId.HasValue || x.PersonelId == personelId.Value))
                .ToListAsync();
            var calismaSaatleri = seciliSubeId.HasValue
                ? await _db.SubeCalismaSaatleri.AsNoTracking()
                    .Where(x => x.SubeId == seciliSubeId.Value).ToListAsync()
                : new System.Collections.Generic.List<AtlasRandevu.Domain.Entities.SubeCalismaSaati>();
            var hizmetler = await _db.Hizmetler.AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId.Value)
                .OrderBy(x => x.Ad).ToListAsync();
            ViewBag.Subeler = subeler;
            ViewBag.SeciliSubeId = seciliSubeId;
            ViewBag.Personeller = personeller;
            ViewBag.SeciliPersonelId = personelId;
            ViewBag.HaftaBaslangic = haftaBaslangic;
            ViewBag.Randevular = randevular;
            ViewBag.KapaliZamanlar = kapaliZamanlar;
            ViewBag.CalismaSaatleri = calismaSaatleri;
            ViewBag.Hizmetler = hizmetler;
            return View();
        }
    }
}
