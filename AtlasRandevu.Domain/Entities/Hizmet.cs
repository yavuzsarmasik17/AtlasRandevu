
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class Hizmet
    {
        public int Id { get; set; }

        // Hizmetin ait olduğu işletme
        public int IsletmeId { get; set; }

        // Örnek: Saç Kesimi, Fön, Cilt Bakımı
        public string Ad { get; set; } = string.Empty;

        public string Aciklama { get; set; } = string.Empty;

        // TL cinsinden hizmet fiyatı
        public decimal Fiyat { get; set; }

        // Hizmetin süresi (dakika)
        public int SureDakika { get; set; }

        // İşletme hizmeti yayından kaldırabilir
        public bool AktifMi { get; set; } = true;

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        public Isletme Isletme { get; set; } = null!;
    }
}
