# HumbleKeysLibrary
Humble Keys Library is a library plug-in extension for [Playnite](https://playnite.link/) which queries Humble Bundle for third-party keys.

The default Humble Library plug-in only reports DRM-free games, not the keys for third-party services like Steam. Humble Keys Library allows you to search your entire collection for a game, to make sure you don't buy a new copy in the latest sale if you already have one from a previous Humble Bundle.

## Installation
1. Download the .pext file from the [latest release](https://github.com/FiercePunchStudios/HumbleKeysLibrary/releases)
2. Drag-and-drop the .pext file onto your Playnite window.

## Settings
* `Ignore Redeemed Keys` is a setting added in v0.1.4. When checked, HumbleKeysLibrary will not import keys that have been revealed on the Humble site.
* `Import Choice Games` is a setting added in v.0.1.5. When checked, purchases that are detected as Humble Choice Bundles will have the bundle's individual games added.
* `Create Tags for Bundle Names` is a setting added in v.0.1.5. When an entry not `None` is selected, it will create a tag in the format of `Bundle: [Bundle Name]`
* `Enable Cache` is a setting added in v.0.1.5. When checked, HumbleKeysLibrary will create json files for data retrieved from the Humble API in the ExtensionsData directory. If a Cache file exists, the API will not be queried. This applies to Purchases, Memberships (Humble Monthly) and Orders.

## Details
### Tags
* `Key: Redeemed` - this tag is attached to entries that have been redeemed. Corresponds to Humble API `tpkd_dict.all_tpks[n].redeemed_key_value`.
* `Key: Unredeemed` - this tag is attached to entries that have not been redeemed. Corresponds to Humble API `tpkd_dict.all_tpks[n].redeemed_key_value`.
* `Bundle: [Bundle Name]` - this tag is attached to entries that belong to a  Bundle. Corresponds to Humble API `order.product.human_name`

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
