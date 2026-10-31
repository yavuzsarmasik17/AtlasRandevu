
using System.Collections.Generic;

namespace AtlasRandevu.Domain.Entities
{
    public class Sektor
    {
        public int Id { get; set; }

        public string Ad { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public bool AktifMi { get; set; } = true;

        public int Sira { get; set; }

        public ICollection<Isletme> Isletmeler { get; set; }
            = new List<Isletme>();
    }
}
