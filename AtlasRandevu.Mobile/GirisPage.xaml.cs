
namespace AtlasRandevu.Mobile;

public partial class GirisPage : ContentPage
{
    public GirisPage()
    {
        InitializeComponent();
    }

    private async void GirisYap_Clicked(
        object sender,
        EventArgs e)
    {
        string email = EmailEntry.Text?.Trim() ?? "";
        string sifre = PasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(sifre))
        {
            await DisplayAlert(
                "Eksik Bilgi",
                "Lütfen e-posta ve þifrenizi girin.",
                "Tamam");

            return;
        }

        await DisplayAlert(
            "ATLAS Randevu",
            "Giriþ ekraný hazýr. Sunucu baðlantýsýný sonraki adýmda kuracaðýz.",
            "Tamam");
    }
}
