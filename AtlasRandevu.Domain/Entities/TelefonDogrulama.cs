
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class TelefonDogrulama
    {
        public int Id { get; set; }

        public string Telefon { get; set; } = string.Empty;

        // Doğrulama kodunun kendisi değil, güvenli özeti tutulur.
        public string KodOzeti { get; set; } = string.Empty;

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

        public DateTime SonGecerlilikTarihi { get; set; }

        public int HataliDenemeSayisi { get; set; }

        public bool KullanildiMi { get; set; }

        public DateTime? DogrulamaTarihi { get; set; }

        public bool BasvurudaKullanildiMi { get; set; } = false;

        public DateTime? BasvurudaKullanilmaTarihi { get; set; }

    }
}
