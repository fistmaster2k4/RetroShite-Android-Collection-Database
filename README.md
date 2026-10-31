# RetroShite — Android Collection Database

**Stop buying the same shite twice.**

RetroShite is an offline Android app for cataloguing physical games, movies and other media. Scan a barcode to check whether an item is already in your collection, or add it manually.

See [Technical Specifications](SPECIFICATIONS.md) for supported barcode types, scanner behaviour, and backup/import details.

## Screenshots

| Collection | Settings | Duplicate check | Item editor |
|:--:|:--:|:--:|:--:|
| <img src="screenshots/collection.jpg" width="200" alt="Collection screen"> | <img src="screenshots/settings-v013.jpg" width="200" alt="Settings menu"> | <img src="screenshots/already-owned.jpg" width="200" alt="Already owned dialog"> | <img src="screenshots/edit-item.jpg" width="200" alt="Item editor"> |

| Remove systems | Confirm removal | Add system |
|:--:|:--:|:--:|
| <img src="screenshots/remove-systems.jpg" width="200" alt="Select systems to remove"> | <img src="screenshots/remove-confirmation.jpg" width="200" alt="Confirm removal"> | <img src="screenshots/add-system.jpg" width="200" alt="Add custom system"> |

| Optimising covers | Optimisation results |
|:--:|:--:|
| <img src="screenshots/optimising-covers.jpg" width="200" alt="Live cover optimisation progress"> | <img src="screenshots/optimisation-complete.jpg" width="200" alt="Cover optimisation space savings"> |

## Features

- Barcode scanning with local duplicate detection
- Manual item entry for loose cartridges and items without barcodes
- Camera capture for cover art
- Automatically resize new cover photographs to 1200 pixels maximum, JPEG quality 80%
- Optimise stored cover images from Settings, with live progress and space-saved summary
- System/category filters and custom categories
- Remember the last system used when adding another item during the same app session
- Remove multiple unused systems from Settings; systems containing items are protected
- Removed default systems stay removed after restarting
- Item title, condition, system and barcode fields
- Local SQLite storage, without accounts or a cloud service
- ZIP export with live progress and a completion message
- Merge ZIP backups from another device, adding missing titles and categories without overwriting the local collection
- Optional full replacement restore for moving to a new device

## Requirements and editions

Both APKs are built from **the same source**, selected by the `RetroShiteEdition` build property. No separate source branch is required.

| Edition | Minimum Android | Package ID | Backup export |
| --- | --- | --- | --- |
| Standard (default) | Android 11 (API 30) | `com.blueprint.retroshite` | `Downloads/RetroShite` using MediaStore |
| Legacy | Android 8 (API 26) | `com.blueprint.retroshite.legacy` | Android system **Save As** picker (Storage Access Framework) |

The separate package IDs allow both editions to coexist on a supported device. A Legacy installation will not overwrite a Standard installation. Both editions read and write compatible ZIP backups. Hardware architecture must match the APK runtime identifier (e.g. `android-arm64` or `android-arm`).

## Build from source

Open `src/RetroShite/RetroShite.csproj` in Visual Studio 2022 with the .NET MAUI Android workload installed, or use the .NET 8 SDK with the Android workload.

From `src/RetroShite`, select the desired edition at build time:

```powershell
dotnet restore
dotnet publish RetroShite.csproj -f net8.0-android -c Release -r android-arm64 -p:AndroidPackageFormat=apk -p:RetroShiteEdition=Standard
dotnet publish RetroShite.csproj -f net8.0-android -c Release -r android-arm -p:AndroidPackageFormat=apk -p:RetroShiteEdition=Legacy
```

The `android-arm` example targets older 32-bit tablets; use `android-arm64` instead if the tablet is 64-bit. Publish **one edition at a time**, or use separate output/intermediate directories to avoid mixing build products. For distribution, sign using your own signing configuration and retain the same signing identity for updates. Name the resulting signed files `RetroShite_v0.1.5-signed.apk` and `RetroShite_Legacy_v0.1.5-signed.apk` respectively.

## Data

Collection records and custom categories are stored in an app-private SQLite database; cover images are saved locally. Use **Settings → Backup / Restore** to export a ZIP (Standard: `Downloads/RetroShite`; Legacy: a location chosen in the Save As picker), merge an existing ZIP into the current collection, or replace the collection entirely. Merging skips matching barcodes; for items without barcodes it compares system, title and condition. Different barcodes are kept as separate editions, even when the title is the same. Older backups remain compatible. **Settings → Optimise cover images** reduces the size of photographs already stored on the device; back up before optimising.

## Licence

MIT — see [LICENSE](LICENSE). Third-party dependencies retain their own licences.
