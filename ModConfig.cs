using System.Text.Json.Serialization;

namespace DewarsTweaks;

/// <summary>Typed config for Dewar's Tweaks (config/config.json).</summary>
public record ModConfig
{
    [JsonPropertyName("addGpCoinsToRef")]
    public bool AddGpCoinsToRef { get; set; } = true;

    /// <summary>Enable the Fence category filter (curated category-&gt;count shop).</summary>
    [JsonPropertyName("replaceFenceCategories")]
    public bool ReplaceFenceCategories { get; set; } = true;

    /// <summary>
    /// Map of item-category id -&gt; how many of that category Fence offers per cycle.
    /// Only these categories are sold (everything else is blacklisted). Sub-categories
    /// work too (e.g. KeyMechanical + Keycard separately). The total (sum of counts) is
    /// clamped to 250 so generation can't hang. Example:
    ///   "5c99f98d86f7745c314214b3": 100  (KeyMechanical)
    ///   "5c164d2286f774194c5e69fa": 13   (Keycard)
    /// </summary>
    [JsonPropertyName("fenceCategoryCounts")]
    public Dictionary<string, int> FenceCategoryCounts { get; set; } = new();

    /// <summary>
    /// Multiplier applied to every Fence offer's flea price (1.0 = flea price,
    /// 0.5 = half, 2.0 = double). Prices are sourced live from the flea/handbook.
    /// </summary>
    [JsonPropertyName("fencePriceMultiplier")]
    public double FencePriceMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Per-category price multipliers. If a category is listed here, its offers
    /// use this multiplier instead of the global fencePriceMultiplier. Example:
    ///   "543be5664bdc2dd4348b4569": 0.8  (Meds at 80% of flea)
    ///   "5c99f98d86f7745c314214b3": 1.5  (Keys at 150% of flea)
    /// </summary>
    [JsonPropertyName("fenceCategoryPriceMultipliers")]
    public Dictionary<string, double> FenceCategoryPriceMultipliers { get; set; } = new();

    /// <summary>
    /// Minimum item "durability" percent (0-100) for Fence offers. For keys this is
    /// remaining uses, for weapons/armor it's durability, for meds it's HP resource.
    /// 100 = pristine. Each offer rolls a value between min and max.
    /// </summary>
    [JsonPropertyName("fenceDurabilityMinPercent")]
    public int FenceDurabilityMinPercent { get; set; } = 100;

    /// <summary>
    /// Maximum item "durability" percent (0-100) for Fence offers. See min for details.
    /// </summary>
    [JsonPropertyName("fenceDurabilityMaxPercent")]
    public int FenceDurabilityMaxPercent { get; set; } = 100;
}
