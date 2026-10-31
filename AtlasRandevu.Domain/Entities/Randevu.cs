
using System;

namespace AtlasRandevu.Domain.Entities
{
    public class Randevu
    {
        public int Id { get; set; }

        // İşletme ve şube
        public int IsletmeId { get; set; }

        public int SubeId { get; set; }

        // Hizmet
        public int HizmetId { get; set; }

        // Randevuyu gerçekleştirecek personel
        public int? PersonelId { get; set; }

        public Personel? Personel { get; set; }

        // Müşteri
        // Online randevuda kullanıcı ID'si bulunur.
        // Manuel randevuda boş olabilir.
        public string? MusteriKullaniciId { get; set; }

        // Randevu kaynağı:
        // Online, Telefon, Elden
        public string RandevuKaynagi { get; set; } = "Online";

        // Telefonla veya elden alınan randevular
        public string? ManuelMusteriAdi { get; set; }

        public string? ManuelMusteriTelefonu { get; set; }

        // Randevu tarihleri
        public DateTime BaslangicTarihi { get; set; }

        public DateTime BitisTarihi { get; set; }

        // Randevu durumları:
        // OnayBekliyor
        // Onaylandi
        // Geldi
        // Gelmedi
        // Reddedildi
        // IptalEdildi
        // Tamamlandi
        public string Durum { get; set; }
            = "OnayBekliyor";

        // Açıklamalar
        public string? MusteriNotu { get; set; }

        public string? IsletmeAciklamasi { get; set; }

        // Kayıt tarihleri
        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        public DateTime? OnayTarihi { get; set; }

        public DateTime? TamamlanmaTarihi { get; set; }

        // =====================================
        // ORTAK QR İLE MÜŞTERİ GİRİŞ SİSTEMİ
        // =====================================

        // Müşterinin QR okuttuğu zaman
        public DateTime? GelisTarihi { get; set; }

        // Giriş yöntemi: OrtakQR
        public string? GelisYontemi { get; set; }

        // QR ile giriş yapıldı mı?
        public bool GelisDogrulandiMi { get; set; }
            = false;

        // =====================================
        // İLİŞKİLER
        // =====================================

        public Isletme Isletme { get; set; } = null!;

        public Sube Sube { get; set; } = null!;

        public Hizmet Hizmet { get; set; } = null!;
    }
}
