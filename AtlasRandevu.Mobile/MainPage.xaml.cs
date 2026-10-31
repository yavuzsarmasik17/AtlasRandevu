
namespace AtlasRandevu.Mobile;

public partial class MainPage : ContentPage
{
    private bool _yonlendirildi;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_yonlendirildi)
            return;

        _yonlendirildi = true;

        await Task.Delay(2500);

        await Shell.Current.GoToAsync("//Kesfet");
    }
}
