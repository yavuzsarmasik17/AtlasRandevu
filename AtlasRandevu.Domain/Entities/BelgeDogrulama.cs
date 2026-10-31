
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class BelgeDogrulama
    {
        public int Id { get; set; }

        public int BelgeId { get; set; }

        public string Durum { get; set; } = "Bekliyor";

        public string? OcrSonucu { get; set; }

        public bool? BilgilerEslesiyorMu { get; set; }

        public string ResmiDogrulamaDurumu { get; set; }
            = "Yapilmadi";

        public string? Aciklama { get; set; }

        public DateTime KontrolTarihi { get; set; }
            = DateTime.UtcNow;

        public IsletmeBelgesi Belge { get; set; } = null!;
    }
}
