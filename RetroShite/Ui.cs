namespace RetroShite;

internal static class Ui
{
    public static readonly Color Ink = Color.FromArgb("#10151C");
    public static readonly Color Panel = Color.FromArgb("#222C38");
    public static readonly Color Mint = Color.FromArgb("#70E5B6");
    public static readonly Color Gold = Color.FromArgb("#F4C95D");

    public static Label Caption(string text) => new()
    {
        Text = text, TextColor = Gold, FontSize = 12, FontAttributes = FontAttributes.Bold
    };

    public static Button Button(string label, Func<Task> action, bool primary = true)
    {
        var b = new Button
        {
            Text = label,
            TextColor = primary ? Ink : Colors.White,
            BackgroundColor = primary ? Mint : Color.FromArgb("#314354"),
            CornerRadius = 12,
            FontAttributes = FontAttributes.Bold,
            MinimumHeightRequest = 55
        };
        b.Clicked += async (_, _) => await action();
        return b;
    }
}
