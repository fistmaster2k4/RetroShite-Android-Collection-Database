using RetroShite.Services;

namespace RetroShite;

public sealed class RemoveSystemsPage : ContentPage
{
    private readonly CollectionStore _store;
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Button _remove;
    public event Action? Changed;

    public RemoveSystemsPage(CollectionStore store)
    {
        _store = store;
        Title = "REMOVE SYSTEMS";
        BackgroundColor = Ui.Ink;

        var list = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16, 12) };
        list.Children.Add(new Label
        {
            Text = "Select the systems to remove. Systems containing items are protected.",
            TextColor = Color.FromArgb("#A6B5C3"), FontSize = 13, Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var system in _store.Systems())
        {
            var name = system;
            int count = _store.ItemCountForSystem(name);
            var checkbox = new CheckBox { Color = Ui.Mint, IsEnabled = count == 0, VerticalOptions = LayoutOptions.Center };
            checkbox.CheckedChanged += (_, e) =>
            {
                if (e.Value) _selected.Add(name);
                else _selected.Remove(name);
                UpdateButton();
            };
            var label = new Label
            {
                Text = count == 0 ? name : $"{name}  ({count} items)",
                TextColor = count == 0 ? Colors.White : Color.FromArgb("#85919E"),
                FontSize = 16, VerticalTextAlignment = TextAlignment.Center
            };
            var row = new HorizontalStackLayout
            {
                Spacing = 12, Padding = new Thickness(8, 4), BackgroundColor = Ui.Panel,
                Children = { checkbox, label }
            };
            if (count == 0)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => checkbox.IsChecked = !checkbox.IsChecked;
                label.GestureRecognizers.Add(tap);
            }
            list.Children.Add(row);
        }

        _remove = Ui.Button("REMOVE SELECTED", async () => await RemoveAsync(), false);
        _remove.IsEnabled = false;
        var layout = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
        };
        layout.Add(new ScrollView { Content = list }, 0, 0);
        layout.Add(new VerticalStackLayout { Padding = new Thickness(16, 10), Children = { _remove } }, 0, 1);
        Content = layout;
    }

    private void UpdateButton()
    {
        _remove.IsEnabled = _selected.Count > 0;
        _remove.Text = _selected.Count == 0 ? "REMOVE SELECTED" : $"REMOVE SELECTED ({_selected.Count})";
    }

    private async Task RemoveAsync()
    {
        if (_selected.Count == 0) return;
        if (!await DisplayAlert("Remove systems?",
                $"Remove {_selected.Count} selected system(s)? Items and cover photos will not be deleted.",
                "REMOVE", "CANCEL")) return;
        try
        {
            _store.RemoveSystems(_selected);
            Changed?.Invoke();
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Cannot remove systems", ex.Message, "OK");
        }
    }
}
