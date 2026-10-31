using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize]
    public class CalismaSaatleriController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;
        public CalismaSaatleriController(AtlasRandevuDbContext db, UserManager<IdentityUser> users)
        { _db = db; _users = users; }

        private async Task<List<int>> YetkiliSubeIdleri()
        {
            var uid = _users.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return new List<int>();
            var isletmeIds = await _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == uid && x.AktifMi && x.Rol == "IsletmeSahibi" && x.Isletme.Durum == "Onaylandi")
                .Select(x => x.IsletmeId).ToListAsync();
            return await _db.Subeler.AsNoTracking().Where(x => isletmeIds.Contains(x.IsletmeId))
                .Select(x => x.Id).ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? subeId)
        {
            var ids = await YetkiliSubeIdleri();
            if (!ids.Any()) return Forbid();
            var subeler = await _db.Subeler.AsNoTracking().Where(x => ids.Contains(x.Id))
                .OrderBy(x => x.Ad).Select(x => new SubeSecenek { Id = x.Id, Ad = x.Ad }).ToListAsync();
            int secili = subeId ?? subeler[0].Id;
            if (!ids.Contains(secili)) return Forbid();
            var kayitlar = await _db.SubeCalismaSaatleri.AsNoTracking().Where(x => x.SubeId == secili).ToListAsync();
            var model = new CalismaSaatleriModel { SubeId = secili, Subeler = subeler };
            foreach (var gun in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday })
            {
                var k = kayitlar.FirstOrDefault(x => x.Gun == gun);
                model.Gunler.Add(new CalismaGunuModel
                {
                    Gun = (int)gun,
                    AcikMi = k?.AcikMi ?? (gun != DayOfWeek.Sunday),
                    Acilis = (k?.Acilis ?? new TimeSpan(9, 0, 0)).ToString(@"hh\:mm"),
                    Kapanis = (k?.Kapanis ?? new TimeSpan(18, 0, 0)).ToString(@"hh\:mm")
                });
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Kaydet(CalismaSaatleriModel model)
        {
            var ids = await YetkiliSubeIdleri();
            if (!ids.Contains(model.SubeId)) return Forbid();
            if (model.Gunler == null || model.Gunler.Count != 7 ||
                model.Gunler.Select(x => x.Gun).Distinct().Count() != 7 ||
                model.Gunler.Any(x => x.Gun < 0 || x.Gun > 6))
                ModelState.AddModelError("", "Haftanın yedi günü eksiksiz girilmelidir.");
            var parsed = new Dictionary<int, (TimeSpan acilis, TimeSpan kapanis)>();
            if (model.Gunler != null)
                foreach (var g in model.Gunler)
                {
                    if (!TimeSpan.TryParseExact(g.Acilis, @"hh\:mm", null, out var a) ||
                        !TimeSpan.TryParseExact(g.Kapanis, @"hh\:mm", null, out var b) ||
                        a < TimeSpan.Zero || b > TimeSpan.FromDays(1) || (g.AcikMi && a >= b))
                        ModelState.AddModelError("", "Açık günlerde geçerli açılış ve kapanış saatleri girin.");
                    else parsed[g.Gun] = (a, b);
                }
            if (!ModelState.IsValid)
            {
                model.Subeler = await _db.Subeler.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .OrderBy(x => x.Ad).Select(x => new SubeSecenek { Id = x.Id, Ad = x.Ad }).ToListAsync();
                return View("Index", model);
            }
            var mevcut = await _db.SubeCalismaSaatleri.Where(x => x.SubeId == model.SubeId).ToListAsync();
            foreach (var g in model.Gunler)
            {
                var kayit = mevcut.FirstOrDefault(x => (int)x.Gun == g.Gun);
                if (kayit == null) { kayit = new SubeCalismaSaati { SubeId = model.SubeId, Gun = (DayOfWeek)g.Gun }; _db.SubeCalismaSaatleri.Add(kayit); }
                kayit.AcikMi = g.AcikMi;
                kayit.Acilis = parsed[g.Gun].acilis;
                kayit.Kapanis = parsed[g.Gun].kapanis;
            }
            await _db.SaveChangesAsync();
            TempData["Basari"] = "Çalışma saatleri kaydedildi.";
            return RedirectToAction(nameof(Index), new { subeId = model.SubeId });
        }
    }
    public class SubeSecenek { public int Id { get; set; } public string Ad { get; set; } = ""; }
    public class CalismaSaatleriModel
    {
        public int SubeId { get; set; }
        public List<SubeSecenek> Subeler { get; set; } = new();
        public List<CalismaGunuModel> Gunler { get; set; } = new();
    }
    public class CalismaGunuModel
    {
        public int Gun { get; set; }
        public bool AcikMi { get; set; }
        [Required] public string Acilis { get; set; } = "09:00";
        [Required] public string Kapanis { get; set; } = "18:00";
    }
}
