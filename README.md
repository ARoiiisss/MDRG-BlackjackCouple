# BlackjackCouple

A romantic Blackjack mini-game mod for **My Dystopian Robot Girlfriend (MDRG)**. Win rounds for money, lose rounds and get an IOU slip — settle it with kisses, headpats, cuddles, and more.

Built with MelonLoader 0.7.2 (net6.0) and Harmony patches. UI follows the game's own canvas style, fully localizable (EN/ZH), no external assets required.

## Features

- **Full Blackjack game** — Hit / Stand / Double. Standard rules: J/Q/K = 10, A = 11 (no soft handling).
- **Win = reward, lose = IOU** — Beat the bot and get `+N$` (100 normal / 200 doubled). Lose and the bot names a debt: kiss, headpat, cuddle sleep, deep interaction, good chat, or go out together.
- **IOU (debt) system** — each IOU is tied to a unique timestamp signal in the game variables. Pay it off by actually doing the thing in-game; one interaction clears exactly one IOU of that type.
- **Overdue debts** — an unpaid IOU past its 3-day deadline docks 10 mood and triggers a nagging popup. Too many outstanding debts and the bot refuses to play with you.
- **Full localization** — English / 中文 / any language via a simple config file, plus a `[Persona]` override section.
- **Non-modal floating toast** — the "debt settled" confirmation for the *go-out* IOU uses the game's native `ShowFloatingTextAtMouse`, so it never blocks buttons.

## Requirements

- [My Dystopian Robot Girlfriend](https://store.steampowered.com/) (IL2CPP build)
- [MelonLoader](https://melonwiki.xyz/) 0.7.2+

## Installation

1. Make sure MelonLoader 0.7.2 is installed for the game.
2. Drop `BlackjackCouple.dll` into the game's `Mods` folder (next to the game executable).
3. Launch the game. The mod logs `[BlackjackCouple] 21点情侣版 MOD 已加载` on load.
4. Enter the **Interaction** screen (the same one where other mini-games like Old Maid / fishing live) — the mod injects a **"21点（情侣版）"** button into that menu. Click it to start a round.
5. First run auto-generates `UserData\BlackjackCouple.cfg` — edit it to customize texts or add translations (see `configs/` for a reference copy).

## Configuration

The mod reads `UserData/BlackjackCouple.cfg` next to the game executable.

```ini
[General]
Language=auto        ; auto / en / zh

[en]
interact_button=Blackjack
...

[zh]
interact_button=21点
...

[Persona]
; optional per-key overrides, take priority over [en]/[zh]
```

`auto` means the mod's own texts stay Chinese (`zh`); the in-game language API is intentionally not queried (IL2CPP NRE hazard early in the load).

## Building from source

See [src/BUILD.md](src/BUILD.md). You need a .NET SDK (6 or 8 will target net6.0) and to fix the `HintPath`s in the `.csproj` to point at your local game installation.

## Project layout

```
├── src/
│   ├── Main.cs               # MelonMod entry, frame loop, UX glue
│   ├── BlackjackGui.cs       # table UI (built to match the game canvas)
│   ├── BlackjackRules.cs     # game rules & AI
│   ├── IOUSystem.cs          # IOU debt system (timestamps, due dates, mood penalty)
│   ├── Localization.cs       # EN/ZH/config localization
│   └── InteractStatePatch.cs # Harmony patch to hook the blackjack entry point
├── configs/
│   └── BlackjackCouple.cfg   # reference config
└── README.md
```

## License

[MIT](LICENSE)

## Credits

- Thanks to **Sheep** and the MDRG dev team for the official non-modal floating-toast API (`UiOverlay.ShowFloatingTextAtMouse`).
- Made for the MDRG community. Enjoy, robot lover.
