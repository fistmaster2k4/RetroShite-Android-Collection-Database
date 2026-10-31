using RetroShite.Services;

namespace RetroShite;

// Simple modal with a live item counter. Operations run on a worker thread.
public sealed class ProgressPage : ContentPage
{
    private readonly Label _stage = new() { TextColor = Colors.White, FontSize = 14 };
    private readonly Label _count = new() { TextColor = Ui.Mint, FontSize = 16,
        HorizontalTextAlignment = TextAlignment.Center };
    private readonly ProgressBar _bar = new() { ProgressColor = Ui.Mint, BackgroundColor = Ui.Panel };

    public ProgressPage(string title)
    {
        Title = title;
        BackgroundColor = Ui.Ink;
        NavigationPage.SetHasNavigationBar(this, false);
        Content = new VerticalStackLayout
        {
            Padding = new Thickness(24), Spacing = 20, VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title.ToUpperInvariant(), FontSize = 19,
                    FontAttributes = FontAttributes.Bold, TextColor = Ui.Gold,
                    HorizontalTextAlignment = TextAlignment.Center },
                _stage, _bar, _count,
                new Label { Text = "Please wait — keep RetroShite open.",
                    TextColor = Color.FromArgb("#98A9B9"), HorizontalTextAlignment = TextAlignment.Center }
            }
        };
    }

    protected override bool OnBackButtonPressed() => true;

    public IProgress<WorkProgress> Reporter => new Progress<WorkProgress>(p =>
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _stage.Text = p.Stage;
            _count.Text = p.Total > 0 ? $"{p.Done} / {p.Total} images or titles processed" : "Preparing…";
            _bar.Progress = p.Total > 0 ? Math.Clamp((double)p.Done / p.Total, 0, 1) : 0;
        });
    });
}
