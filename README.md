# Dewar's Tweaks

A small SPT 4.0 server mod with two quality-of-life tweaks.

## Features

- **GP Coins at Ref** — adds GP Coins to Ref's stock at 20,000 roubles each, unlimited quantity, loyalty level 1.
- **Fence category filter** — turns Fence into a curated shop driven by a category → count map. Only the configured item categories are sold (every other category is blacklisted), each category capped to its configured count, weapon/equipment presets disabled. Every offer is re-priced to the **live flea price × a multiplier**, and item "durability" (key uses / weapon-armor durability / med resource) is tunable. The assort is frozen after generation so prices and durability stick for the session.

## Config (`config/config.json`)

| Key | Default | Description |
| --- | --- | --- |
| `addGpCoinsToRef` | `true` | Add GP Coins to Ref. |
| `replaceFenceCategories` | `true` | Enable the Fence category filter. |
| `fenceCategoryCounts` | keys map | Map of item-category id → how many of that category Fence offers per cycle. Only these categories are sold. Sub-categories work too. The total (sum of counts) is clamped to 250 so generation can't hang. |
| `fencePriceMultiplier` | `1.0` | Global multiplier applied to every Fence offer's flea price. `1.0` = flea price, `0.5` = half, `2.0` = double. |
| `fenceCategoryPriceMultipliers` | `{}` | Per-category price multipliers (overrides global). If a category is listed here, its offers use that multiplier instead. Example: `"543be5664bdc2dd4348b4569": 0.8` (Meds at 80% of flea). Empty dict = use global multiplier. |
| `fenceDurabilityMinPercent` | `100` | Minimum "durability" percent (0–100) per offer. Keys = remaining uses, weapons/armor = durability, meds = HP resource. `100` = pristine. |
| `fenceDurabilityMaxPercent` | `100` | Maximum "durability" percent (0–100) per offer. Each offer rolls between min and max. |

### `fenceCategoryCounts` example

```json
"fenceCategoryCounts": {
  "5c99f98d86f7745c314214b3": 100,
  "5c164d2286f774194c5e69fa": 13
}
```

Common category ids:

| Id | Category |
| --- | --- |
| `5c99f98d86f7745c314214b3` | Mechanical keys |
| `5c164d2286f774194c5e69fa` | Keycards |
| `543be5664bdc2dd4348b4569` | Meds |
| `543be6674bdc2df1348b4569` | Food & drink |
| `5448eb774bdc2d0a728b4567` | Barter / junk items |
| `543be6564bdc2df4348b4568` | Throwables (grenades) |
| `5447e1d04bdc2dff2f8b4567` | Knives |

Leave `fenceCategoryCounts` empty (or set `replaceFenceCategories` to `false`) to leave Fence vanilla.

## Build

```
dotnet build dewar-tweaks.csproj -c Release
```

Output: `bin/Release/dewar-tweaks/dewar-tweaks.dll` (+ `config/`). Deploy the `dewar-tweaks` folder to `SPT/user/mods/`.

## License

MIT
