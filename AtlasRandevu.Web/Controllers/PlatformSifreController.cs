using System;
using System.Linq;
using System.Threading.Tasks;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "PlatformYoneticisi")]
    public class PlatformSifreController : Controller
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;
        private readonly ILogger<PlatformSifreController> _logger;

        public PlatformSifreController(AtlasRandevuDbContext db,
            UserManager<IdentityUser> users,
            ILogger<PlatformSifreController> logger)
        {
            _db = db;
            _users = users;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sifirla(int id, string eposta,
            string yeniSifre, string yeniSifreTekrar)
        {
            IActionResult Don() => RedirectToAction("Detay", "PlatformIsletmeler", new { id });

            if (!await _db.Isletmeler.AnyAsync(x => x.Id == id))
                return NotFound();

            if (string.IsNullOrWhiteSpace(eposta) ||
                string.IsNullOrWhiteSpace(yeniSifre) ||
                yeniSifre != yeniSifreTekrar)
            {
                TempData["Hata"] = "E-posta ve şifre alanlarını kontrol edin. Şifreler aynı olmalıdır.";
                return Don();
            }

            var user = await _users.FindByEmailAsync(eposta.Trim());
            if (user == null || !await _db.IsletmeKullanicilari.AnyAsync(x =>
                x.IsletmeId == id && x.KullaniciId == user.Id &&
                x.Rol == "IsletmeSahibi" && x.AktifMi))
            {
                TempData["Hata"] = "Bu işletmeye bağlı aktif işletme sahibi bulunamadı.";
                return Don();
            }

            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var sonuc = await _users.ResetPasswordAsync(user, token, yeniSifre);
            if (!sonuc.Succeeded)
            {
                TempData["Hata"] = string.Join(" ", sonuc.Errors.Select(x => x.Description));
                return Don();
            }

            // Mevcut oturumları geçersiz kılmak için güvenlik damgasını yenile.
            var damgaSonuc = await _users.UpdateSecurityStampAsync(user);
            if (!damgaSonuc.Succeeded)
            {
                _logger.LogWarning("Şifre değişti ancak güvenlik damgası yenilenemedi. IsletmeId={IsletmeId}, KullaniciId={KullaniciId}", id, user.Id);
                TempData["Hata"] = "Şifre değişti ancak mevcut oturumları kapatma işlemi tamamlanamadı. Destek kontrolü gerekiyor.";
                return Don();
            }

            _logger.LogInformation("Platform yöneticisi işletme sahibi şifresini sıfırladı. IsletmeId={IsletmeId}, KullaniciId={KullaniciId}, YoneticiId={YoneticiId}",
                id, user.Id, _users.GetUserId(User));
            TempData["Basari"] = "İşletme sahibinin şifresi güncellendi. Yeni şifreyi güvenli bir kanaldan iletin.";
            return Don();
        }
    }
}
