using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using AtlasRandevu.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.RegularExpressions;

namespace AtlasRandevu.Web.Controllers
{
    public class IsletmeKayitController : Controller
    {
        private readonly AtlasRandevuDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<IsletmeKayitController> _logger;

        public IsletmeKayitController(AtlasRandevuDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<IsletmeKayitController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await SektorleriYukle();
            return View(new IsletmeKayitViewModel());
        }

        private async Task SektorleriYukle()
        {
            ViewBag.Sektorler = await _context.Sektorler.AsNoTracking()
                .Where(x => x.AktifMi).OrderBy(x => x.Sira).ToListAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(IsletmeKayitViewModel model, bool sozlesmeKabulEdildi)
        {
            if (!sozlesmeKabulEdildi)
                ModelState.AddModelError("", "İşletme Kullanım Sözleşmesi kabul edilmelidir.");
            if (!ModelState.IsValid) return await HataliForm(model);

            model.Telefon = model.Telefon.Trim();
            model.Eposta = model.Eposta.Trim();

            if (!Regex.IsMatch(model.Telefon, @"^05\d{9}$"))
            {
                ModelState.AddModelError(nameof(model.Telefon),
                    "Telefon 05XXXXXXXXX biçiminde olmalıdır.");
                return await HataliForm(model);
            }

            if (!await _context.Sektorler.AnyAsync(x => x.Id == model.SektorId && x.AktifMi))
            {
                ModelState.AddModelError(nameof(model.SektorId), "Geçerli bir sektör seçiniz.");
                return await HataliForm(model);
            }

            if (await _userManager.FindByEmailAsync(model.Eposta) != null)
            {
                ModelState.AddModelError(nameof(model.Eposta), "Bu e-posta adresi zaten kayıtlı.");
                return await HataliForm(model);
            }

            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var kullanici = new IdentityUser
                    {
                        UserName = model.Eposta,
                        Email = model.Eposta,
                        PhoneNumber = model.Telefon,
                        PhoneNumberConfirmed = false,
                        EmailConfirmed = false
                    };

                    var identitySonuc = await _userManager.CreateAsync(kullanici, model.Sifre);
                    if (!identitySonuc.Succeeded)
                    {
                        foreach (var hata in identitySonuc.Errors)
                            ModelState.AddModelError("", hata.Description);
                        await transaction.RollbackAsync();
                        return await HataliForm(model);
                    }

                    var isletme = new Isletme
                    {
                        Ad = model.IsletmeAdi.Trim(),
                        TicariUnvan = model.TicariUnvan.Trim(),
                        SektorId = model.SektorId,
                        Durum = "OnayBekliyor",
                        OlusturmaTarihi = DateTime.UtcNow
                    };
                    _context.Isletmeler.Add(isletme);
                    await _context.SaveChangesAsync();

                    var sube = new Sube
                    {
                        IsletmeId = isletme.Id,
                        Ad = model.SubeAdi.Trim(),
                        Il = model.Il.Trim(),
                        Ilce = model.Ilce.Trim(),
                        Adres = model.Adres.Trim(),
                        Telefon = model.SubeTelefon.Trim(),
                        Durum = "Taslak",
                        OlusturmaTarihi = DateTime.UtcNow
                    };

                    var isletmeKullanicisi = new IsletmeKullanicisi
                    {
                        IsletmeId = isletme.Id,
                        KullaniciId = kullanici.Id,
                        Rol = "IsletmeSahibi",
                        AktifMi = true,
                        OlusturmaTarihi = DateTime.UtcNow
                    };

                    _context.Subeler.Add(sube);
                    _context.IsletmeKullanicilari.Add(isletmeKullanicisi);
                    await _context.SaveChangesAsync();

                    // Kayıtla aynı transaction: hata halinde her şey geri alınır.
                    await _context.Database.ExecuteSqlInterpolatedAsync($@"
                        INSERT INTO dbo.SozlesmeKabulleri
                        (IsletmeId, KullaniciId, SozlesmeSurumu, KvkkAydinlatmaSurumu, KabulTarihiUtc)
                        VALUES ({isletme.Id}, {kullanici.Id}, {"1.0"}, {"1.0"}, {DateTime.UtcNow})");

                    await transaction.CommitAsync();
                    return RedirectToAction("Basarili");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İşletme kaydı oluşturulamadı.");
                ModelState.AddModelError("", "Başvuru kaydedilemedi. Lütfen tekrar deneyiniz.");
                return await HataliForm(model);
            }
        }

        private async Task<IActionResult> HataliForm(IsletmeKayitViewModel model)
        {
            await SektorleriYukle();
            return View(model);
        }

        [HttpGet]
        public IActionResult Basarili() => View();
    }
}
