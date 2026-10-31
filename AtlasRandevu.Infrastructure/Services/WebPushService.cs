
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;

namespace AtlasRandevu.Infrastructure.Services
{
    public class WebPushService
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebPushService> _logger;

        public WebPushService(
            AtlasRandevuDbContext db,
            IConfiguration configuration,
            ILogger<WebPushService> logger)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task BildirimGonderAsync(
            int isletmeId,
            string baslik,
            string mesaj,
            string url = "/IsletmePanel")
        {
            var publicKey = _configuration["WebPush:PublicKey"];
            var privateKey = _configuration["WebPush:PrivateKey"];
            var subject = _configuration["WebPush:Subject"];

            if (string.IsNullOrWhiteSpace(publicKey) ||
                string.IsNullOrWhiteSpace(privateKey) ||
                string.IsNullOrWhiteSpace(subject))
            {
                _logger.LogWarning(
                    "Web Push VAPID yapılandırması eksik.");
                return;
            }

            var abonelikler = await _db.PushSubscriptions
                .AsNoTracking()
                .Where(x => x.IsletmeId == isletmeId &&
                            x.AktifMi)
                .ToListAsync();

            var vapidDetails = new VapidDetails(
                subject, publicKey, privateKey);

            var client = new WebPushClient();

            var payload = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    title = baslik,
                    body = mesaj,
                    url = url
                });

            foreach (var abonelik in abonelikler)
            {
                try
                {
                    var subscription = new WebPush.PushSubscription(
                        abonelik.Endpoint,
                        abonelik.P256dh,
                        abonelik.Auth);

                    await client.SendNotificationAsync(
                        subscription,
                        payload,
                        vapidDetails);
                }
                catch (WebPushException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Web Push gönderilemedi. Abonelik ID: {Id}",
                        abonelik.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Web Push beklenmeyen hata. Abonelik ID: {Id}",
                        abonelik.Id);
                }
            }
        }
    }
}
