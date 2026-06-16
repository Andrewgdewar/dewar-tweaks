using System.Text.Json.Serialization;

namespace DewarsTweaks;

/// <summary>Typed config for Dewar's Tweaks (config/config.json).</summary>
public record ModConfig
{
    [JsonPropertyName("addGpCoinsToRef")]
    public bool AddGpCoinsToRef { get; set; } = true;

    /// <summary>Make Fence a keys-only shop (mechanical keys + keycards).</summary>
    [JsonPropertyName("replaceFenceWithKeys")]
    public bool ReplaceFenceWithKeys { get; set; } = true;

    /// <summary>
    /// How many keys Fence shows per refresh cycle (rotates through the pool each reset).
    /// MUST stay below the number of available keys (~238) — Fence picks this many UNIQUE
    /// items, and exceeding the pool makes flea-offer generation loop forever (server hang).
    /// </summary>
    [JsonPropertyName("fenceKeyAssortSize")]
    public int FenceKeyAssortSize { get; set; } = 100;
}
