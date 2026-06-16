# Dewar's Tweaks

A small SPT 4.0 server mod with two quality-of-life tweaks.

## Features

- **GP Coins at Ref** — adds GP Coins to Ref's stock at 20,000 roubles each, unlimited quantity, loyalty level 1.
- **Keys-only Fence** — turns Fence into a dedicated key shop. Every other top-level item category is blacklisted so Fence only sells mechanical keys + keycards (using Fence's own pricing). Fence rotates a fresh random selection from the full key pool on each refresh.

## Config (`config/config.json`)

| Key | Default | Description |
| --- | --- | --- |
| `addGpCoinsToRef` | `true` | Add GP Coins to Ref. |
| `replaceFenceWithKeys` | `true` | Make Fence a keys-only shop. |
| `fenceKeyAssortSize` | `100` | Keys shown per Fence refresh cycle. Clamped to 1–200 (must stay below the ~238 key pool or Fence's offer generation would hang). |

## Build

```
dotnet build dewar-tweaks.csproj -c Release
```

Output: `bin/Release/dewar-tweaks/dewar-tweaks.dll` (+ `config/`). Deploy the `dewar-tweaks` folder to `SPT/user/mods/`.

## License

MIT
