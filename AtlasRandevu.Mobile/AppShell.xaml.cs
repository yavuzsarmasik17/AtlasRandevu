
namespace AtlasRandevu.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("Giris", typeof(GirisPage));
        Routing.RegisterRoute("Berberler", typeof(BerberlerPage));
    }
}
