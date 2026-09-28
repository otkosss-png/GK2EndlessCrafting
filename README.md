# GK2 Endless Crafting

A BepInEx plugin for **Graveyard Keeper 2**: adds an **∞** toggle to a craft station's
craft window. While it is on, the station keeps repeating the selected recipe forever,
so you no longer have to click "+" hundreds of times or run back to the station.

## Features

- **∞ toggle** in the craft window of any station that has a recipe list.
- Drives the game's **own** infinite-craft mechanism (`CraftElementBase.IsInfinite`): the
  queue row shows the game's infinite state and the "+" button becomes a no-op.
- If ingredients or conditions run out, it simply **waits** and resumes when they are
  available again.
- **Per-station, per-save-slot memory**: the mode and the chosen recipe are stored in
  `BepInEx\config\GK2EndlessCrafting\<slot>.json` and re-applied when the save loads.
- Works with **mouse and controller** (the button is a clone of the in-game one, so it
  stays in the gamepad navigation).
- Not offered on the **firewood sheds** (woodpiles 1 and 2): an endless queue broke how they
  display the firewood (1.0.8); a mode that was already on there is switched off automatically.
- **Settings** in the in-game **Mods** menu.

## Settings (in-game Mods menu)

- Enabled — show the "∞" button, on/off.
- "Проверка станций, мс" (`PollMs`) — station poll interval, 100–5000 ms (default 250).
- Language (auto / en / ru).
- DebugLog.

## Requirements

- Graveyard Keeper 2 (Steam).
- BepInEx 5.4.23.5 x64 and **GK2 Mod Framework** (both installed by
  [GK2 Mod Installer](https://github.com/otkosss-png/GK2ModInstaller)).

## Install

Copy `BepInEx/plugins/GK2EndlessCrafting/GK2EndlessCrafting.dll` into
`<game>\BepInEx\plugins\GK2EndlessCrafting\`, or install the Workshop item via the
[GK2 Workshop Auto-Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3807406994).

## Build

```
& "<dotnet>" build -c Release
```
References the game's assemblies from `$(GameDir)` (default
`E:\SteamLibrary\steamapps\common\Graveyard Keeper 2`).

## Layout

- `src/GK2EndlessCrafting.Core` — pure logic (netstandard2.0): registry + store, unit-tested.
- `src/GK2EndlessCrafting` — the BepInEx plugin (netstandard2.1).
- `tests/GK2EndlessCrafting.Core.Tests` — xUnit.
- `docs/superpowers` — design spec and implementation plan.

Not affiliated with the developers or publishers of Graveyard Keeper 2.
