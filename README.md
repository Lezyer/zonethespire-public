# Zone the Spire

A Slay the Spire 2 mod by **Reyzel**. Zone the Spire adds 14 zones that can appear on each act's map, each with its own
mechanics for fights, shops and rest sites, along with zone relics, zone events and new card modifiers. Zones are generated
per act, saved with the run and synchronised in multiplayer; most of the map stays unzoned.

Players: get it from the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3806787309). This repository
is the source, published with each release.

## Zones

The Scrapyard, Mirrorlands, Infestation, Prismatic Storm, Blood Rain, Halls of Midas, Phantasmal Tombs, Ferrosand,
Forgotten Empire, Shadow Corruption, Deva's Domain, Hoarfrost, Blinding Hallows and The Fermentory. Hover a zone node on
the map to see what it does there.

## Requirements

- Slay the Spire 2 on the public beta branch (v0.107.1 or newer)
- [BaseLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127) 3.4.7 or newer
- .NET 9 SDK (to build)

## Build

```bash
dotnet build src/ZoneTheSpire -c Release
```

The mod ends up in `dist/ZoneTheSpire/` (`ZoneTheSpire.dll`, `ZoneTheSpire.json`, `textures/`, `localization/`). The build
looks for the game and BaseLib in the default Steam folders; if yours live elsewhere, override the paths:

```bash
dotnet build src/ZoneTheSpire -c Release -p:Sts2Dir="D:\SteamLibrary\steamapps\common\Slay the Spire 2" -p:BaseLibDir="D:\SteamLibrary\steamapps\workshop\content\2868840\3737335127\BaseLib"
```

## Install a local build

The game loads local mods from `<game folder>\mods\`. Copy `dist/ZoneTheSpire/` there, or build with
`-p:DeployToGame=true`, which copies it to `<game folder>\mods\ZoneTheSpire\`. BaseLib must also be installed. A local copy
takes priority over the Workshop copy of the same mod.

Logs: `%APPDATA%\SlayTheSpire2\logs\godot.log`. Search for `[ZoneTheSpire]`: the mod logs its startup, one line per act
about its zones, and warnings.

## Tests

```bash
dotnet test tests/ZoneTheSpire.Tests
```

The tests cover the engine-free rules (`src/ZoneTheSpire/Core`), the localisation files and the manifest.

## Translating

The mod's text follows the game's language setting. Each language is a folder of JSON files in
`src/ZoneTheSpire/Localization/`, named with the game's language code (`eng`, `deu`, `esp`, `fra`, `ind`, `ita`, `jpn`,
`kor`, `pol`, `ptb`, `rus`, `spa`, `tha`, `tur`, `zhs`, `zht`). `eng` is complete; any string a language leaves out shows
in English.

1. Create `src/ZoneTheSpire/Localization/<code>/` and copy in the `eng` files you want to translate (or only some keys of
   them; keep the file names).
2. Translate the values. Keep the keys, and keep every `{...}` as it is:
   - `{HallowedRules.HallowedPerBlasphemy}` is a number from the rules, filled in when the file loads.
   - `{@hallowed.hallowed}` is another string of `zone_the_spire.json` (a keyword's name, say), in the same language.
     Translate the keyword once in `zone_the_spire.json` and every mention follows.
   - `{Zone}`, `{Amount}` and other plain names are filled in by the game while playing.
   - Tags like `[gold]...[/gold]` and `[blue]...[/blue]` must stay balanced.
3. Run `dotnet test tests/ZoneTheSpire.Tests`: it reports keys English doesn't have, changed placeholders, broken
   references and unbalanced tags, and prints how much of each language is translated.
4. Build, switch the game's language and check the text in game.

Mod keywords that name vanilla powers (Doom, Strength, Weak, Vulnerable) link to the game's own tips by the game's
translated name, so write them the way the game does in that language.

## Debug commands

With the game's dev console enabled (all arguments tab-complete). Zone ids: `scrapyard`, `mirrorlands`, `infestation`,
`prismatic_storm`, `blood_rain`, `halls_of_midas`, `phantasmal_tombs`, `ferrosand`, `forgotten_empire`,
`shadow_corruption`, `devas_domain`, `hoarfrost`, `blinding_hallowed` (Blinding Hallows), `fermentory`.

- `fight_with_zone <encounter> <zone>`: a fight with that zone's effects, e.g. `fight_with_zone TWO_TAILED_RAT_NORMAL mirrorlands`
- `room_with_zone <shop|rest|treasure> <zone>`: a shop, rest site or treasure room with that zone's effects
- `event_with_zone <zone>`: one of the zone's events; `event_with_zone_random`: any zone event
- `zone_map <zone|random>`: the whole current act map becomes one zone, or new random zones

In multiplayer they are networked like the game's own `fight` command.

## Contributing

Bug reports, compatibility issues and balance suggestions are welcome on the Steam Workshop page or as GitHub issues
(include your `godot.log`). This repository receives one commit per release; pull requests are read, and accepted changes
arrive with the next release.

## Licence

[GPL-3.0](LICENSE).

## Disclaimer

Not affiliated with or endorsed by Mega Crit. Slay the Spire is a trademark of Mega Crit.
