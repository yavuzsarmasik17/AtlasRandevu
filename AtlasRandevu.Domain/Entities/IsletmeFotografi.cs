
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class IsletmeFotografi
    {
        public int Id { get; set; }

        public int IsletmeId { get; set; }

        public Isletme Isletme { get; set; } = null!;

        public string DosyaYolu { get; set; } = string.Empty;

        public DateTime YuklenmeTarihi { get; set; }
            = DateTime.UtcNow;

        public int Sira { get; set; }
    }
}
