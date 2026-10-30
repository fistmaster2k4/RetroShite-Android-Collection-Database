# RetroShite v0.1.2 — Android prototype

A local-only physical games/movie collection scanner. No accounts or online lookup.

## Behaviour

- Portrait app with horizontal system tabs and vertically scrolling two-column cover grid.
- **Scan**: read EAN/UPC and other 1D barcodes with ZXing. Existing barcode shows OWNED; unknown offers Add New / Cancel.
- **Add Manually**: add loose cartridges and media with no barcode.
- Item form: title, system, condition, optional barcode, cover photo captured by camera.
- Tap an existing item to view/edit/delete it.
- Gear menu: **Add system** creates a persistent console/media category, and **Backup / Restore** opens the existing ZIP tools.
- Export one ZIP (SQLite + cover JPGs) to Android `Downloads/RetroShite/`, using the same MediaStore pattern as WTT 1.2.42. Import via Android document picker with explicit replace confirmation.
- All data lives on the phone. No network permission or backend.

## Build (Windows, Android workload)

Requires a .NET 8 Android MAUI development environment.

1. Install Visual Studio 2022 with **.NET Multi-platform App UI development** workload or the matching .NET 8 SDK and Android workload.
2. Open `RetroShite.csproj` or run from its directory:

   ```powershell
   dotnet restore
   dotnet publish RetroShite.csproj -f net8.0-android -c Release -p:AndroidPackageFormat=apk
   ```

3. Find the APK under `bin/Release/net8.0-android/publish/` and sideload onto the phone. For Release distribution you may need to configure your own signing keystore; a debug deploy from Visual Studio is also fine for this first test.

## Important testing notes

- Check camera permissions, EAN-13 and UPC scans, duplicate recognition, no-barcode entry, photo capture, editing, ZIP export and ZIP restore on your target Android device.
- v0.1.1: Declares `WRITE_EXTERNAL_STORAGE` (up to API 32) and the `IMAGE_CAPTURE` intent query required by the .NET 8 MAUI photo-capture path on the CAT S62.
- v0.1.2: Stores systems in SQLite, seeds the original defaults for existing installations, and moves backup into the settings menu.
- First-pass cover capture stores the camera JPEG as returned. A later iteration should downsample/rotate as needed to keep exports small.
- Imports replace the collection database and copy covers. Take an export before importing, especially in v0.1.
- Categories can be added from the gear menu and are retained in SQLite and ZIP backups. Existing item categories are also included in the tabs.
- Device/edition variants sharing a single barcode are not separately distinguished in v0.1; this is an ownership catalogue, not a product database.

