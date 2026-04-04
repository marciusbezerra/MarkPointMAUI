namespace MarkPointMAUI
{
    using Microsoft.Maui.Devices.Sensors;
    using Microsoft.Maui.ApplicationModel;
    using System.Text;
    using Microsoft.Maui.Controls;
    using System.Collections.ObjectModel;

    public class MainPageViewModel
    {
        public ObservableCollection<string> Points { get; } = [];
    }

    public partial class MainPage : ContentPage
    {
        private MainPageViewModel viewModel = new MainPageViewModel();

        public MainPage()
        {
            InitializeComponent();
            this.BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    return;

                var request = new GeolocationRequest(GeolocationAccuracy.Best);
                var location = await Geolocation.GetLocationAsync(request);
                // Load the HTML map and center at current location
                var html = BuildMapHtml(location?.Latitude ?? 0, location?.Longitude ?? 0);
                MapView.Source = new HtmlWebViewSource { Html = html };

                if (location != null)
                {
                    var coord = $"{location.Latitude:F6}, {location.Longitude:F6}";
                    // add initial marker via JS after a short delay to ensure the page loaded
                    await Task.Delay(500);
                    await MapView.EvaluateJavaScriptAsync($"addMarker({location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Erro", $"Ocorreu um erro: {ex.Message}", "OK");
            }
        }

        private async void OnMarkPointClicked(object sender, EventArgs e)
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    return;

                var request = new GeolocationRequest(GeolocationAccuracy.Best);
                var location = await Geolocation.GetLocationAsync(request);
                if (location != null)
                {
                    var text = $"{location.Latitude:F6}, {location.Longitude:F6}";
                    viewModel.Points.Add(text);
                    await DisplayAlertAsync("Atenção", "Seu ponto foi salvo com sucesso!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Erro", $"Ocorreu um erro: {ex.Message}", "OK");
            }
        }

        string BuildMapHtml(double lat, double lon)
        {
            // Minimal Leaflet map with a JS function to add markers
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"/>\n");
            html.AppendLine("<link rel=\"stylesheet\" href=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.css\"/>\n");
            html.AppendLine("<style>html,body,#map{height:100%;margin:0;padding:0}</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<div id=\"map\"></div>");
            html.AppendLine("<script src=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.js\"></script>");
            html.AppendLine("<script>");
            html.AppendLine($"var map = L.map('map').setView([{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}], 15);");
            html.AppendLine("L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19,attribution:'© OpenStreetMap'}).addTo(map);");
            html.AppendLine("function addMarker(lat, lon){ L.marker([lat, lon]).addTo(map); map.setView([lat, lon]); }");
            html.AppendLine("</script>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }
    }
}
