
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class IsletmeKullanicisi
    {
        public int Id { get; set; }

        public int IsletmeId { get; set; }

        // ASP.NET Core Identity kullanıcı ID'si
        public string KullaniciId { get; set; } = string.Empty;

        public string Rol { get; set; } = "IsletmeSahibi";

        public bool AktifMi { get; set; } = true;

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        public Isletme Isletme { get; set; } = null!;
    }
}
