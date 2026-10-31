
using AtlasRandevu.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasRandevu.Infrastructure.Services
{
    public class SmsService : ISmsService
    {
        public Task<bool> SendAsync(
            string telefon,
            string mesaj,
            CancellationToken cancellationToken = default)
        {
            // SMS sağlayıcısı henüz bağlanmadı.
            // Gönderilmediği için false döndürüyoruz.
            return Task.FromResult(false);
        }
    }
}
