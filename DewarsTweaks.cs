using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

namespace DewarsTweaks;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.dewar.tweaks";
    public override string Name { get; init; } = "Dewar's Tweaks";
    public override string Author { get; init; } = "Dewar";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.3.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class DewarsTweaks(
    ISptLogger<DewarsTweaks> logger,
    ModHelper modHelper,
    ConfigServer configServer,
    FluentTraderAssortCreator fluentAssortCreator)
    : IOnLoad
{
    private const string RefTraderId = "6617beeaa9cfa777ca915b7c";
    private const string GpCoinTpl = "5d235b4d86f7742e017bc88a";

    // "Key" category (parent of KeyMechanical + Keycard) — the only category Fence keeps.
    private const string KeyCategory = "543be5e94bdc2df1348b4568";
    private const string KeyMechanicalParent = "5c99f98d86f7745c314214b3";
    private const string KeycardParent = "5c164d2286f774194c5e69fa";

    // All other top-level item categories (direct children of the root "Item" node).
    // Fence's base-assort generator iterates the whole item DB and appends every valid
    // item; blacklisting these leaves only "Key" items (mechanical keys + keycards).
    // CompoundItem covers weapons/mods/gear/armor; StackableItem covers ammo/money.
    private static readonly string[] NonKeyCategories =
    [
        "543be5664bdc2dd4348b4569", // Meds
        "543be6564bdc2df4348b4568", // ThrowWeap
        "543be6674bdc2df1348b4569", // FoodDrink
        "5447e0e74bdc2d3c308b4567", // SpecItem
        "5447e1d04bdc2dff2f8b4567", // Knife
        "5448eb774bdc2d0a728b4567", // BarterItem
        "5448ecbe4bdc2d60728b4568", // Info
        "566162e44bdc2d3f298b4573", // CompoundItem
        "5661632d4bdc2d903d8b456b", // StackableItem
        "567849dd4bdc2d150f8b456e", // Map
        "6759673c76e93d8eb20b2080", // Flyer
    ];

    public Task OnLoad()
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config/config.json");

        if (config.AddGpCoinsToRef)
            AddGpCoinsToRef();

        if (config.ReplaceFenceWithKeys)
            MakeFenceKeysOnly(config);

        return Task.CompletedTask;
    }

    private void AddGpCoinsToRef()
    {
        fluentAssortCreator
            .CreateSingleAssortItem(GpCoinTpl)
            .AddUnlimitedStackCount()
            .AddMoneyCost(Money.ROUBLES, 20000)
            .AddLoyaltyLevel(1)
            .Export(RefTraderId);

        logger.Success("[Dewar's Tweaks] Added GP Coins to Ref: 20,000 roubles each, unlimited stock");
    }

    /// <summary>
    /// Turn Fence into a keys-only shop. Fence's base-assort generator walks the entire
    /// item DB and appends every valid item (+ presets), so the clean approach is to
    /// blacklist every top-level category EXCEPT "Key". We also drop the key price caps /
    /// type limits that would otherwise hide most keys, and zero the weapon/equipment
    /// preset counts. Fence then generates the full key list itself (its own pricing).
    /// </summary>
    private void MakeFenceKeysOnly(ModConfig config)
    {
        var fence = configServer.GetConfig<TraderConfig>()?.Fence;
        if (fence == null)
        {
            logger.Warning("[Dewar's Tweaks] Fence config not found; skipping keys-only setup.");
            return;
        }

        // Whitelist keys by blacklisting everything else.
        fence.Blacklist ??= [];
        foreach (var cat in NonKeyCategories)
            if (!fence.Blacklist.Contains(cat))
                fence.Blacklist.Add(cat);
        // Ensure keys are never blacklisted.
        fence.Blacklist.Remove(KeyCategory);
        fence.Blacklist.Remove(KeyMechanicalParent);
        fence.Blacklist.Remove(KeycardParent);

        // Drop the per-category caps/limits that hide keys.
        fence.ItemCategoryRoublePriceLimit?.Remove(KeyMechanicalParent);
        fence.ItemCategoryRoublePriceLimit?.Remove(KeycardParent);
        fence.ItemTypeLimits?.Remove(KeyMechanicalParent);
        fence.ItemTypeLimits?.Remove(KeycardParent);

        // No weapon/equipment presets so Fence only surfaces keys.
        if (fence.WeaponPresetMinMax != null) { fence.WeaponPresetMinMax.Min = 0; fence.WeaponPresetMinMax.Max = 0; }
        if (fence.EquipmentPresetMinMax != null) { fence.EquipmentPresetMinMax.Min = 0; fence.EquipmentPresetMinMax.Max = 0; }
        // Show N keys per cycle (rotates through the key pool on each Fence refresh). MUST
        // stay below the pool size: Fence picks AssortSize UNIQUE items, and exceeding the
        // pool makes it loop forever during flea-offer generation (server hangs). Clamp to
        // a safe ceiling so a bad config value can't hang the server.
        fence.AssortSize = Math.Clamp(config.FenceKeyAssortSize, 1, 200);

        logger.Success($"[Dewar's Tweaks] Fence is now keys-only (mechanical keys + keycards), {fence.AssortSize} per cycle.");
    }
}
