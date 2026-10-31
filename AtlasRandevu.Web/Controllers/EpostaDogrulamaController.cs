
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Controllers
{
    [Route("api/eposta-dogrulama")]
    public class EpostaDogrulamaController : Controller
    {
        private readonly IEmailSender _emailSender;

        public EpostaDogrulamaController(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        [HttpPost("kod-gonder")]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("sms")]
        public async Task<IActionResult> KodGonder(
            [FromForm] string eposta)
        {
            eposta = (eposta ?? "").Trim().ToLowerInvariant();

            if (!new System.ComponentModel.DataAnnotations
                .EmailAddressAttribute().IsValid(eposta) ||
                eposta.Length > 254)
            {
                return BadRequest(new
                {
                    basarili = false,
                    mesaj = "Geçerli bir e-posta adresi giriniz."
                });
            }

            var sonGonderim = HttpContext.Session.GetString(
                "Atlas_Eposta_SonGonderim");

            if (long.TryParse(sonGonderim, out var sonZaman) &&
                DateTimeOffset.UtcNow -
                DateTimeOffset.FromUnixTimeSeconds(sonZaman)
                    < TimeSpan.FromSeconds(60))
            {
                return StatusCode(429, new
                {
                    basarili = false,
                    mesaj = "Yeni kod istemeden önce 60 saniye bekleyin."
                });
            }

            var kod = RandomNumberGenerator.GetInt32(
                0, 1000000).ToString("D6");

            var anahtar = RandomNumberGenerator.GetBytes(32);

            var hash = KodHash(anahtar, eposta, kod);

            // Yeni kod gönderilirken önceki doğrulama geçersizleşir.
            HttpContext.Session.Remove("Atlas_Eposta_Dogrulandi");

            var mesaj =
                "<h2>Atlas Randevu</h2>" +
                "<p>İşletme kaydı için doğrulama kodunuz:</p>" +
                "<h1>" + HtmlEncoder.Default.Encode(kod) + "</h1>" +
                "<p>Bu kod 5 dakika geçerlidir.</p>" +
                "<p>Kodu kimseyle paylaşmayın.</p>";

            try
            {
                await _emailSender.SendEmailAsync(
                    eposta,
                    "Atlas Randevu - E-posta Doğrulama",
                    mesaj);
            }
            catch
            {
                return StatusCode(503, new
                {
                    basarili = false,
                    mesaj = "E-posta gönderilemedi. Tekrar deneyin."
                });
            }

            HttpContext.Session.SetString(
                "Atlas_Eposta_Adres", eposta);
            HttpContext.Session.SetString(
                "Atlas_Eposta_Anahtar",
                Convert.ToBase64String(anahtar));
            HttpContext.Session.SetString(
                "Atlas_Eposta_Hash",
                Convert.ToBase64String(hash));
            HttpContext.Session.SetString(
                "Atlas_Eposta_SonGonderim",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            HttpContext.Session.SetInt32(
                "Atlas_Eposta_Deneme", 0);

            return Json(new
            {
                basarili = true,
                mesaj = "Doğrulama kodu e-posta adresinize gönderildi."
            });
        }

        [HttpPost("kod-dogrula")]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("sms")]
        public IActionResult KodDogrula(
            [FromForm] string eposta,
            [FromForm] string kod)
        {
            eposta = (eposta ?? "").Trim().ToLowerInvariant();
            kod = (kod ?? "").Trim();

            var kayitliEposta = HttpContext.Session.GetString(
                "Atlas_Eposta_Adres");

            var anahtarMetni = HttpContext.Session.GetString(
                "Atlas_Eposta_Anahtar");

            var hashMetni = HttpContext.Session.GetString(
                "Atlas_Eposta_Hash");

            var zamanMetni = HttpContext.Session.GetString(
                "Atlas_Eposta_SonGonderim");

            if (kayitliEposta != eposta ||
                string.IsNullOrEmpty(anahtarMetni) ||
                string.IsNullOrEmpty(hashMetni) ||
                !long.TryParse(zamanMetni, out var zaman) ||
                DateTimeOffset.UtcNow -
                DateTimeOffset.FromUnixTimeSeconds(zaman)
                    > TimeSpan.FromMinutes(5))
            {
                return Json(new
                {
                    basarili = false,
                    mesaj = "Kod geçersiz veya süresi dolmuş."
                });
            }

            var deneme = HttpContext.Session.GetInt32(
                "Atlas_Eposta_Deneme") ?? 0;

            if (deneme >= 5)
            {
                return Json(new
                {
                    basarili = false,
                    mesaj = "Deneme hakkınız doldu. Yeni kod isteyin."
                });
            }

            HttpContext.Session.SetInt32(
                "Atlas_Eposta_Deneme", deneme + 1);

            if (kod.Length != 6 ||
                !int.TryParse(kod, out _) ||
                !CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(hashMetni),
                    KodHash(
                        Convert.FromBase64String(anahtarMetni),
                        eposta,
                        kod)))
            {
                return Json(new
                {
                    basarili = false,
                    mesaj = "Doğrulama kodu hatalı."
                });
            }

            HttpContext.Session.SetString(
                "Atlas_Eposta_Dogrulandi", eposta);

            HttpContext.Session.SetString(
                "Atlas_Eposta_DogrulamaZamani",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

            return Json(new
            {
                basarili = true,
                mesaj = "E-posta adresiniz doğrulandı."
            });
        }

        private static byte[] KodHash(
            byte[] anahtar, string eposta, string kod)
        {
            using var hmac = new HMACSHA256(anahtar);

            return hmac.ComputeHash(
                Encoding.UTF8.GetBytes(eposta + ":" + kod));
        }
    }
}
