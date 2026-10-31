using System;
using System.ComponentModel.DataAnnotations;

namespace AtlasRandevu.Domain.Entities
{
    public class MusteriProfili
    {
        public int Id { get; set; }

        [Required, StringLength(450)]
        public string KullaniciId { get; set; } = string.Empty;

        [Required, StringLength(60)]
        public string Ad { get; set; } = string.Empty;

        [Required, StringLength(60)]
        public string Soyad { get; set; } = string.Empty;

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
    }
}
