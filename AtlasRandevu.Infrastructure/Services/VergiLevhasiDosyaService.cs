
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasRandevu.Infrastructure.Services
{
    public class VergiLevhasiDosyaService
    {
        private const long MaksimumBoyut = 5 * 1024 * 1024;

        private readonly string _depolamaKlasoru;

        public VergiLevhasiDosyaService(
            IConfiguration configuration)
        {
            var klasor = configuration["BelgeDepolama:Klasor"];

            if (string.IsNullOrWhiteSpace(klasor) ||
                !Path.IsPathFullyQualified(klasor))
            {
                throw new InvalidOperationException(
                    "BelgeDepolama:Klasor mutlak bir dizin olmalıdır.");
            }

            _depolamaKlasoru = Path.GetFullPath(klasor);
        }

        public async Task<string> KaydetAsync(
            IFormFile dosya,
            CancellationToken cancellationToken = default)
        {
            if (dosya == null ||
                dosya.Length == 0 ||
                dosya.Length > MaksimumBoyut)
            {
                throw new InvalidOperationException(
                    "Dosya boyutu 5 MB'ı geçemez ve boş olamaz.");
            }

            var uzanti = Path.GetExtension(dosya.FileName)
                .ToLowerInvariant();

            if (uzanti != ".pdf" &&
                uzanti != ".jpg" &&
                uzanti != ".jpeg" &&
                uzanti != ".png")
            {
                throw new InvalidOperationException(
                    "Yalnızca PDF, JPG ve PNG kabul edilir.");
            }

            await using var kaynak = dosya.OpenReadStream();

            var imza = new byte[8];

            var okunan = await kaynak.ReadAsync(
                imza.AsMemory(0, imza.Length),
                cancellationToken);

            if (okunan < 8)
                throw new InvalidOperationException(
                    "Dosya biçimi geçersiz.");

            bool pdf = imza[0] == 0x25 &&
                       imza[1] == 0x50 &&
                       imza[2] == 0x44 &&
                       imza[3] == 0x46 &&
                       imza[4] == 0x2D;

            bool jpg = imza[0] == 0xFF &&
                       imza[1] == 0xD8 &&
                       imza[2] == 0xFF;

            bool png = imza[0] == 0x89 &&
                       imza[1] == 0x50 &&
                       imza[2] == 0x4E &&
                       imza[3] == 0x47 &&
                       imza[4] == 0x0D &&
                       imza[5] == 0x0A &&
                       imza[6] == 0x1A &&
                       imza[7] == 0x0A;

            bool gecerli =
                (uzanti == ".pdf" && pdf) ||
                ((uzanti == ".jpg" || uzanti == ".jpeg") && jpg) ||
                (uzanti == ".png" && png);

            if (!gecerli)
                throw new InvalidOperationException(
                    "Dosyanın içeriği ile uzantısı uyuşmuyor.");

            Directory.CreateDirectory(_depolamaKlasoru);

            var dosyaAdi =
                Guid.NewGuid().ToString("N") + uzanti;

            var tamYol = Path.Combine(
                _depolamaKlasoru,
                dosyaAdi);

            kaynak.Position = 0;

            try
            {
                await using var hedef = new FileStream(
                    tamYol,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

                await kaynak.CopyToAsync(
                    hedef,
                    cancellationToken);
            }
            catch
            {
                if (File.Exists(tamYol))
                    File.Delete(tamYol);

                throw;
            }

            return tamYol;
        }
    }
}
