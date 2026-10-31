
using AtlasRandevu.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Route("api/telefon-dogrulama")]
    [AutoValidateAntiforgeryToken]
    [EnableRateLimiting("sms")]
    public class TelefonDogrulamaController : Controller
    {
        private readonly TelefonDogrulamaService _service;
        private readonly IWebHostEnvironment _environment;

        public TelefonDogrulamaController(
            TelefonDogrulamaService service,
            IWebHostEnvironment environment)
        {
            _service = service;
            _environment = environment;
        }

        public class KodGonderRequest
        {
            [Required]
            [RegularExpression(@"^05\d{9}$")]
            public string Telefon { get; set; } = "";
        }

        public class KodDogrulaRequest
        {
            [Required]
            [RegularExpression(@"^05\d{9}$")]
            public string Telefon { get; set; } = "";

            [Required]
            [RegularExpression(@"^\d{6}$")]
            public string Kod { get; set; } = "";
        }

        private bool TestErisimiEngellendi()
        {
            if (!_environment.IsDevelopment())
                return false;

            var ip = HttpContext.Connection.RemoteIpAddress;

            return ip == null || !IPAddress.IsLoopback(ip);
        }

        private void DogrulamaOturumunuTemizle()
        {
            HttpContext.Session.Remove(
                "AtlasRandevu_DogrulananTelefon");

            HttpContext.Session.Remove(
                "AtlasRandevu_DogrulamaZamani");

            HttpContext.Session.Remove(
                "AtlasRandevu_DogrulamaId");
        }

        [HttpPost("kod-gonder")]
        public async Task<IActionResult> KodGonder(
            [FromForm] KodGonderRequest model)
        {
            if (TestErisimiEngellendi())
            {
                return StatusCode(403, new
                {
                    basarili = false,
                    mesaj = "Test SMS yalnızca yerel bilgisayarda kullanılabilir."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    basarili = false,
                    mesaj = "Geçerli bir telefon numarası giriniz."
                });
            }

            // Yeni SMS istendiğinde eski doğrulama
            // oturumunun tamamını temizle.
            DogrulamaOturumunuTemizle();

            var sonuc = await _service.KodGonderAsync(
                model.Telefon);

            return Json(new
            {
                basarili = sonuc,
                mesaj = sonuc
                    ? "Doğrulama kodu gönderildi."
                    : "Kod gönderilemedi. Daha sonra tekrar deneyiniz."
            });
        }

        [HttpPost("kod-dogrula")]
        public async Task<IActionResult> KodDogrula(
            [FromForm] KodDogrulaRequest model)
        {
            if (TestErisimiEngellendi())
            {
                return StatusCode(403, new
                {
                    basarili = false,
                    mesaj = "Test doğrulaması yalnızca yerel bilgisayarda kullanılabilir."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    basarili = false,
                    mesaj = "Telefon veya doğrulama kodu geçersiz."
                });
            }

            var dogrulamaId = await _service.KodDogrulaAsync(
                model.Telefon,
                model.Kod);

            bool sonuc = dogrulamaId.HasValue;

            if (sonuc)
            {
                // Doğrulanan telefon
                HttpContext.Session.SetString(
                    "AtlasRandevu_DogrulananTelefon",
                    model.Telefon);

                // Veritabanındaki doğrulama kaydının ID'si
                HttpContext.Session.SetInt32(
                    "AtlasRandevu_DogrulamaId",
                    dogrulamaId.Value);

                // Doğrulama zamanı
                HttpContext.Session.SetString(
                    "AtlasRandevu_DogrulamaZamani",
                    DateTimeOffset.UtcNow
                        .ToUnixTimeSeconds()
                        .ToString());
            }
            else
            {
                // Hatalı doğrulamada eski oturum
                // bilgileri geçerli kalmasın.
                DogrulamaOturumunuTemizle();
            }

            return Json(new
            {
                basarili = sonuc,
                mesaj = sonuc
                    ? "Telefon başarıyla doğrulandı."
                    : "Kod hatalı, kullanılmış veya süresi dolmuş."
            });
        }
    }
}
