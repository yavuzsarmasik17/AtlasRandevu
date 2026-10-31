
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class Sube
    {
        public int Id { get; set; }

        public int IsletmeId { get; set; }

        public string Ad { get; set; } = string.Empty;

        public string Il { get; set; } = string.Empty;

        public string Ilce { get; set; } = string.Empty;

        public string Adres { get; set; } = string.Empty;

        public string Telefon { get; set; } = string.Empty;

        public string Durum { get; set; } = "Taslak";

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        public Isletme Isletme { get; set; } = null!;
    }
}
