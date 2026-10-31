
namespace AtlasRandevu.Mobile;

public partial class KesfetPage : ContentPage
{
    public KesfetPage()
    {
        InitializeComponent();
    }


    private async void Musteri_Clicked(
        object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            "//Musteri/MusteriAnaSayfa");
    }


    private async void Isletme_Clicked(
        object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Giris");
    }

    private async void Platform_Clicked(
        object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Giris");
    }
}
