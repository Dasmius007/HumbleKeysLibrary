# HumbleKeysLibrary
Humble Keys Library is a library plug-in extension for [Playnite](https://playnite.link/) which queries Humble Bundle for third-party keys.

The default Humble Library plug-in only reports DRM-free games, not the keys for third-party services like Steam. Humble Keys Library allows you to search your entire collection for a game, to make sure you don't buy a new copy in the latest sale if you already have one from a previous Humble Bundle.

## Installation
1. Download the .pext file from the [latest release](https://github.com/FiercePunchStudios/HumbleKeysLibrary/releases)
2. Drag-and-drop the .pext file onto your Playnite window.

## Settings
`Ignore Redeemed Keys` is a setting added in v0.1.4. When checked, HumbleKeysLibrary will not import keys that have been revealed on the Humble site.

## Details
### Tags
* `Key: Redeemed` - this tag is attached to entries that have been redeemed. Corresponds to Humble API `tpkd_dict.all_tpks[n].redeemed_key_value`.
* `Key: Unredeemed` - this tag is attached to entries that have not been redeemed. Corresponds to Humble API `tpkd_dict.all_tpks[n].redeemed_key_value`.

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
