using Newtonsoft.Json;
using System.Net;
using System.Windows;
using System.Windows.Threading;

namespace DesktopApp;

public partial class MainWindow : Window
{
    private System.Timers.Timer _timer = new System.Timers.Timer(1000);
    private readonly WebClient _client = new WebClient(); // sync, no DI

    public MainWindow()
    {
        InitializeComponent();

        _timer.Elapsed += (s, e) =>
        {
            try
            {
                // Blocking call to local API
                var json = _client.DownloadString("https://localhost:7296/api/v1/measurements?type=HeartRate");

                Dispatcher.Invoke(() =>
                {
                    dataGrid.ItemsSource = JsonConvert.DeserializeObject<List<dynamic>>(json);
                });
            }
            catch { /* swallow */ }
        };

        _timer.Start();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        // Imperative UI logic
        MessageBox.Show("Refreshing...");

        var json = _client.DownloadString("https://localhost:7296/healthz");

        MessageBox.Show("OK: " + json);
    }
}