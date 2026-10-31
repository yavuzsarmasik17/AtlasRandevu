
namespace AtlasRandevu.Mobile;

public partial class HesabimPage : ContentPage
{
    public HesabimPage()
    {
        InitializeComponent();
    }

    private async void Giris_Clicked(
        object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Giris");
    }
}
