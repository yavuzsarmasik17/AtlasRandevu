
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class MusteriYorumu
    {
        public int Id { get; set; }

        // Yorumun ait olduğu işletme
        public int IsletmeId { get; set; }

        // Yorumun bağlı olduğu randevu
        public int RandevuId { get; set; }

        // Yorumu yapan müşteri
        public string MusteriKullaniciId { get; set; } = string.Empty;

        // 1 ile 5 arasında yıldız puanı
        public int Puan { get; set; }

        // Müşterinin yorumu
        public string Yorum { get; set; } = string.Empty;

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        // İşletme sahibinin yoruma cevabı
        public string? IsletmeCevabi { get; set; }

        public DateTime? CevapTarihi { get; set; }

        // Platform yöneticisi gerektiğinde gizleyebilir
        public bool YayinlandiMi { get; set; } = true;

        // İlişkiler
        public Isletme Isletme { get; set; } = null!;

        public Randevu Randevu { get; set; } = null!;
    }
}
