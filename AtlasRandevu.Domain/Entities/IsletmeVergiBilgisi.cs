
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class IsletmeVergiBilgisi
    {
        public int Id { get; set; }

        public int IsletmeId { get; set; }

        public string VergiNumarasi { get; set; } = string.Empty;

        public string VergiDairesi { get; set; } = string.Empty;

        public string TicariUnvan { get; set; } = string.Empty;

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        public Isletme Isletme { get; set; } = null!;
    }
}
