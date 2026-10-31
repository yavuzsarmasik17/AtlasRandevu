
namespace AtlasRandevu.Domain.Entities
{
    public class PushSubscription
    {
        public int Id { get; set; }

        // Bildirimi alacak işletme
        public int IsletmeId { get; set; }

        // Bildirim izni veren kullanıcı
        public string KullaniciId { get; set; } = string.Empty;

        // Tarayıcının bildirim adresi
        public string Endpoint { get; set; } = string.Empty;

        // Web Push şifreleme anahtarları
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;

        public DateTime OlusturmaTarihi { get; set; }
            = DateTime.UtcNow;

        public bool AktifMi { get; set; } = true;
    }
}
