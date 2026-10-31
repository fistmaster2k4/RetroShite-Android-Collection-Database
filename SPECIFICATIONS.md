# RetroShite — Technical Specifications

This document describes the **v0.1.4** Android build. It distinguishes the barcode
types enabled in code from those individually tested on physical products.

## Barcode scanning

RetroShite uses **ZXing.Net.Maui.Controls 0.4.0** and configures the camera scanner
with `BarcodeFormats.OneDimensional` in
[`ScanPage.cs`](src/RetroShite/ScanPage.cs).

The following **11 one-dimensional barcode symbologies are enabled** by that setting:

| Barcode type | Typical examples |
| --- | --- |
| EAN-13 | UK and European retail products; books with an EAN/ISBN-13 barcode |
| EAN-8 | Smaller retail products |
| UPC-A | North American retail products and packaged media |
| UPC-E | Compact UPC product labels |
| Code 39 | Older inventory, industrial and asset labels |
| Code 93 | Compact inventory and asset labels |
| Code 128 | Shipping, logistics and internal inventory labels |
| Codabar | Some library, laboratory and legacy inventory labels |
| ITF (Interleaved 2 of 5) | Carton and logistics labels |
| RSS-14 / GS1 DataBar-14 | Certain retail product labels |
| RSS Expanded / GS1 DataBar Expanded | Extended retail and product-identification labels |

**Enabled does not guarantee every printed example will scan.** Physical
barcode quality, contrast, curvature, damage, lighting and the phone camera
all affect decoding. The list describes the configured decoder capabilities,
not a completed compatibility test matrix.

### Scanner behaviour

- Detects a barcode anywhere in the camera view; no artificial laser line is required.
- Automatic barcode rotation handling is enabled.
- Takes the first decoded result and stops scanning before handling the item.
- Checks the decoded value against the local database. If present, shows the
  already-owned record; otherwise offers to create a new entry.
- Works offline. The barcode identifies a record; it **does not automatically
  download** product titles, cover art, prices or release information.
- Items without a printed barcode can be added manually.

### Not currently enabled

QR Code, Data Matrix, Aztec, PDF417 and MaxiCode (2D formats) are not included
in the present `OneDimensional` scanner setting. MSI, Plessey, Intelligent Mail
and Pharmacode are also outside the enabled set.

Some labels contain multiple barcodes or a supplementary UPC/EAN extension.
The scanner currently saves the first decoded value, not a guaranteed compound
main-code-plus-supplement representation. There is no format-specific
normalisation or validation layer beyond the decoder.

## Collection and backup

| Area | v0.1.4 behaviour |
| --- | --- |
| Platform | Android 11 / API 30 or later |
| Framework | .NET 8 MAUI, Android |
| Data storage | App-private SQLite database |
| Cover storage | Local JPEG files |
| New cover photographs | Maximum 1200 pixels on the longest side, JPEG quality 80% |
| Existing cover photographs | Optional **Settings → Optimise cover images** with progress and storage-savings summary |
| Backup format | ZIP containing `retroshite.db` and `covers/*.jpg` |
| Backup destination | `Downloads/RetroShite` |
| Export | Progress while packaging photographs, followed by final ZIP saving |
| Import — Merge | Adds missing systems and items without replacing existing records |
| Import — Replace | Replaces the current collection from a backup ZIP |
| Accounts/cloud | None required; storage and barcode lookup are offline |

### Duplicate handling

- A non-empty **barcode** is treated as the primary identity for an item.
- Records with **different barcodes** may share a title and system; this is
  intentional for different editions, regions or packaging.
- For records **without a barcode**, merge compares system, title and
  condition, ignoring case and surrounding whitespace.
- Merge reports the number of new items, skipped items and new categories.
- Merging is not bidirectional live synchronisation; transfer an exported ZIP
  between devices to apply additions.

### Notes

- A barcode uniquely identifies a **scanned code**, not necessarily a work or
  title. Different packages may use different codes for the same film or game.
- Cover-image optimisation is lossy. Export a backup first if preserving the
  original full-resolution photographs matters.
- On-device testing has confirmed successful optimisation of 30 cover images,
  reducing their combined storage from **131.9 MB to 3.6 MB**. This is a test
  result, not a guaranteed ratio for every collection.

## Source references

- [RetroShite scanner implementation](src/RetroShite/ScanPage.cs)
- [RetroShite project dependencies](src/RetroShite/RetroShite.csproj)
- [ZXing.Net.MAUI barcode format definitions](https://github.com/Redth/ZXing.Net.Maui/blob/main/ZXing.Net.MAUI/BarcodeFormats.cs)
