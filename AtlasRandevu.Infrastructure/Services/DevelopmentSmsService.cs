
using AtlasRandevu.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasRandevu.Infrastructure.Services
{
    public class DevelopmentSmsService : ISmsService
    {
        private readonly ILogger<DevelopmentSmsService> _logger;
        private readonly IHostEnvironment _environment;

        public DevelopmentSmsService(
            ILogger<DevelopmentSmsService> logger,
            IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public Task<bool> SendAsync(
            string telefon,
            string mesaj,
            CancellationToken cancellationToken = default)
        {
            // Canlı ortamda kesinlikle çalışmasın.
            if (!_environment.IsDevelopment())
            {
                return Task.FromResult(false);
            }

            _logger.LogWarning(
                "ATLAS RANDEVU TEST SMS | Telefon: {Telefon} | Mesaj: {Mesaj}",
                telefon,
                mesaj);

            return Task.FromResult(true);
        }
    }
}
