using System;

namespace AtlasRandevu.Domain.Entities
{
    public class IsletmeBildirimi
    {
        public int Id { get; set; }
        public int IsletmeId { get; set; }
        public int? RandevuId { get; set; }
        public string Tur { get; set; } = "";
        public string Baslik { get; set; } = "";
        public string Mesaj { get; set; } = "";
        public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
        public bool OkunduMu { get; set; }
        public DateTime? OkunmaTarihi { get; set; }
    }
}