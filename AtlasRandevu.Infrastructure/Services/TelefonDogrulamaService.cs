
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AtlasRandevu.Application.Interfaces;
using AtlasRandevu.Domain.Entities;
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AtlasRandevu.Infrastructure.Services
{
    public class TelefonDogrulamaService
    {
        private readonly AtlasRandevuDbContext _context;
        private readonly ISmsService _smsService;
        private readonly byte[] _secret;

        public TelefonDogrulamaService(
            AtlasRandevuDbContext context,
            ISmsService smsService,
            IConfiguration configuration)
        {
            _context = context;
            _smsService = smsService;

            var secret = configuration["SmsVerification:Secret"];

            if (string.IsNullOrWhiteSpace(secret) ||
                Encoding.UTF8.GetByteCount(secret) < 32)
            {
                throw new InvalidOperationException(
                    "SmsVerification:Secret en az 32 bayt olmalıdır.");
            }

            _secret = Encoding.UTF8.GetBytes(secret);
        }

        private string KodOzetiOlustur(
            string telefon,
            string kod)
        {
            using var hmac = new HMACSHA256(_secret);

            var veri = Encoding.UTF8.GetBytes(
                telefon + ":" + kod);

            return Convert.ToHexString(
                hmac.ComputeHash(veri));
        }

        public async Task<bool> KodGonderAsync(
            string telefon,
            CancellationToken cancellationToken = default)
        {
            telefon = telefon.Trim();

            // Türkiye cep telefonu: 05XXXXXXXXX
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                telefon, @"^05\d{9}$"))
                return false;

            var simdi = DateTime.UtcNow;

            // Son 60 saniyede tekrar kod gönderilmesin.
            var sonKayit = await _context.TelefonDogrulamalari
                .Where(x => x.Telefon == telefon)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .FirstOrDefaultAsync(cancellationToken);

            if (sonKayit != null &&
                sonKayit.OlusturmaTarihi > simdi.AddSeconds(-60))
                return false;

            // Bir saatte en fazla 5 istek.
            var saatlikIstek = await _context.TelefonDogrulamalari
                .CountAsync(x =>
                    x.Telefon == telefon &&
                    x.OlusturmaTarihi > simdi.AddHours(-1),
                    cancellationToken);

            if (saatlikIstek >= 5)
                return false;

            var kod = RandomNumberGenerator
                .GetInt32(0, 1000000)
                .ToString("D6");

            var kayit = new TelefonDogrulama
            {
                Telefon = telefon,
                KodOzeti = KodOzetiOlustur(telefon, kod),
                OlusturmaTarihi = simdi,
                SonGecerlilikTarihi = simdi.AddMinutes(3),
                HataliDenemeSayisi = 0,
                KullanildiMi = false
            };

            _context.TelefonDogrulamalari.Add(kayit);
            await _context.SaveChangesAsync(cancellationToken);

            var gonderildi = await _smsService.SendAsync(
                telefon,
                $"Atlas Randevu dogrulama kodunuz: {kod}",
                cancellationToken);

            // SMS gönderilmediyse kod kullanılamaz.
            if (!gonderildi)
            {
                kayit.KullanildiMi = true;
                await _context.SaveChangesAsync(cancellationToken);
                return false;
            }

            return true;
        }


        public async Task<int?> KodDogrulaAsync(
            string telefon,
            string kod,
            CancellationToken cancellationToken = default)
        {
            telefon = telefon.Trim();

            if (!System.Text.RegularExpressions.Regex.IsMatch(
                telefon, @"^05\d{9}$"))
                return null;

            if (!System.Text.RegularExpressions.Regex.IsMatch(
                kod, @"^\d{6}$"))
                return null;

            var simdi = DateTime.UtcNow;

            var kayit = await _context.TelefonDogrulamalari
                .Where(x =>
                    x.Telefon == telefon &&
                    !x.KullanildiMi &&
                    x.SonGecerlilikTarihi > simdi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .FirstOrDefaultAsync(cancellationToken);

            if (kayit == null || kayit.HataliDenemeSayisi >= 5)
                return null;

            var beklenen = Convert.FromHexString(kayit.KodOzeti);

            var girilen = Convert.FromHexString(
                KodOzetiOlustur(telefon, kod));

            var dogru = CryptographicOperations.FixedTimeEquals(
                beklenen, girilen);

            if (!dogru)
            {
                kayit.HataliDenemeSayisi++;

                if (kayit.HataliDenemeSayisi >= 5)
                    kayit.KullanildiMi = true;

                await _context.SaveChangesAsync(cancellationToken);
                return null;
            }

            kayit.KullanildiMi = true;
            kayit.DogrulamaTarihi = simdi;

            await _context.SaveChangesAsync(cancellationToken);

            return kayit.Id;
        }

    }
}
