# Changelog

Player-facing changes per release. Earlier test builds (0.9.0 to 0.15.0) went out as unlisted Workshop uploads; their change
notes live in the git history of `workshop/workshop.json`.

## Unreleased

- Translation support: all of the mod's text comes from `localization/<language>/*.json` and follows the game's language
  setting, falling back to English per string. English only so far.
- Sturdier against game updates and other mods: a patch that fails logs one warning and leaves the game's own behaviour in
  place, and card previews still show when another mod fails to draw a preview card.
- A Troubled Dreams free action can no longer carry over to a later campfire or another run; the potion row and map zones
  start fresh with every run.
- Quieter log: only startup lines, one zone line per act and real warnings.
- The mod's DLL no longer carries the build machine's folder path.
- A short in-game mod description; the details live on the Workshop page.
