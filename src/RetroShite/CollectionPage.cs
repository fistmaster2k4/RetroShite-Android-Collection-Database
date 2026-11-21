using RetroShite.Models;
using RetroShite.Services;

namespace RetroShite;

public sealed class CollectionPage : ContentPage
{
    private CollectionStore _store = new();
    private string _selectedSystem = "All";
    private string? _lastNewItemSystem;
    private readonly HorizontalStackLayout _tabs = new() { Spacing = 8, Padding = new Thickness(12, 8) };
    private readonly CollectionView _collection;
    private readonly Label _count = new() { TextColor = Color.FromArgb("#98A9B9"), FontSize = 12 };
    private bool _busy;

    public CollectionPage()
    {
        Title = "RETROSHITE";
        BackgroundColor = Ui.Ink;
        var tabs = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _tabs,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never };
        var scan = Ui.Button("▣  SCAN", async () => await ScanAsync());
        var add = Ui.Button("＋  ADD MANUALLY", async () => await OpenEditorAsync(new CollectionItem()), false);
        var actions = new Grid
        {
            BackgroundColor = Ui.Ink, ColumnSpacing = 8, Padding = new Thickness(12, 10),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        actions.Add(scan, 0); actions.Add(add, 1);
        var head = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Padding = new Thickness(15, 12) };
        head.Add(new Label { Text = "YOUR COLLECTION", TextColor = Ui.Gold, FontSize = 15,
            FontAttributes = FontAttributes.Bold }, 0);
        head.Add(_count, 1);
        // Virtualise rows rather than mounting an Image and card for every item.
        // Each row contains at most two cards, preserving the existing design.
        _collection = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 },
            Margin = new Thickness(12, 0, 12, 12),
            ItemTemplate = new DataTemplate(() =>
            {
                var host = new ContentView();
                host.BindingContextChanged += (_, _) =>
                {
                    host.Content = host.BindingContext is CollectionRow row ? MakeRow(row) : null;
                };
                return host;
            })
        };
        var main = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        main.Add(tabs, 0, 0);
        main.Add(head, 0, 1);
        main.Add(_collection, 0, 2);
        main.Add(actions, 0, 3);
        Content = main;
        ToolbarItems.Add(new ToolbarItem("⚙", null, async () => await ShowSettingsAsync()));
        RebuildTabs(); Refresh();
    }

    private void RebuildTabs()
    {
        _tabs.Children.Clear();
        var allSystems = _store.Systems().Prepend("All");
        foreach (var system in allSystems)
        {
            var name = system;
            var selected = name == _selectedSystem;
            var button = new Button { Text = name.ToUpperInvariant(), Padding = new Thickness(12, 6),
                FontSize = 11, CornerRadius = 9, TextColor = selected ? Ui.Ink : Colors.White,
                BackgroundColor = selected ? Ui.Mint : Ui.Panel };
            button.Clicked += (_, _) => { _selectedSystem = name; RebuildTabs(); Refresh(); };
            _tabs.Children.Add(button);
        }
    }

    // All retains the configured system/tab order, with titles A-Z within each system.
    // Individual filters retain the existing unheaded two-column appearance.
    private void Refresh()
    {
        var items = _store.All();
        var rows = new List<CollectionRow>();
        if (_selectedSystem == "All")
        {
            _count.Text = $"{items.Count} ITEMS";
            var bySystem = items.GroupBy(x => x.System, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var system in _store.Systems())
            {
                if (!bySystem.TryGetValue(system, out var systemItems) || systemItems.Count == 0)
                    continue;
                rows.Add(new CollectionRow(system));
                AddCardRows(rows, systemItems);
            }
        }
        else
        {
            var filtered = items.Where(x => string.Equals(x.System, _selectedSystem,
                StringComparison.OrdinalIgnoreCase)).ToList();
            _count.Text = $"{filtered.Count} ITEMS";
            AddCardRows(rows, filtered);
        }
        if (rows.Count == 0)
            rows.Add(new CollectionRow("Nothing here yet. Scan or add your first item.", true));
        _collection.ItemsSource = rows;
    }

    private static void AddCardRows(List<CollectionRow> rows, List<CollectionItem> items)
    {
        for (var i = 0; i < items.Count; i += 2)
            rows.Add(new CollectionRow(items[i], i + 1 < items.Count ? items[i + 1] : null));
    }

    private sealed class CollectionRow
    {
        public string? Heading { get; }
        public bool IsEmpty { get; }
        public CollectionItem? Left { get; }
        public CollectionItem? Right { get; }
        public CollectionRow(string heading, bool isEmpty = false)
        {
            Heading = heading;
            IsEmpty = isEmpty;
        }
        public CollectionRow(CollectionItem left, CollectionItem? right)
        {
            Left = left;
            Right = right;
        }
    }

    private View MakeRow(CollectionRow row)
    {
        if (row.Heading is not null)
            return new Label
            {
                Text = row.IsEmpty ? row.Heading : row.Heading.ToUpperInvariant(),
                TextColor = row.IsEmpty ? Colors.White : Ui.Gold,
                FontSize = row.IsEmpty ? 13 : 14,
                FontAttributes = row.IsEmpty ? FontAttributes.None : FontAttributes.Bold,
                Margin = row.IsEmpty ? new Thickness(6, 18) : new Thickness(3, 14, 3, 2),
                HorizontalTextAlignment = row.IsEmpty ? TextAlignment.Center : TextAlignment.Start
            };

        var grid = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        if (row.Left is not null) grid.Add(MakeCard(row.Left), 0);
        if (row.Right is not null) grid.Add(MakeCard(row.Right), 1);
        return grid;
    }

    private View MakeCard(CollectionItem item)
    {
        var art = new Image { HeightRequest = 165, Aspect = Aspect.AspectFit, BackgroundColor = Color.FromArgb("#314354") };
        if (item.CoverFile.Length != 0 && File.Exists(Path.Combine(CollectionStore.Covers, item.CoverFile)))
            art.Source = ImageSource.FromFile(Path.Combine(CollectionStore.Covers, item.CoverFile));
        var card = new VerticalStackLayout
        {
            Spacing = 6, Padding = 9, BackgroundColor = Ui.Panel,
            Children = { art,
                new Label { Text = item.Title, TextColor = Colors.White, FontSize = 13,
                    FontAttributes = FontAttributes.Bold, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation },
                new Label { Text = $"{item.System} · {item.Format}", TextColor = Color.FromArgb("#A6B5C3"), FontSize = 10 } }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenEditorAsync(item);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task OpenEditorAsync(CollectionItem item)
    {
        var isNew = item.Id == 0;
        var preferred = _lastNewItemSystem ?? (_selectedSystem == "All" ? null : _selectedSystem);
        var editor = new ItemPage(_store, item, isNew ? preferred : null);
        editor.Changed += () =>
        {
            if (isNew) _lastNewItemSystem = editor.SelectedSystem;
            RebuildTabs(); Refresh();
        };
        await Navigation.PushAsync(editor);
    }

    private async Task ScanAsync()
    {
        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
        {
            await DisplayAlert("Camera permission", "The camera is needed to scan barcodes.", "OK");
            return;
        }
        var scanner = new ScanPage();
        scanner.Scanned += value => _ = HandleScanAsync(value);
        await Navigation.PushAsync(scanner);
    }

    private async Task HandleScanAsync(string barcode)
    {
        await Navigation.PopAsync();
        var found = _store.Lookup(barcode);
        if (found is not null)
        {
            var edit = await DisplayAlert("ALREADY OWNED", $"{found.Title}\n{found.System}\n{found.Format}\n\nBarcode: {barcode}", "VIEW / EDIT", "DONE");
            if (edit) await OpenEditorAsync(found);
        }
        else if (await DisplayAlert("NOT IN COLLECTION", $"Barcode: {barcode}\n\nAdd this item?", "ADD NEW", "CANCEL"))
            await OpenEditorAsync(new CollectionItem { Barcode = barcode });
    }

    private async Task ShowBackupsAsync()
    {
        if (_busy) return;
        var action = await DisplayActionSheet("Collection backup", "Cancel", null,
            "Export collection ZIP", "Merge ZIP into collection", "Replace collection from ZIP");
        if (action == "Export collection ZIP")
        {
            var progress = await BeginProgressAsync("Exporting collection");
            try
            {
                var location = await BackupService.ExportAsync(_store, progress.Reporter);
                _store = new CollectionStore();
                await EndProgressAsync(progress);
                await DisplayAlert("Backup saved", location, "OK");
            }
            catch (OperationCanceledException)
            {
                _store = new CollectionStore();
                await EndProgressAsync(progress);
            }
            catch (Exception ex)
            {
                _store = new CollectionStore();
                await EndProgressAsync(progress);
                await DisplayAlert("Export FAILED", ex.Message, "OK");
            }
        }
        else if (action is "Merge ZIP into collection" or "Replace collection from ZIP")
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select RetroShite ZIP backup" });
            if (file is null) return;
            var merge = action == "Merge ZIP into collection";
            if (!await DisplayAlert(merge ? "Merge collection?" : "Replace collection?",
                merge ? "New titles and systems will be added. Existing titles will remain unchanged." :
                "This replaces your collection. Export a backup first.", "IMPORT", "CANCEL")) return;
            var progress = await BeginProgressAsync(merge ? "Merging collection" : "Restoring collection");
            try
            {
                var result = await BackupService.ImportAsync(file, _store, merge, progress.Reporter);
                _store = new CollectionStore();
                RebuildTabs(); Refresh();
                await EndProgressAsync(progress);
                await DisplayAlert(merge ? "Merge complete" : "Restored",
                    merge ? $"Added: {result.Added}\nAlready present: {result.Skipped}\nNew systems: {result.SystemsAdded}" :
                    "Collection replaced from backup.", "OK");
            }
            catch (Exception ex)
            {
                _store = new CollectionStore();
                await EndProgressAsync(progress);
                await DisplayAlert("Import FAILED", ex.Message, "OK");
            }
        }
    }

    private async Task<ProgressPage> BeginProgressAsync(string title)
    {
        _busy = true;
        var progress = new ProgressPage(title);
        await Navigation.PushModalAsync(progress);
        return progress;
    }

    private async Task EndProgressAsync(ProgressPage progress)
    {
        if (Navigation.ModalStack.LastOrDefault() == progress)
            await Navigation.PopModalAsync();
        _busy = false;
    }

    private async Task OptimiseCoversAsync()
    {
        if (_busy) return;
        var covers = Directory.GetFiles(CollectionStore.Covers, "*.jpg");
        if (!await DisplayAlert("Optimise existing covers?",
            $"Resize large photographs to {CoverService.MaximumDimension}px (JPEG {CoverService.JpegQuality}% quality). " +
            $"Your {covers.Length} original cover images will be replaced after successful conversion. Export a backup first.",
            "OPTIMISE", "CANCEL")) return;
        var progress = await BeginProgressAsync("Optimising covers");
        try
        {
            var changed = 0;
            long before = 0, after = 0;
            await Task.Run(() =>
            {
                for (var i = 0; i < covers.Length; i++)
                {
                    var result = CoverService.OptimiseExisting(covers[i]);
                    if (result.Changed) changed++;
                    before += result.Before;
                    after += result.After;
                    progress.Reporter.Report(new WorkProgress("Resizing photographs", i + 1, covers.Length));
                }
            });
            await EndProgressAsync(progress);
            Refresh();
            await DisplayAlert("Optimisation complete",
                $"Images processed: {covers.Length}\nImages reduced: {changed}\n" +
                $"Before: {before / 1048576.0:F1} MB\nAfter: {after / 1048576.0:F1} MB\n" +
                $"Saved: {(before - after) / 1048576.0:F1} MB", "OK");
        }
        catch (Exception ex)
        {
            await EndProgressAsync(progress);
            Refresh();
            await DisplayAlert("Optimisation FAILED", ex.Message + "\nCompleted images remain safe.", "OK");
        }
    }

    private async Task ShowSettingsAsync()
    {
        var action = await DisplayActionSheet("RetroShite settings", "Cancel", null,
            "Add system", "Remove systems", "Backup / Restore", "Optimise cover images");
        if (action == "Backup / Restore") await ShowBackupsAsync();
        else if (action == "Optimise cover images") await OptimiseCoversAsync();
        else if (action == "Remove systems")
        {
            var page = new RemoveSystemsPage(_store);
            page.Changed += () =>
            {
                if (!_store.Systems().Contains(_selectedSystem, StringComparer.OrdinalIgnoreCase))
                    _selectedSystem = "All";
                if (_lastNewItemSystem is not null && !_store.Systems().Contains(_lastNewItemSystem, StringComparer.OrdinalIgnoreCase))
                    _lastNewItemSystem = null;
                RebuildTabs(); Refresh();
            };
            await Navigation.PushAsync(page);
        }
        else if (action == "Add system")
        {
            var name = await DisplayPromptAsync("Add system", "System name", "ADD", "CANCEL",
                "e.g. Atari Jaguar", maxLength: 60);
            if (name is null) return;
            if (!_store.AddSystem(name))
            {
                await DisplayAlert("System not added", "Enter a new, unique system name.", "OK");
                return;
            }
            _selectedSystem = name.Trim();
            RebuildTabs(); Refresh();
        }
    }
}
