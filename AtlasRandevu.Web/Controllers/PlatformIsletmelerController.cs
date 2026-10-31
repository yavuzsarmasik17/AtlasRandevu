using System;
using System.Linq;
using System.Threading.Tasks;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "PlatformYoneticisi")]
    public class PlatformIsletmelerController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly ILogger<PlatformIsletmelerController> _logger;

        public PlatformIsletmelerController(AtlasRandevuDbContext db, ILogger<PlatformIsletmelerController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? ara, string? durum)
        {
            var sorgu = _db.Isletmeler.AsNoTracking().Include(x => x.Sektor).AsQueryable();
            if (!string.IsNullOrWhiteSpace(ara))
            {
                var kelime = ara.Trim();
                sorgu = sorgu.Where(x => x.Ad.Contains(kelime) ||
                    (x.TicariUnvan != null && x.TicariUnvan.Contains(kelime)) ||
                    (x.Sektor != null && x.Sektor.Ad.Contains(kelime)));
            }
            if (durum == "OnayBekliyor" || durum == "Onaylandi" || durum == "Reddedildi" || durum == "Pasif")
                sorgu = sorgu.Where(x => x.Durum == durum);
            ViewBag.Ara = ara;
            ViewBag.Durum = durum;
            return View(await sorgu.OrderByDescending(x => x.OlusturmaTarihi).ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Detay(int id)
        {
            var isletme = await _db.Isletmeler.AsNoTracking()
                .Include(x => x.Sektor).Include(x => x.Subeler)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (isletme == null) return NotFound();
            ViewBag.Hizmetler = await _db.Hizmetler.AsNoTracking()
                .Where(x => x.IsletmeId == id).OrderBy(x => x.Ad).ToListAsync();
            return View(isletme);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PasifeAl(int id)
        {
            var isletme = await _db.Isletmeler.FindAsync(id);
            if (isletme == null) return NotFound();
            if (isletme.Durum != "Onaylandi")
            {
                TempData["Hata"] = "Yalnızca onaylı işletmeler pasife alınabilir.";
                return RedirectToAction(nameof(Detay), new { id });
            }
            isletme.Durum = "Pasif";
            await _db.SaveChangesAsync();
            TempData["Basari"] = "İşletme pasife alındı. Mevcut kayıtlar korundu.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aktiflestir(int id)
        {
            var isletme = await _db.Isletmeler.FindAsync(id);
            if (isletme == null) return NotFound();
            if (isletme.Durum != "Pasif")
            {
                TempData["Hata"] = "Yalnızca pasif işletmeler aktifleştirilebilir.";
                return RedirectToAction(nameof(Detay), new { id });
            }
            isletme.Durum = "Onaylandi";
            await _db.SaveChangesAsync();
            TempData["Basari"] = "İşletme yeniden aktifleştirildi.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Sil(int id)
        {
            var model = await _db.Isletmeler.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new IsletmeSilViewModel { Id = x.Id, Ad = x.Ad })
                .FirstOrDefaultAsync();
            if (model == null) return NotFound();
            await SayilariDoldur(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Sil")]
        public async Task<IActionResult> SilOnayla(IsletmeSilViewModel model)
        {
            var isletme = await _db.Isletmeler.FirstOrDefaultAsync(x => x.Id == model.Id);
            if (isletme == null) return NotFound();
            model.Ad = isletme.Ad;
            await SayilariDoldur(model);

            if (!string.Equals(model.OnayAdi?.Trim(), isletme.Ad, StringComparison.Ordinal))
                ModelState.AddModelError(nameof(model.OnayAdi), "İşletmenin adını birebir yazın.");
            if (!model.KaliciSilmeOnayi)
                ModelState.AddModelError(nameof(model.KaliciSilmeOnayi), "Kalıcı silme onayı gereklidir.");
            if (!ModelState.IsValid) return View("Sil", model);

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Önce belgeye bağlı doğrulamalar, sonra belgeler silinmeli.
                var belgeIdleri = await _db.IsletmeBelgeleri
                    .Where(x => x.IsletmeId == model.Id)
                    .Select(x => x.Id).ToListAsync();

                _db.BelgeDogrulamalari.RemoveRange(await _db.BelgeDogrulamalari
                    .Where(x => belgeIdleri.Contains(x.BelgeId)).ToListAsync());
                await _db.SaveChangesAsync();

                _db.IsletmeBelgeleri.RemoveRange(await _db.IsletmeBelgeleri
                    .Where(x => x.IsletmeId == model.Id).ToListAsync());
                _db.IsletmeVergiBilgileri.RemoveRange(await _db.IsletmeVergiBilgileri
                    .Where(x => x.IsletmeId == model.Id).ToListAsync());
                await _db.SaveChangesAsync();

                // Yorumlar randevulara bağlı olduğundan önce yorumları sil.
                _db.MusteriYorumlari.RemoveRange(await _db.MusteriYorumlari
                    .Where(x => x.IsletmeId == model.Id).ToListAsync());
                await _db.SaveChangesAsync();

                _db.Randevular.RemoveRange(await _db.Randevular
                    .Where(x => x.IsletmeId == model.Id).ToListAsync());
                await _db.SaveChangesAsync();

                _db.IsletmeFotograflari.RemoveRange(await _db.IsletmeFotograflari
                    .Where(x => x.IsletmeId == model.Id).ToListAsync());
                _db.IsletmeKullanicilari.RemoveRange(await _db.IsletmeKullanicilari
                    .Where(x => x.IsletmeId == model.Id).ToListAsync());
                _db.Hizmetler.RemoveRange(await _db.Hizmetler
                    .Where(x => x.IsletmeId == model.Id).ToListAsync());
                await _db.SaveChangesAsync();

                _db.Subeler.RemoveRange(await _db.Subeler
                    .Where(x => x.IsletmeId == model.Id).ToListAsync());
                await _db.SaveChangesAsync();

                _db.Isletmeler.Remove(isletme);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Basari"] = "İşletme ve ilişkili veritabanı kayıtları silindi. Kullanıcı hesapları korundu.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "İşletme kalıcı silinemedi. İşletmeId={IsletmeId}", model.Id);
                ModelState.AddModelError("", "İşletme silinemedi. Ek veritabanı ilişkisi olabilir. İşlem geri alındı; ayrıntı sunucu kayıtlarında.");
                return View("Sil", model);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "İşletme silme işleminde hata. İşletmeId={IsletmeId}", model.Id);
                ModelState.AddModelError("", "Silme sırasında hata oluştu. İşlem geri alındı; ayrıntı sunucu kayıtlarında.");
                return View("Sil", model);
            }
        }

        private async Task SayilariDoldur(IsletmeSilViewModel model)
        {
            var id = model.Id;
            model.SubeSayisi = await _db.Subeler.CountAsync(x => x.IsletmeId == id);
            model.HizmetSayisi = await _db.Hizmetler.CountAsync(x => x.IsletmeId == id);
            model.RandevuSayisi = await _db.Randevular.CountAsync(x => x.IsletmeId == id);
            model.YorumSayisi = await _db.MusteriYorumlari.CountAsync(x => x.IsletmeId == id);
            model.KullaniciSayisi = await _db.IsletmeKullanicilari.CountAsync(x => x.IsletmeId == id);
            model.FotografSayisi = await _db.IsletmeFotograflari.CountAsync(x => x.IsletmeId == id);
        }
    }

    public class IsletmeSilViewModel
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
        public string? OnayAdi { get; set; }
        public bool KaliciSilmeOnayi { get; set; }
        public int SubeSayisi { get; set; }
        public int HizmetSayisi { get; set; }
        public int RandevuSayisi { get; set; }
        public int YorumSayisi { get; set; }
        public int KullaniciSayisi { get; set; }
        public int FotografSayisi { get; set; }
    }
}
