
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class IsletmeBelgesi
    {
        public int Id { get; set; }

        public int IsletmeId { get; set; }

        public string BelgeTuru { get; set; } = "VergiLevhasi";

        public string DosyaYolu { get; set; } = string.Empty;

        public string DosyaAdi { get; set; } = string.Empty;

        public string IcerikTuru { get; set; } = string.Empty;

        public DateTime YuklemeTarihi { get; set; }
            = DateTime.UtcNow;

        public Isletme Isletme { get; set; } = null!;
    }
}
