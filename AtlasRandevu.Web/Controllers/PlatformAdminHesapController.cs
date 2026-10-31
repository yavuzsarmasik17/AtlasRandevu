using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AtlasRandevu.Web.Controllers
{
    [Authorize(Policy = "PlatformYoneticisi")]
    public class PlatformAdminHesapController : Controller
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly SignInManager<IdentityUser> _signIn;
        private readonly ILogger<PlatformAdminHesapController> _logger;

        public PlatformAdminHesapController(UserManager<IdentityUser> users,
            SignInManager<IdentityUser> signIn,
            ILogger<PlatformAdminHesapController> logger)
        {
            _users = users;
            _signIn = signIn;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();
            return View(new PlatformAdminHesapModel { YeniEposta = user.Email ?? "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(PlatformAdminHesapModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();
            if (!await _users.IsInRoleAsync(user, "PlatformAdmin")) return Forbid();

            if (!await _users.CheckPasswordAsync(user, model.MevcutSifre))
            {
                ModelState.AddModelError(nameof(model.MevcutSifre), "Mevcut şifre yanlış.");
                return View(model);
            }

            var email = model.YeniEposta.Trim();
            var existing = await _users.FindByEmailAsync(email);
            if (existing != null && existing.Id != user.Id)
            {
                ModelState.AddModelError(nameof(model.YeniEposta), "Bu e-posta başka bir hesapta kullanılıyor.");
                return View(model);
            }

            // Şifreyi önce güncelle: hata olursa e-posta değişmeden kalır.
            var passwordResult = await _users.ChangePasswordAsync(user, model.MevcutSifre, model.YeniSifre);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                    ModelState.AddModelError(nameof(model.YeniSifre), error.Description);
                return View(model);
            }

            // E-posta ile kullanıcı adı ayrı Identity işlemleridir.
            // Olası kısmi hata durumunda yöneticiye açık uyarı gösterilir.
            var emailResult = await _users.SetEmailAsync(user, email);
            if (!emailResult.Succeeded)
            {
                foreach (var error in emailResult.Errors) ModelState.AddModelError("", error.Description);
                ModelState.AddModelError("", "Şifre güncellendi fakat e-posta değiştirilemedi. Yeni şifrenle tekrar giriş yapıp işlemi tamamla.");
                _logger.LogWarning("Platform admin e-posta değişikliği başarısız. UserId={UserId}", user.Id);
                return View(model);
            }

            var nameResult = await _users.SetUserNameAsync(user, email);
            if (!nameResult.Succeeded)
            {
                foreach (var error in nameResult.Errors) ModelState.AddModelError("", error.Description);
                ModelState.AddModelError("", "Şifre ve e-posta değişti fakat kullanıcı adı güncellenemedi. Yönetici desteğiyle kontrol edin.");
                _logger.LogWarning("Platform admin kullanıcı adı değişikliği başarısız. UserId={UserId}", user.Id);
                return View(model);
            }

            // E-posta doğrulamasını varsaymayız: giriş sistemi EmailConfirmed gerektiriyorsa ayrıca doğrulama gerekir.
            await _signIn.SignOutAsync();
            _logger.LogInformation("Platform admin kendi hesap bilgilerini güncelledi. UserId={UserId}", user.Id);
            TempData["Basari"] = "Hesap bilgilerin güncellendi. Yeni e-posta ve şifrenle giriş yap.";
            return RedirectToAction("Giris", "Hesap");
        }
    }

    public class PlatformAdminHesapModel
    {
        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        public string YeniEposta { get; set; } = "";

        [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
        [DataType(DataType.Password)]
        public string MevcutSifre { get; set; } = "";

        [Required(ErrorMessage = "Yeni şifre zorunludur.")]
        [DataType(DataType.Password)]
        public string YeniSifre { get; set; } = "";

        [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
        [Compare(nameof(YeniSifre), ErrorMessage = "Yeni şifreler eşleşmiyor.")]
        [DataType(DataType.Password)]
        public string YeniSifreTekrar { get; set; } = "";
    }
}
