using System.ComponentModel.DataAnnotations;

namespace AtlasRandevu.Web.ViewModels
{
    public class IsletmeKayitViewModel
    {
        [Required(ErrorMessage = "Ad soyad zorunludur.")]
        [Display(Name = "Ad Soyad")]
        public string AdSoyad { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefon zorunludur.")]
        [Phone]
        public string Telefon { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Eposta { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [DataType(DataType.Password)]
        public string Sifre { get; set; } = string.Empty;

        [Required]
        [Display(Name = "İşletme Adı")]
        public string IsletmeAdi { get; set; } = string.Empty;

        [Required]
        public string TicariUnvan { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int SektorId { get; set; }

        [Required]
        public string SubeAdi { get; set; } = string.Empty;

        [Required]
        public string Il { get; set; } = string.Empty;

        [Required]
        public string Ilce { get; set; } = string.Empty;

        [Required]
        public string Adres { get; set; } = string.Empty;

        [Required]
        public string SubeTelefon { get; set; } = string.Empty;
    }
}
