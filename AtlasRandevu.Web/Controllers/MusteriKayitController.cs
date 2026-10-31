using AtlasRandevu.Domain.Entities;
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
    [AllowAnonymous]
    public class MusteriKayitController : Controller
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly SignInManager<IdentityUser> _signIn;
        private readonly RoleManager<IdentityRole> _roles;
        private readonly AtlasRandevuDbContext _db;

        public MusteriKayitController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn,
            RoleManager<IdentityRole> roles, AtlasRandevuDbContext db)
        {
            _users = users; _signIn = signIn; _roles = roles; _db = db;
        }

        [HttpGet]
        public IActionResult Index(string? returnUrl = null)
        {
            var donus = LocalUrl(returnUrl);
            if (User.Identity?.IsAuthenticated == true)
                return donus != null ? LocalRedirect(donus) : RedirectToAction("Index", "Kesfet");
            return View(new MusteriKayitViewModel { ReturnUrl = donus });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(MusteriKayitViewModel model)
        {
            model.ReturnUrl = LocalUrl(model.ReturnUrl);
            if (User.Identity?.IsAuthenticated == true)
                return model.ReturnUrl != null ? LocalRedirect(model.ReturnUrl) : RedirectToAction("Index", "Kesfet");
            if (!ModelState.IsValid) return View(model);

            if (!await _roles.RoleExistsAsync("Musteri"))
            {
                ModelState.AddModelError("", "Müşteri rolü sistemde tanımlı değil.");
                return View(model);
            }
            var email = model.Email.Trim();
            var user = new IdentityUser { UserName = email, Email = email, PhoneNumber = model.Telefon.Trim() };
            await using var transaction = await _db.Database.BeginTransactionAsync();
            var create = await _users.CreateAsync(user, model.Sifre);
            if (!create.Succeeded)
            {
                foreach (var error in create.Errors) ModelState.AddModelError("", error.Description);
                return View(model);
            }
            var roleResult = await _users.AddToRoleAsync(user, "Musteri");
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors) ModelState.AddModelError("", error.Description);
                return View(model);
            }
            _db.MusteriProfilleri.Add(new MusteriProfili
            {
                KullaniciId = user.Id,
                Ad = model.Ad.Trim(),
                Soyad = model.Soyad.Trim(),
                OlusturmaTarihi = DateTime.UtcNow
            });
            try
            {
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Müşteri profili kaydedilemedi. Lütfen tekrar deneyin.");
                return View(model);
            }
            if (await _users.IsEmailConfirmedAsync(user) ||
                (!_users.Options.SignIn.RequireConfirmedEmail && !_users.Options.SignIn.RequireConfirmedAccount))
            {
                await _signIn.SignInAsync(user, isPersistent: false);
                return model.ReturnUrl != null ? LocalRedirect(model.ReturnUrl) : RedirectToAction("Index", "Kesfet");
            }
            TempData["KayitBilgi"] = "Hesabınız oluşturuldu. Giriş yapmadan önce e-posta onayınız gerekebilir.";
            // Onay zorunluysa dönüş adresi giriş ekranında korunur.
            return RedirectToAction("Giris", "Hesap", new { girisTipi = "musteri", returnUrl = model.ReturnUrl });
        }

        private string? LocalUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Url.IsLocalUrl(url)) return null;
            // Randevuya dönüşte işletme kimliği zorunludur.
            if (url.StartsWith("/Randevu/Olustur", StringComparison.OrdinalIgnoreCase))
            {
                var soru = url.IndexOf('?');
                if (soru < 0) return null;
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(url.Substring(soru));
                if (!query.TryGetValue("isletmeId", out var idText) ||
                    !int.TryParse(idText.ToString(), out var id) || id <= 0) return null;
            }
            return url;
        }
    }

    public class MusteriKayitViewModel
    {
        [Required(ErrorMessage = "Adınızı girin.")]
        [StringLength(60)] public string Ad { get; set; } = "";
        [Required(ErrorMessage = "Soyadınızı girin.")]
        [StringLength(60)] public string Soyad { get; set; } = "";
        [Required(ErrorMessage = "E-posta adresinizi girin.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        public string Email { get; set; } = "";
        [Required(ErrorMessage = "Telefon numaranızı girin.")]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası girin.")]
        public string Telefon { get; set; } = "";
        [Required(ErrorMessage = "Şifrenizi girin.")]
        [DataType(DataType.Password)] public string Sifre { get; set; } = "";
        [Required(ErrorMessage = "Şifrenizi tekrar girin.")]
        [Compare(nameof(Sifre), ErrorMessage = "Şifreler eşleşmiyor.")]
        [DataType(DataType.Password)] public string SifreTekrar { get; set; } = "";
        public string? ReturnUrl { get; set; }
    }
}
