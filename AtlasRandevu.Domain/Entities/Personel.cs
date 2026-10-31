
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class Personel
    {
        public int Id { get; set; }

        public int IsletmeId { get; set; }

        public int SubeId { get; set; }

        public string AdSoyad { get; set; } = "";

        public string Unvan { get; set; } = "";

        public string? Telefon { get; set; }

        public string? FotografYolu { get; set; }

        public bool AktifMi { get; set; } = true;

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        public Isletme Isletme { get; set; } = null!;

        public Sube Sube { get; set; } = null!;
    }
}
