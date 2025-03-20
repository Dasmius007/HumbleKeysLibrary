[![Build PEXT](https://github.com/Dasmius007/HumbleKeysLibrary/actions/workflows/msbuild.yml/badge.svg?event=push)](https://github.com/Dasmius007/HumbleKeysLibrary/actions/workflows/msbuild.yml)

# HumbleKeysLibrary
Humble Keys Library is a library plug-in extension for [Playnite](https://playnite.link/) which queries Humble Bundle for third-party keys.

The default Humble Library plug-in only reports DRM-free games, not the keys for third-party services like Steam. Humble Keys Library allows you to search your entire collection for a game, to make sure you don't buy a new copy in the latest sale if you already have one from a previous Humble Bundle.

## Installation
1. Download the .pext file from the [latest release](https://github.com/Dasmius007/HumbleKeysLibrary/releases)
2. Drag-and-drop the .pext file onto your Playnite window.

## Settings
* `Ignore Redeemed Keys` is a setting added in v0.1.4. When checked, HumbleKeysLibrary will not import keys that have been revealed on the Humble site.
* `Import Choice Games` is a setting added in v0.1.5. When checked, purchases that are detected as Humble Choice Bundles will have the bundle's individual games added.
* `Create Tags for Bundle Names` is a setting added in v0.1.5. When an entry not `None` is selected, it will create a tag in the format of `Bundle: [Bundle Name]` (Updated in v0.3.0)
* `Enable Cache` is a setting added in v0.3.0. When checked, HumbleKeysLibrary will create json files for data retrieved from the Humble API in the ExtensionsData directory. If a Cache file exists, the API will not be queried. This applies to Purchases, Memberships (Humble Monthly) and Orders.
* `Unredeemable Key Handling` added in v0.3.2. If 'Tag' is selected, keys that cannot be redeemed due to expiration or humble removing it will be tagged with 'Key: Unredeemable'. If 'Delete' is selected, the entry will be removed from the Playnite database instead.
* `Notify about expiring keys` added in v0.3.5. When checked, notifications will be made for any key that does not contain the 'Key: Redeemed' tag if the key has an expiry date. It will add a note on the game entry of the exact date and time the key will expire.
* `Notify about claimed keys that doesn't exist in the Steam Library` added in v0.3.5. When checked, during the library scan, will try and cross reference the redeemed key with the Steam Library Plugin. If there is no match it will create a notification.
## Details
### Tags
* `Key: Redeemed` - this tag is attached to entries that have been redeemed. Corresponds to Humble API `tpkd_dict.all_tpks[n].redeemed_key_value`.
* `Key: Unredeemed` - this tag is attached to entries that have not been redeemed. Corresponds to Humble API `tpkd_dict.all_tpks[n].redeemed_key_value`.
* `Key: Unredeemable` - this tag is attached to entries that can no longer be redeemed. Corresponds to Humble API `tkkd_dict.all_tpks[n].is_expired`.
* `Key: Expirable` - this tag is attached to entries that have an expiration date and has not been redeemed. Corresponds to Humble API `tkkd_dict.all_tpks[n].expiration_date` and `tkkd_dict.all_tpks[n].num_days_until_expired`.
* `Bundle: [Bundle Name]` - will be created per Bundle of keys if the option to create grouping tags is enabled (when `product.category=='subscriptioncontent' and product.choice_url has a value`).
### Key Types
Humble API lists key types in `tpkd_dict.all_tpks[n].key_type`, which corresponds to the services on which the key can be redeemed. Supported keys include:
* `gog`
* `nintendo_direct`
* `origin`
* `origin_keyless`
* `steam`

Unsupported key types include non-game software, services that are shut down, and other kinds of content.
* `generic`
* `desura`
* `external_key`

## Attributions
Key icon by Freepik
https://www.flaticon.com/authors/freepik
