
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRandevu.Web.Controllers
{
    [ApiController]
    [Route("api/mobil")]
    public class MobilController : ControllerBase
    {
        private readonly AtlasRandevuDbContext _db;

        public MobilController(AtlasRandevuDbContext db)
        {
            _db = db;
        }

        // Mobil API bağlantı testi
        [HttpGet("durum")]
        [AllowAnonymous]
        public IActionResult Durum()
        {
            return Ok(new
            {
                basarili = true,
                uygulama = "ATLAS Randevu",
                mesaj = "Mobil API bağlantısı başarılı",
                sunucuZamani = DateTime.UtcNow
            });
        }

        // Onaylı işletmeleri listele
        [HttpGet("isletmeler")]
        [AllowAnonymous]
        public async Task<IActionResult> Isletmeler()
        {
            var isletmeler = await _db.Isletmeler
                .AsNoTracking()
                .Where(x => x.Durum == "Onaylandi")
                .OrderBy(x => x.Ad)
                .Select(x => new
                {
                    id = x.Id,
                    ad = x.Ad,
                    hakkimizda = x.Hakkimizda,
                    slogan = x.Slogan,
                    logoYolu = x.LogoYolu,
                    kapakFotografiYolu = x.KapakFotografiYolu,
                    webSitesi = x.WebSitesi,
                    instagramAdresi = x.InstagramAdresi
                })
                .ToListAsync();

            return Ok(new
            {
                basarili = true,
                toplam = isletmeler.Count,
                isletmeler
            });
        }
    }
}
