## What's Changed
# v0.3.4
* Altered how tags are handled to deal with scenario where tags get removed manually via Manage Library function of Playnite
* Corrected tooltips for Unredeemable key handling
* Remove prerequisite "Import Choice Game" from "Unredeemable key handling" options

# v0.3.3
* Correct github action to build against correct tag version

# v0.3.2
* Update ChoiceMonth model to include ChoicesRemaining and ChoicesMade
* Update Order model to determine virtual orders (items added from Bundle instead of from persisted record on server)
* Alter HumbleKeysAccountClient to add virtual orders that have not yet been added to the Order
* Add additional logic to HumbleKeysLibrary to handle unredeemable virtual orders (either expired and cannot be redeemed or part of a Bundle where all choices have been made)
* Add new option to allow for either tagging a Game as 'Key: Unredeemable' or not add to the library

# v0.3.1
* Correct version number to match release version

# v0.3.0
New Features:
* Added Optional feature to import games in Humble Choice Monthly bundles.
* Added Optional feature to create tags based on Bundle Names (Either all Bundles or Monthly only)
* Added Optional feature to cache API Objects as JSON files in the ExtensionsData directory

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