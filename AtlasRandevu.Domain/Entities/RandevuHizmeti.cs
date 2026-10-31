
namespace AtlasRandevu.Domain.Entities
{
    public class RandevuHizmeti
    {
        public int Id { get; set; }

        public int RandevuId { get; set; }

        public int HizmetId { get; set; }

        // Randevu anındaki fiyat ve süre saklanır.
        // İşletme sonradan fiyat değiştirse bile
        // eski randevunun bilgileri korunur.
        public decimal Fiyat { get; set; }

        public int SureDakika { get; set; }

        public Randevu Randevu { get; set; } = null!;

        public Hizmet Hizmet { get; set; } = null!;
    }
}
