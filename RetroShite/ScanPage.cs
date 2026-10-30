using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace RetroShite;

public sealed class ScanPage : ContentPage
{
    private bool _completed;
    private readonly CameraBarcodeReaderView _camera;
    public event Action<string>? Scanned;

    public ScanPage()
    {
        Title = "SCAN BARCODE";
        BackgroundColor = Color.FromArgb("#10151C");
        _camera = new CameraBarcodeReaderView
        {
            IsDetecting = true,
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.OneDimensional,
                AutoRotate = true,
                Multiple = false
            }
        };
        _camera.BarcodesDetected += OnDetected;
        var hint = new Label { Text = "Align the barcode inside the camera view", TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#26313B"), Padding = 18,
            HorizontalTextAlignment = TextAlignment.Center };
        Grid.SetRow(hint, 1);
        Content = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { _camera, hint }
        };
    }

    private void OnDetected(object? sender, BarcodeDetectionEventArgs args)
    {
        var value = args.Results?.FirstOrDefault()?.Value?.Trim();
        if (_completed || string.IsNullOrWhiteSpace(value)) return;
        _completed = true;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _camera.IsDetecting = false;
            Scanned?.Invoke(value);
        });
    }

    protected override void OnDisappearing()
    {
        _camera.IsDetecting = false;
        base.OnDisappearing();
    }
}
