
using System;
using System.Collections.Generic;

namespace AtlasRandevu.Domain.Entities
{
    public class Isletme
    {
        public int Id { get; set; }

        public string Ad { get; set; } = string.Empty;

        public string TicariUnvan { get; set; } = string.Empty;

        public int SektorId { get; set; }

        public Sektor Sektor { get; set; } = null!;

        public string Durum { get; set; } = "Taslak";

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        public DateTime? OnayTarihi { get; set; }

        public string? OnaylayanKullaniciId { get; set; }

        // İŞLETME TANITIM BİLGİLERİ

        // İşletmenin müşterilere gösterilecek açıklaması
        public string? Hakkimizda { get; set; }

        // İşletme logosunun dosya yolu
        public string? LogoYolu { get; set; }

        // İşletme kapak fotoğrafının dosya yolu
        public string? KapakFotografiYolu { get; set; }

        // İşletmenin web sitesi
        public string? WebSitesi { get; set; }

        // Sosyal medya hesabı
        public string? InstagramAdresi { get; set; }

        // İşletme tarafından belirtilen slogan
        public string? Slogan { get; set; }

        // Profil bilgilerinin son güncellenme tarihi
        public DateTime? ProfilGuncellemeTarihi { get; set; }

        // MEVCUT ŞUBELER

        public ICollection<Sube> Subeler { get; set; }
            = new List<Sube>();
    }
}
