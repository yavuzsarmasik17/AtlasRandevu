using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    public class HesapController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AtlasRandevuDbContext _db;

        public HesapController(SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager, AtlasRandevuDbContext db)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _db = db;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Giris(string? returnUrl = null, string? girisTipi = null)
        {
            girisTipi = GirisTipiniDogrula(girisTipi);
            if (returnUrl != null && returnUrl.StartsWith("/Platform", StringComparison.OrdinalIgnoreCase))
                girisTipi = "yonetici";

            if (User.Identity?.IsAuthenticated == true)
            {
                var mevcut = await _userManager.GetUserAsync(User);
                if (mevcut != null)
                {
                    if (girisTipi == "musteri" && await _userManager.IsInRoleAsync(mevcut, "Musteri"))
                    {
                        var donus = GuvenliDonusAdresi(returnUrl);
                        if (donus != null) return LocalRedirect(donus);
                    }
                    return await YetkiyeGoreYonlendir(mevcut);
                }
            }

            ViewBag.ReturnUrl = GuvenliDonusAdresi(returnUrl);
            ViewBag.GirisTipi = girisTipi;
            return View(ViewAdi(girisTipi), new GirisViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Giris(GirisViewModel model,
            string? returnUrl = null, string? girisTipi = null)
        {
            girisTipi = GirisTipiniDogrula(girisTipi);
            ViewBag.ReturnUrl = GuvenliDonusAdresi(returnUrl);
            ViewBag.GirisTipi = girisTipi;
            if (!ModelState.IsValid) return View(ViewAdi(girisTipi), model);

            var user = await _userManager.FindByEmailAsync(model.Eposta.Trim());
            if (user == null)
            {
                ModelState.AddModelError("", "E-posta veya şifre hatalı.");
                return View(ViewAdi(girisTipi), model);
            }

            var platformAdmin = await _userManager.IsInRoleAsync(user, "PlatformAdmin");
            var isletmeSahibi = await _db.IsletmeKullanicilari.AsNoTracking().AnyAsync(x =>
                x.KullaniciId == user.Id && x.AktifMi && x.Rol == "IsletmeSahibi");
            var musteri = await _userManager.IsInRoleAsync(user, "Musteri");

            // Çakışan hesap türleri ve yanlış giriş kapıları reddedilir.
            var turSayisi = (platformAdmin ? 1 : 0) + (isletmeSahibi ? 1 : 0) + (musteri ? 1 : 0);
            var dogruKapi = turSayisi == 1 && (
                (girisTipi == "yonetici" && platformAdmin) ||
                (girisTipi == "isletme" && isletmeSahibi) ||
                (girisTipi == "musteri" && musteri));

            if (!dogruKapi)
            {
                ModelState.AddModelError("", "Bu giriş ekranı hesabınızın türüne uygun değil.");
                return View(ViewAdi(girisTipi), model);
            }

            // Her giriş oturumluk; formdan gelen BeniHatirla dikkate alınmaz.
            var result = await _signInManager.PasswordSignInAsync(
                user, model.Sifre, isPersistent: false, lockoutOnFailure: true);

            if (result.IsLockedOut)
                ModelState.AddModelError("", "Hesabınız geçici olarak kilitlendi. Daha sonra tekrar deneyin.");
            else if (result.IsNotAllowed)
                ModelState.AddModelError("", "Hesabınız için gerekli doğrulama henüz tamamlanmamış.");
            else if (result.RequiresTwoFactor)
                ModelState.AddModelError("", "Bu hesap için iki aşamalı doğrulama gerekiyor.");
            else if (!result.Succeeded)
                ModelState.AddModelError("", "E-posta veya şifre hatalı.");

            if (!result.Succeeded) return View(ViewAdi(girisTipi), model);
            // Müşteriyi kayıt/giriş öncesinde seçtiği randevu ekranına döndür.
            if (girisTipi == "musteri")
            {
                var donus = GuvenliDonusAdresi(returnUrl);
                if (donus != null) return LocalRedirect(donus);
            }
            return await YetkiyeGoreYonlendir(user);
        }

        private async Task<IActionResult> YetkiyeGoreYonlendir(IdentityUser user)
        {
            if (await _userManager.IsInRoleAsync(user, "PlatformAdmin"))
                return RedirectToAction("Index", "Platform");

            var isletme = await _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == user.Id && x.AktifMi && x.Rol == "IsletmeSahibi")
                .Select(x => new { x.Isletme.Durum })
                .FirstOrDefaultAsync();

            if (isletme != null)
                return isletme.Durum == "Onaylandi"
                    ? RedirectToAction("Index", "IsletmePanel")
                    : RedirectToAction("BasvuruDurumu", "Hesap");

            return RedirectToAction("Index", "Kesfet");
        }

        private static string ViewAdi(string? girisTipi) =>
            girisTipi == "yonetici" ? "YoneticiGiris" : "Giris";

        private string? GuvenliDonusAdresi(string? url) =>
            !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url) ? url : null;

        private static string GirisTipiniDogrula(string? girisTipi) =>
            girisTipi == "isletme" || girisTipi == "yonetici" ? girisTipi : "musteri";

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BasvuruDurumu()
        {
            var userId = _userManager.GetUserId(User);
            var isletme = await _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == userId && x.AktifMi && x.Rol == "IsletmeSahibi")
                .Select(x => new { x.Isletme.Ad, x.Isletme.Durum })
                .FirstOrDefaultAsync();

            if (isletme == null) return Forbid();
            ViewBag.IsletmeAdi = isletme.Ad;
            ViewBag.Durum = isletme.Durum;
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cikis()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Kesfet");
        }

        [HttpGet]
        public IActionResult Yetkisiz() => View();
    }

    public class GirisViewModel
    {
        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
        [Display(Name = "E-posta")]
        public string Eposta { get; set; } = "";

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Sifre { get; set; } = "";

        // Eski giriş ekranlarının derlenebilmesi için tutuldu; girişte kullanılmaz.
        public bool BeniHatirla { get; set; }
    }
}
