using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
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
    public override SemanticVersioning.Version Version { get; init; } = new("1.4.0");
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
    DatabaseService databaseService,
    ConfigServer configServer,
    FluentTraderAssortCreator fluentAssortCreator)
    : IOnLoad
{
    private const string RefTraderId = "6617beeaa9cfa777ca915b7c";
    private const string GpCoinTpl = "5d235b4d86f7742e017bc88a";

    // Root "Item" node + its 12 direct children (top-level categories). Fence's base-assort
    // generator iterates the whole item DB and keeps anything not blacklisted, so we compute
    // the minimal blacklist (everything not under a wanted category) from this tree.
    private const string RootItemNode = "54009119af1c881c07000029";
    private static readonly string[] TopLevelCategories =
    [
        "543be5664bdc2dd4348b4569", // Meds
        "543be5e94bdc2df1348b4568", // Key
        "543be6564bdc2df4348b4568", // ThrowWeap
        "543be6674bdc2df1348b4569", // FoodDrink
        "5447e0e74bdc2d3c308b4567", // SpecItem
        "5447e1d04bdc2dff2f8b4567", // Knife
        "5448eb774bdc2d0a728b4567", // BarterItem
        "5448ecbe4bdc2d60728b4568", // Info
        "566162e44bdc2d3f298b4573", // CompoundItem (weapons/mods/gear/armor)
        "5661632d4bdc2d903d8b456b", // StackableItem (ammo/money)
        "567849dd4bdc2d150f8b456e", // Map
        "6759673c76e93d8eb20b2080", // Flyer
    ];

    public Task OnLoad()
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config/config.json");

        if (config.AddGpCoinsToRef)
            AddGpCoinsToRef();

        if (config.ReplaceFenceCategories)
            ApplyFenceCategoryFilter(config);

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
    /// Turn Fence into a curated shop driven by a category-&gt;count map. Only the configured
    /// categories are sold (every other category is blacklisted so Fence's generator skips
    /// it), each category capped to its configured count, presets disabled, and AssortSize
    /// set to the sum of counts (clamped to keep offer generation from hanging).
    /// </summary>
    private void ApplyFenceCategoryFilter(ModConfig config)
    {
        var fence = configServer.GetConfig<TraderConfig>()?.Fence;
        if (fence == null)
        {
            logger.Warning("[Dewar's Tweaks] Fence config not found; skipping category filter.");
            return;
        }

        var counts = config.FenceCategoryCounts;
        if (counts == null || counts.Count == 0)
        {
            logger.Warning("[Dewar's Tweaks] fenceCategoryCounts is empty; skipping category filter.");
            return;
        }

        var items = databaseService.GetItems();

        // Category-node tree (skip leaf items): parent -> direct child category ids.
        var childrenOf = new Dictionary<string, List<string>>();
        foreach (var (id, tpl) in items)
        {
            if (string.Equals(tpl.Type, "Item", StringComparison.OrdinalIgnoreCase)) continue;
            var parent = tpl.Parent.ToString();
            (childrenOf.TryGetValue(parent, out var list) ? list : childrenOf[parent] = []).Add(id.ToString());
        }

        bool IsUnder(string descId, string ancId)
        {
            var cur = descId;
            for (var i = 0; i < 32 && cur != null; i++)
            {
                if (cur == ancId) return true;
                if (!items.TryGetValue(new MongoId(cur), out var n)) break;
                cur = n.Parent.ToString();
            }
            return false;
        }

        var wanted = counts.Keys.ToHashSet();

        // Minimal blacklist: blacklist any subtree that contains no wanted category; recurse
        // into partially-wanted subtrees so siblings of wanted sub-categories are excluded.
        var blacklist = new List<string>();
        void Exclude(string cat)
        {
            if (wanted.Contains(cat)) return;                                  // keep whole subtree
            if (!wanted.Any(w => IsUnder(w, cat))) { blacklist.Add(cat); return; } // nothing wanted inside
            if (childrenOf.TryGetValue(cat, out var kids))                     // partial -> recurse
                foreach (var c in kids) Exclude(c);
        }
        foreach (var top in TopLevelCategories) Exclude(top);

        fence.Blacklist ??= [];
        foreach (var b in blacklist)
            if (!fence.Blacklist.Contains(b)) fence.Blacklist.Add(b);

        // Clear the blacklist path from each wanted category up to root (undo any vanilla
        // category blacklist that would otherwise block a wanted category).
        foreach (var w in wanted)
        {
            var cur = w;
            for (var i = 0; i < 32 && cur != null; i++)
            {
                fence.Blacklist.Remove(cur);
                if (cur == RootItemNode || !items.TryGetValue(new MongoId(cur), out var n)) break;
                cur = n.Parent.ToString();
            }
        }

        // Per-category counts.
        fence.ItemTypeLimits ??= [];
        foreach (var (cat, count) in counts)
            fence.ItemTypeLimits[new MongoId(cat)] = count;

        // Curated shop: drop price caps and presets. Both the normal and the rep-6+ discount
        // assort generate presets, so zero both (weapons are blacklisted = empty preset pool).
        fence.ItemCategoryRoublePriceLimit?.Clear();
        if (fence.WeaponPresetMinMax != null) { fence.WeaponPresetMinMax.Min = 0; fence.WeaponPresetMinMax.Max = 0; }
        if (fence.EquipmentPresetMinMax != null) { fence.EquipmentPresetMinMax.Min = 0; fence.EquipmentPresetMinMax.Max = 0; }
        if (fence.DiscountOptions?.WeaponPresetMinMax != null) { fence.DiscountOptions.WeaponPresetMinMax.Min = 0; fence.DiscountOptions.WeaponPresetMinMax.Max = 0; }
        if (fence.DiscountOptions?.EquipmentPresetMinMax != null) { fence.DiscountOptions.EquipmentPresetMinMax.Min = 0; fence.DiscountOptions.EquipmentPresetMinMax.Max = 0; }

        // Total offers = sum of counts. Clamp so a huge config can't hang offer generation.
        var total = counts.Values.Where(v => v > 0).Sum();
        fence.AssortSize = Math.Clamp(total, 1, 250);
        if (fence.DiscountOptions != null) fence.DiscountOptions.AssortSize = fence.AssortSize;

        // Freeze the assort after generation so DewarsFencePricing's flea-price + durability
        // edits persist (no on-refresh regen, no periodic partial refresh during a session).
        fence.RegenerateAssortsOnRefresh = false;
        fence.PartialRefreshTimeSeconds = 2_000_000_000;

        logger.Success(
            $"[Dewar's Tweaks] Fence category filter: {counts.Count} categories, " +
            $"{fence.AssortSize} offers/cycle, {blacklist.Count} subtrees blacklisted.");
    }
}
