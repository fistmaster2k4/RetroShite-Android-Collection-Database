namespace RetroShite;

public sealed class App : Application
{
    public App() => MainPage = new NavigationPage(new CollectionPage())
    {
        BarBackgroundColor = Color.FromArgb("#10151C"),
        BarTextColor = Color.FromArgb("#F4C95D")
    };
}
