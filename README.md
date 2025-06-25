# Humble Keys Library
![DownloadCountTotal](https://img.shields.io/github/downloads/Dasmius007/HumbleKeysLibrary/total?label=Total%20Downloads&style=plastic)
![DownloadCountLatest](https://img.shields.io/github/downloads/Dasmius007/HumbleKeysLibrary/latest/total?label=Downloads@Latest&style=plastic)
![LatestVersion](https://img.shields.io/github/v/release/Dasmius007/HumbleKeysLibrary?label=Latest%20Version&style=plastic)
![BuildStatus](https://img.shields.io/github/actions/workflow/status/Dasmius007/HumbleKeysLibrary/generate_release_artifacts.yml?label=Build)
[![Build PEXT](https://github.com/Dasmius007/HumbleKeysLibrary/actions/workflows/msbuild.yml/badge.svg?event=push)](https://github.com/Dasmius007/HumbleKeysLibrary/actions/workflows/msbuild.yml)

Humble Keys Library is a library plug-in extension for [Playnite](https://playnite.link/) which queries [Humble Bundle](https://www.humblebundle.com/) for third-party keys, and also supports Humble Choice subscription games.

The default Humble Library plug-in only reports DRM-free games, not the keys for third-party services like Steam. Humble Keys Library allows you to search your entire collection for a game, to make sure you don't buy a new copy in the latest sale if you already have one from a previous Humble Bundle.

## Installation
You can install it via Playnite's built-in add-on browser or:
1. Download the .pext file from the [latest release](https://github.com/Dasmius007/HumbleKeysLibrary/releases)
2. Drag-and-drop the .pext file onto your Playnite window

## Settings
* `Ignore Redeemed Keys` is a setting added in v0.1.4. When checked, HumbleKeysLibrary will not import keys that have been revealed on the Humble site.
* `Import Choice Games` is a setting added in v0.1.5. When checked, purchases that are detected as Humble Choice Bundles will have the bundle's individual games added.
* `Create Tags for Bundle Names` is a setting added in v0.1.5. When an entry not `None` is selected, it will create a tag in the format of `Bundle: [Bundle Name]` (Updated in v0.3.0)
* `Unredeemable key handling` is a setting added in v0.3.4. Unredeemable virtual orders (either expired and cannot be redeemed or part of a Bundle where all choices have been made) can be tagged as "Key: Unredeemable" or not added to the library. For existing games, if Tag is selected a new tag will replace the existing 'Key: Unredeemed' tag with 'Key: Unredeemable', if Delete is selected the game will be deleted from the library if it cannot be redeemed.
* `Enable Cache` is a setting added in v0.3.0. When checked, HumbleKeysLibrary will create JSON files for data retrieved from the Humble API in the ExtensionsData directory. If a Cache file exists, the API will not be queried to prevent spamming Humble. This applies to Purchases, Memberships (Humble Monthly) and Orders.

## Details
### Tags
* `Key: Redeemed` - this tag is attached to entries that have been redeemed. Corresponds to Humble API `tpkd_dict.all_tpks[n].redeemed_key_value`.
* `Key: Unredeemed` - this tag is attached to entries that have not been redeemed. Corresponds to Humble API `tpkd_dict.all_tpks[n].redeemed_key_value`.
* `Key: Unredeemable` - this tag is attached to entries that cannot be redeemed (either expired or part of a Bundle where all choices have been made).
* `Bundle: [Bundle Name]` - will be created per Bundle of keys if the option to create grouping tags is enabled (when `product.category=='subscriptioncontent' and product.choice_url has a value`).
### Key Types
Humble API lists key types in `tpkd_dict.all_tpks[n].key_type`, which corresponds to the services on which the key can be redeemed. Supported keys include:
* `gog`
* `nintendo_direct`
* `origin`
* `origin_keyless`
* `steam`

Unsupported key types include non-game software, services that are shut down, and other kinds of content:
* `generic`
* `desura`
* `external_key`

## Attributions
Key icon by Freepik: https://www.flaticon.com/authors/freepik  
Original author: Justin Hardage (Fierce Punch Studios)
