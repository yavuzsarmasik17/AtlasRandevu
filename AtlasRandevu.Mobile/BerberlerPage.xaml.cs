
using System.Collections.ObjectModel;

namespace AtlasRandevu.Mobile;

public partial class BerberlerPage : ContentPage
{
    private readonly List<BerberModel> _tumBerberler;

    public BerberlerPage()
    {
        InitializeComponent();

        // Þimdilik örnek veriler.
        // Sonrasýnda API'den getirilecek.
        _tumBerberler = new List<BerberModel>
        {
            new()
            {
                Ad = "Atlas Erkek Kuaförü",
                Konum = "Arnavutköy, Ýstanbul",
                Aciklama = "Saç kesimi, sakal týraþý ve bakým.",
                Puan = "? 4.9"
            },
            new()
            {
                Ad = "Modern Barber",
                Konum = "Hadýmköy, Ýstanbul",
                Aciklama = "Modern saç kesimi ve erkek bakýmý.",
                Puan = "? 4.8"
            },
            new()
            {
                Ad = "Premium Erkek Kuaförü",
                Konum = "Çatalca, Ýstanbul",
                Aciklama = "Profesyonel saç ve sakal hizmetleri.",
                Puan = "? 4.7"
            }
        };

        BerberListesi.ItemsSource =
            new ObservableCollection<BerberModel>(_tumBerberler);
    }

    private void TxtAra_TextChanged(
        object sender, TextChangedEventArgs e)
    {
        string arama = e.NewTextValue?.Trim() ?? "";

        var sonuc = _tumBerberler
            .Where(x =>
                x.Ad.Contains(arama,
                    StringComparison.OrdinalIgnoreCase) ||
                x.Konum.Contains(arama,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        BerberListesi.ItemsSource =
            new ObservableCollection<BerberModel>(sonuc);
    }

    private async void Incele_Clicked(
        object sender, EventArgs e)
    {
        if (sender is Button button &&
            button.CommandParameter is BerberModel berber)
        {
            await DisplayAlert(
                berber.Ad,
                "Ýþletme detay ve randevu ekranýný " +
                "bir sonraki aþamada oluþturacaðýz.",
                "Tamam");
        }
    }
}

public class BerberModel
{
    public string Ad { get; set; } = "";
    public string Konum { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public string Puan { get; set; } = "";
}
