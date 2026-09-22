# Windows beta 0.1.42

The wallet now shows how many known blocks it has downloaded while catching up.
If it does not yet know the target height, the bar shows activity instead of a
percentage. A pause of more than a minute gets a separate message.

Beta miners can be offline for days. The local node now allows an old beta tip
without treating its age alone as an unfinished download. Header, chain-work,
block-validation and mining peer checks still apply. This startup setting does
not apply to the live network.

The new web installer downloads the current full installer and verifies its
signed release record, file hash and Windows publisher before opening setup.
An offline installer remains available.

The bundled native code also includes the published source fixes to template
refresh and amount validation. Official nodes need the matching native release.
There is no chain reset in this update, and wallet files are preserved.

## Release record

The release tag identifies the source used for the wallet. Native build inputs
are recorded separately because the Windows core build uses a Linux cross
compiler. Download hashes identify the signed artifacts, not reproducible
unsigned builds.
