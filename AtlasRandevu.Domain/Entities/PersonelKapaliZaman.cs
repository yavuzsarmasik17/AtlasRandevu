using System;

namespace AtlasRandevu.Domain.Entities
{
    public class PersonelKapaliZaman
    {
        public int Id { get; set; }

        public int IsletmeId { get; set; }

        public int SubeId { get; set; }

        public int PersonelId { get; set; }

        public DateTime BaslangicTarihi { get; set; }

        public DateTime BitisTarihi { get; set; }

        public string? Aciklama { get; set; }

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;
    }
}