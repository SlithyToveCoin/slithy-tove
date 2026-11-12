# Windows 0.1.51 and Linux GUI 0.1.51-beta.1

This update fixes a problem where a failed balance check could make the app
say it could not reach the miner. Mining and wallet checks now report their
own results. If a balance read fails, your previous balance stays visible
with a note that it needs updating.

Background checks no longer overlap. A delayed reply cannot replace the
result of a newer Start or Stop request. If the app cannot confirm a command,
it checks the miner again. It does not send another Start request on its own.
You can still request a stop when the mining status is unknown.

On Linux, Stop interrupts a slow background check. The balance breakdown
comes from a single reply, so the total and its parts describe the same
moment. Routine balance updates no longer trigger wallet rescans on Windows.
Recovery and explicit rescan tools are unchanged.

Both apps save a small local diagnostic history for troubleshooting after
a restart. It records known app events, not raw node output or wallet secrets.
Nothing is uploaded automatically.

Your wallet files, saved thread count and current beta chain stay in place.
Mining stays stopped after installing the update. Start it again when you
are ready.
