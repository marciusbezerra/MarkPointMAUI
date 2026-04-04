namespace MarkPointMAUI
{
    using Microsoft.Maui.Devices.Sensors;
    using Microsoft.Maui.ApplicationModel;
    using System.Text;
    using Microsoft.Maui.Controls;
    using System.Collections.ObjectModel;
    using MarkPointMAUI.Data;
    using MarkPointMAUI.Models;
    using System.Diagnostics;
    using System.ComponentModel;
    using System.Globalization;

    public class MainPageViewModel
    {
        public ObservableCollection<MakedPointViewModel> Points { get; } = new ObservableCollection<MakedPointViewModel>();
    }

    public class MakedPointViewModel: MarkedPoint, INotifyPropertyChanged
    {
        public string DisplayText => Name ?? $"{Lat:F6}, {Long:F6}";

        public event PropertyChangedEventHandler? PropertyChanged;

        public new string? Name
        {
            get => base.Name;
            set
            {
                if (base.Name != value)
                {
                    base.Name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
                }
            }
        }
    }

    public partial class MainPage : ContentPage
    {
        private MainPageViewModel viewModel = new MainPageViewModel();
        private readonly IMarkedPointRepository _repository = new MarkedPointDatabase();
        CancellationTokenSource? _cts;

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
                await StartLocationUpdates();

                // Load saved points from repository and populate the view model
                var saved = await _repository.GetPointsAsync();
                foreach (var p in saved)
                {
                    viewModel.Points.Add(new MakedPointViewModel
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Lat = p.Lat,
                        Long = p.Long
                    });
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Erro", $"Ocorreu um erro: {ex.Message}", "OK");
            }
        }

        protected override void OnDisappearing()
        {
            StopLocationUpdates();
            base.OnDisappearing();
        }

        async Task StartLocationUpdates()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                return;

            try
            {
                var last = await Geolocation.GetLastKnownLocationAsync();
                var lat = last?.Latitude ?? 0;
                var lon = last?.Longitude ?? 0;
                var html = BuildMapHtml(lat, lon);
                MapView.Source = new HtmlWebViewSource { Html = html };
                if (last != null)
                {
                    await Task.Delay(500);
                    await MapView.EvaluateJavaScriptAsync($"addMarker({last.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {last.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
                }
            }
            catch { }

            _cts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        try
                        {
                            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                            var loc = await Geolocation.GetLocationAsync(request, _cts.Token);
                            if (loc != null)
                            {
                                MainThread.BeginInvokeOnMainThread(async () => UpdateLocation(loc));
                            }
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex) { 
                            /* loga erro, pelo menos... */ 
                            Debug.WriteLine($"Erro ao obter localização: {ex.Message}");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10), _cts.Token);
                    }
                }
                catch { }
            });
        }

        void StopLocationUpdates()
        {
            if (_cts == null) return;
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        async void UpdateLocation(Location loc)
        {
            try
            {
                await MapView.EvaluateJavaScriptAsync($"addMarker({loc.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {loc.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
            }
            catch { }
        }

        private async void OnMarkPointClicked(object sender, EventArgs e)
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    return;

                Location? location = null;

                location = await Geolocation.GetLastKnownLocationAsync();

                if (location == null)
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Best);
                    location = await Geolocation.GetLocationAsync(request);
                }

                if (location != null)
                {
                        var model = new MarkedPoint
                        {
                            Lat = location.Latitude,
                            Long = location.Longitude
                        };

                        await _repository.SavePointAsync(model);

                        var newMarkedPoint = new MakedPointViewModel
                        {
                            Id = model.Id,
                            Lat = model.Lat,
                            Long = model.Long
                        };
                        viewModel.Points.Add(newMarkedPoint);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Erro", $"Ocorreu um erro: {ex.Message}", "OK");
            }
        }

        private async void OnEditClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is MakedPointViewModel point)
            {
                var newName = await DisplayPromptAsync("Editar Ponto", "Digite um nome para este ponto:", initialValue: point.DisplayText);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    try
                    {
                        // Save plain model so SQLite uses the correct table
                        var model = new MarkPointMAUI.Models.MarkedPoint
                        {
                            Id = point.Id,
                            Name = newName,
                            Lat = point.Lat,
                            Long = point.Long
                        };
                        await _repository.SavePointAsync(model);
                        point.Name = newName;
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlertAsync("Erro", ex.Message, "OK");
                    }
                }
            }
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is MakedPointViewModel point)
            {
                var confirm = await DisplayAlertAsync("Confirmar", "Deseja realmente excluir este ponto?", "Sim", "Não");
                if (confirm)
                {
                    try
                    {
                        await _repository.DeletePointByIdAsync(point.Id);
                        viewModel.Points.Remove(point);
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlertAsync("Erro", ex.Message, "OK");
                    }
                }
            }
        }

        private async void OnNavigateClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is MakedPointViewModel point)
            {
                var uri = new Uri($"geo:{point.Lat.ToString(CultureInfo.InvariantCulture)},{point.Long.ToString(CultureInfo.InvariantCulture)}?q={point.Lat.ToString(CultureInfo.InvariantCulture)},{point.Long.ToString(CultureInfo.InvariantCulture)}({Uri.EscapeDataString(point.DisplayText)})");
                await Launcher.OpenAsync(uri);
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
