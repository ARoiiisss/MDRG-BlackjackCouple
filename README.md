# BlackjackCouple

A romantic Blackjack mini-game mod for **My Dystopian Robot Girlfriend (MDRG)** — win rounds for money, lose rounds and get an IOU slip that you settle with kisses, headpats, cuddles, and more.

## Table of Contents

* [Version Notice](#version-notice)
* [Overview](#overview)
* [Features](#features)
* [Installation](#installation)
* [Updating / Uninstalling](#updating--uninstalling)
* [Building from Source](#building-from-source)
* [Configuration](#configuration)
* [Credits](#credits)
* [License](#license)

## Version Notice

> **Version Notice**: This mod is developed and tested for the **latest IL2CPP version** of My Dystopian Robot Girlfriend. Using it with older game versions or the Mono build may result in bugs or crashes.

> [!CAUTION]
> Supported setup:<br>
> - Windows (Native) — tested<br>
> - Linux (Not native, only via Proton **(untested)**)<br>
> - MacOS (Not native, not verified)

> [!CAUTION]
> NOT supported:<br>
> - Android (Lemonloader is outdated and does not work with the IL2CPP build of MDRG)<br>
> - iOS<br>
> - Web

> This mod requires **MelonLoader 0.7.2 or later**. The `.dll` targets `net6.0` and patches the game with Harmony.

## Overview

Adds a full in-game **Blackjack (21点) table** to the Interaction screen — the same menu where the other mini-games live. The mod injects a **"21点（情侣版）"** button into that menu; click it to start a round.

Winning pays out money. Losing makes the bot name a debt: a kiss, ten minutes of headpats, a cuddle sleep, a deep interaction, a good chat, or going out together. Every debt is tracked in your save until you actually pay it off in-game.

## Features

* **Full Blackjack Game** — Hit / Stand / Double. Standard rules: J/Q/K = 10, A = 11 (no soft-hand handling).
* **Win = Reward, Lose = IOU** — Beat the bot and get `+N$` (`100` normal / `200` doubled). Lose and the bot names a debt instead of taking money.
* **IOU (Debt) System** — Each IOU is tied to a unique timestamp signal in the game variables, so it survives save & reload. Paying one interaction clears exactly one IOU of that type.
* **Overdue Debts** — An unpaid IOU past its 3-day deadline docks 10 mood and triggers a nagging popup until the debt is settled. Too many outstanding IOUs and the bot refuses to play with you at all.
* **Full Localization** — English / 中文 / any language via a plain-text config file, plus a `[Persona]` override section for flavour personalization.
* **No External Assets** — The whole table UI is built at runtime to match the game's own canvas style. Nothing to download, nothing to configure except the text.
* **Non-Modal Toasts** — The "debt settled" confirmation for the *go-out* IOU uses the game's native `UiOverlay.ShowFloatingTextAtMouse`, so it never blocks buttons.

## Installation

### What you need:

* [My Dystopian Robot Girlfriend](https://store.steampowered.com/) (IL2CPP build)
* [MelonLoader](https://melonwiki.xyz/) 0.7.2+ installed for the game
* `BlackjackCouple.dll` (from the [Releases](https://github.com/ARoiiisss/MDRG-BlackjackCouple/releases) page, or built from source — see below)

> Note: Make sure your MelonLoader version is **0.7.2 or later**. Older versions will fail at runtime with a `MissingMethodException`.

### Steps:

1. Get MelonLoader set up (if not already):
   - Download MelonLoader from the link above
   - Press "Add game manually", find and select the game's `.exe`
   - Install — do **not** use "nightly builds"
2. Launch the game once to create the MelonLoader folders, then close it.
3. Drop `BlackjackCouple.dll` into the game's `Mods/` folder (next to the game executable).
4. Launch the game. The mod logs `[BlackjackCouple] 21点情侣版 MOD 已加载` on load.
5. Enter the **Interaction** screen and click the injected **"21点（情侣版）"** button to start a round.
6. First run auto-generates `UserData/BlackjackCouple.cfg` — restart the game after editing it (see [Configuration](#configuration)).

### Expected folder structure:

```
Game Install Folder/
├── My Dystopian Robot Girlfriend.exe
├── (Other game files...)
├── MelonLoader/
└── Mods/
    └── BlackjackCouple.dll
```

> [!WARNING]
> **Important**: Make sure your game installation path **does not contain non-Latin characters** (for example Cyrillic, Chinese, Japanese, etc.). Install the game in a folder with only standard English letters (e.g. `C:\Games\MDRG`). Otherwise Unity IL2CPP may fail to load the mod.

## Updating / Uninstalling

Updating works the same way as uninstalling:

1. Close the game.
2. Go to the game's `Mods/` folder.
3. Delete `BlackjackCouple.dll`.
4. (Optional) Delete `UserData/BlackjackCouple.cfg` to also reset any custom texts.
5. Install the new version by following the [installation steps](#steps).

> Your outstanding IOUs live in the game save (`customData`), not in the mod folder — uninstalling the mod does not wipe your debts.

## Building from Source

### Requirements

- A .NET SDK (6 or 8 — a recent SDK can target `net6.0`)
- Game install with MelonLoader and Il2Cpp assemblies (see [What you need](#what-you-need))

### Setup

1. Clone or extract the repository.
2. Open `src/BlackjackCouple.csproj` and fix every `HintPath` that points at `E:\Hgame\factorial-omega-win-64` — replace it with your actual game folder (a global find-and-replace across the whole file is fine).
3. Build:
   ```
   dotnet build -c Release
   ```
4. Output: `bin/Release/BlackjackCouple.dll`.
5. Copy it into the game's `Mods/` folder.

See [src/BUILD.md](src/BUILD.md) for the full build guide, in-game verification checklist and troubleshooting table.

## Configuration

The mod reads `UserData/BlackjackCouple.cfg` next to the game executable. Changes take effect after a restart.

```ini
[General]
Language=auto        ; auto / en / zh

[en]
interact_button=Blackjack
hit=Hit
stand=Stand
double=Double
...

[zh]
interact_button=21点
hit=叫牌
stand=停牌
double=加倍
...

[Persona]
; optional per-key overrides, take priority over [en]/[zh]
; e.g. interact_button=My Own Label
```

* `auto` keeps the mod's own texts in Chinese (`zh`). The in-game language API is intentionally not queried (IL2CPP NRE hazard early in the load).
* All UI strings, bot lines and IOU names/lines are `key=value` pairs — translate any of them and add your language as a new section.
* `[Persona]` entries override everything and are meant for personalizing the bot's tone.

## Credits

AROiiisss

Made for the MDRG community. Enjoy, robot lover.

## License

MIT License — see [LICENSE](LICENSE).
