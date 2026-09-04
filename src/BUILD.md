---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 3adc8e09e621df6acc6996e0d611b887_4857f696a8b711f190de525400461939
    ReservedCode1: aisdhL0PlJVMG/PudOKp1QP4vsn0GcQaRA1208Tawf5Qc5+umTVz88dhCB/2+dJ2IvGMBBWcmLUL3uvYPWbtHFs8O0p7DkDR1E420m/X6McQ2RcJHnfaouMjTIOo3no4P2WDCShwEcIc9jHzOZPZM3jAceRZiy/I3/C8d3EfciqGre+rOFqFCRdoBek=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 3adc8e09e621df6acc6996e0d611b887_4857f696a8b711f190de525400461939
    ReservedCode2: aisdhL0PlJVMG/PudOKp1QP4vsn0GcQaRA1208Tawf5Qc5+umTVz88dhCB/2+dJ2IvGMBBWcmLUL3uvYPWbtHFs8O0p7DkDR1E420m/X6McQ2RcJHnfaouMjTIOo3no4P2WDCShwEcIc9jHzOZPZM3jAceRZiy/I3/C8d3EfciqGre+rOFqFCRdoBek=
---

# Building BlackjackCouple (from source)

> 本文档说明如何把源码编译成 `BlackjackCouple.dll`。Use any machine with a .NET SDK (6 or 8 — a recent SDK can target `net6.0`).

## 1. Prerequisites

1. Install a .NET SDK:
   - https://dotnet.microsoft.com/download/dotnet/6.0 (choose SDK, not just runtime)
   - or `winget install Microsoft.DotNet.SDK.8`
2. Verify:
   ```
   dotnet --list-sdks
   ```

## 2. Fix the reference paths (important)

`BlackjackCouple.csproj` references two groups of assemblies, **all via `HintPath` rooted at one hard-coded game path** (`E:\Hgame\factorial-omega-win-64\`):

1. MelonLoader runtime (`MelonLoader\net6\`) — MelonLoader, Il2CppInterop.Runtime, 0Harmony, Newtonsoft.Json
2. Game / Unity assemblies (`MelonLoader\Il2CppAssemblies\`) — Il2CppGameAssembly, Il2CppIC.UI / Utility.Runtime / ManagedCoroutine, UnityEngine.CoreModule / UI / UIModule / TextRenderingModule, Unity.TextMeshPro, Il2Cppmscorlib, Il2CppSystem + Core / Configuration

Replace `E:\Hgame\factorial-omega-win-64` with the actual game path on your machine (a global find-and-replace across the whole `.csproj` is fine), then build on the same machine.

## 3. Build

```
dotnet build -c Release
```

Output: `bin\Release\BlackjackCouple.dll`

## 4. Deploy

Copy `BlackjackCouple.dll` into the game's `Mods` folder, e.g.:

```
E:\Hgame\factorial-omega-win-64\Mods\BlackjackCouple.dll
```

## 5. In-game verification

1. Launch the game (MelonLoader auto-loads; console prints `[BlackjackCouple] 21点情侣版 MOD 已加载`).
2. Open the Interaction screen and click the injected **"21点（情侣版）"** button.
3. Checklist:
   - [ ] Table UI opens (player/dealer points, Hit / Stand / Double buttons)
   - [ ] Player wins → `+N$` notice, wallet rises (100 normal / 200 doubled)
   - [ ] Bot wins → an IOU slip pops up (named action)
   - [ ] Save & reload → IOU still tracked (persisted in customData)
   - [ ] 3 days without settling → mood −10 + nagging popup
   - [ ] Settle an IOU → settlement popup (go-out IOU shows a floating toast near the mouse)

## 6. Troubleshooting

| Error | Fix |
|-------|-----|
| `NETSDK1005 No .NET SDK available` | Installed runtime but not SDK — install the SDK pack |
| `type or namespace Il2Cpp/StaticGuiBase not found` | `HintPath`s wrong — fix the csproj reference paths |
| `MissingMethodException` at runtime | MelonLoader assembly version mismatch — update MelonLoader to 0.7.2 |
| `UnityAction`-related compile errors | UnityEngine.Events type conflict — make sure `UnityEngine.UI` is referenced and `LangVersion=10` |
*（内容由AI生成，仅供参考）*
