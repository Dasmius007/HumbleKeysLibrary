## What's Changed
# v0.2.0
Updated for new SDK. Also fixes Newtonsoft.Json exceptions thrown when Humble API returns **redeemed_key_val**
as a JObject instead of JString.

Tested against:
Playnite 9.18
SDK 6.2.2
Desktop 2.1.0

# v0.1.4
Adds **Ignore Redeemed Keys** setting. When checked, the library does not import any keys which have
already been redeemed.

# v0.1.3
Compiled for Playnite 8.0

Removes references to Playnite and Playnite.Common assemblies to comply with SDK changes in Playnite 8.

# v0.1.2
Compiled for Playnite 7.9

Improves key type filtering and adds settings view localization. Also changes "Platform" for keys to reflect the
TPKD machine name, as the human names were too inconsistent and creating many single-game platforms.

# v0.1.1
Adds **nintendo_direct** tag to Humble key_type whitelist and removes extraneous **Humble Key: {key_type}** tagging,
since that information is already in "Platform".


# v0.1.0 Pre-release
Release v0.1.0
Installation
Drag and drop the .pext file onto your Playnite window.

Release Details
First release implements:

* Querying the Humble API for "TPKD" objects that represent game keys.
* Checking against a key type whitelist (currently only permits steam keys)
* Reporting the Redeemed / Unredeemed status as tags
* Updates Redeemed / Unredeemed on previously imported games when loading the Humble Keys Library
  Creates a link to your Humble "downloads" page

Upcoming features:

* Other key types in the white list
* Integrating localization