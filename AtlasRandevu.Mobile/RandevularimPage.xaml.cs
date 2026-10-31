
namespace AtlasRandevu.Mobile;

public partial class RandevularimPage : ContentPage
{
    public RandevularimPage()
    {
        InitializeComponent();
    }

    private async void Kesfet_Clicked(
        object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            "//Musteri/MusteriAnaSayfa");
    }
}
