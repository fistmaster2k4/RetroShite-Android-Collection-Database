using RetroShite.Models;
using RetroShite.Services;

namespace RetroShite;

public sealed class ItemPage : ContentPage
{
    private readonly CollectionStore _store;
    private readonly CollectionItem _item;
    private readonly Entry _title = new() { Placeholder = "Title", TextColor = Colors.White, BackgroundColor = Ui.Panel };
    private readonly Entry _barcode = new() { Placeholder = "Optional barcode", Keyboard = Keyboard.Numeric,
        TextColor = Colors.White, BackgroundColor = Ui.Panel };
    private readonly Picker _system = new() { Title = "System", TextColor = Colors.White, BackgroundColor = Ui.Panel };
    private readonly Picker _format = new() { Title = "Condition", TextColor = Colors.White, BackgroundColor = Ui.Panel };
    private readonly Image _cover = new() { HeightRequest = 220, Aspect = Aspect.AspectFit };
    private string _newCover = "";
    public event Action? Changed;
    public string SelectedSystem => _system.SelectedItem?.ToString() ?? "";

    public ItemPage(CollectionStore store, CollectionItem item, string? preferredSystem = null)
    {
        _store = store;
        _item = item;
        Title = item.Id == 0 ? "ADD ITEM" : "EDIT ITEM";
        BackgroundColor = Color.FromArgb("#10151C");
        _title.Text = item.Title;
        _barcode.Text = item.Barcode;
        foreach (var s in _store.Systems()) _system.Items.Add(s);
        _system.SelectedItem = item.System.Length > 0 ? item.System :
            _system.Items.FirstOrDefault(x => x.Equals(preferredSystem, StringComparison.OrdinalIgnoreCase))
            ?? _system.Items.FirstOrDefault();
        foreach (var s in new[] { "Boxed", "Complete", "Loose", "Sealed", "Other" }) _format.Items.Add(s);
        _format.SelectedItem = item.Format;
        if (item.CoverFile.Length > 0)
            _cover.Source = ImageSource.FromFile(Path.Combine(CollectionStore.Covers, item.CoverFile));

        var capture = Ui.Button("TAKE COVER PHOTO", async () => await CaptureAsync(), false);
        var save = Ui.Button("SAVE ITEM", async () => await SaveAsync());
        var stack = new VerticalStackLayout
        {
            Spacing = 14,
            Padding = 20,
            Children = { _cover, capture, Ui.Caption("TITLE"), _title,
                         Ui.Caption("SYSTEM"), _system, Ui.Caption("CONDITION"), _format,
                         Ui.Caption("BARCODE"), _barcode, save }
        };
        if (item.Id != 0)
        {
            stack.Children.Add(Ui.Button("DELETE ITEM", async () => await DeleteAsync(), false));
        }
        Content = new ScrollView { Content = stack };
    }

    private async Task CaptureAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert("Camera unavailable", "This device cannot capture photos.", "OK");
                return;
            }
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null) return;
            var filename = Guid.NewGuid().ToString("N") + ".jpg";
            var destination = Path.Combine(CollectionStore.Covers, filename);
            await using var input = await photo.OpenReadAsync();
            await CoverService.SaveCapturedAsync(input, destination);
            if (_newCover.Length > 0)
            {
                var priorDraft = Path.Combine(CollectionStore.Covers, _newCover);
                if (File.Exists(priorDraft)) File.Delete(priorDraft);
            }
            _newCover = filename;
            _cover.Source = ImageSource.FromFile(destination);
        }
        catch (Exception ex) { await DisplayAlert("Photo error", ex.Message, "OK"); }
    }

    private async Task SaveAsync()
    {
        var title = _title.Text?.Trim() ?? "";
        if (title.Length == 0) { await DisplayAlert("Missing title", "Enter a title.", "OK"); return; }
        if (SelectedSystem.Length == 0) { await DisplayAlert("Missing system", "Add a system in Settings first.", "OK"); return; }
        try
        {
            _item.Title = title;
            _item.System = SelectedSystem;
            _item.Format = _format.SelectedItem?.ToString() ?? "Boxed";
            _item.Barcode = _barcode.Text?.Trim() ?? "";
            var previousCover = _item.CoverFile;
            if (_newCover.Length > 0) _item.CoverFile = _newCover;
            _store.Save(_item);
            if (_newCover.Length > 0 && previousCover.Length > 0 && previousCover != _newCover)
            {
                var old = Path.Combine(CollectionStore.Covers, previousCover);
                if (File.Exists(old)) File.Delete(old);
            }
            Changed?.Invoke();
            await Navigation.PopAsync();
        }
        catch (Exception ex) { await DisplayAlert("Unable to save", ex.Message, "OK"); }
    }

    private async Task DeleteAsync()
    {
        if (!await DisplayAlert("Delete item?", $"Remove {_item.Title} from the collection?", "Delete", "Cancel")) return;
        _store.Delete(_item);
        Changed?.Invoke();
        await Navigation.PopAsync();
    }
}
