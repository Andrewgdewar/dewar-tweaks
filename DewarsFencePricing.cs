using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace DewarsTweaks;

/// <summary>
/// Runs after Fence assorts are generated (TraderCallbacks = 800000). Rewrites every Fence
/// offer's price to the live flea price * configured multiplier (per-category or global),
/// and sets each item's "durability" (key uses / weapon-armor durability / med resource) from
/// config. The assort is frozen by DewarsTweaks so these edits stick for the whole session.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderCallbacks + 1)]
public class DewarsFencePricing(
    ISptLogger<DewarsFencePricing> logger,
    ModHelper modHelper,
    DatabaseService databaseService,
    RagfairPriceService ragfairPriceService,
    FenceService fenceService,
    RandomUtil randomUtil)
    : IOnLoad
{
    public Task OnLoad()
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config/config.json");

        if (!config.ReplaceFenceCategories)
            return Task.CompletedTask;

        var items = databaseService.GetItems();
        var min = Math.Clamp(Math.Min(config.FenceDurabilityMinPercent, config.FenceDurabilityMaxPercent), 0, 100);
        var max = Math.Clamp(Math.Max(config.FenceDurabilityMinPercent, config.FenceDurabilityMaxPercent), 0, 100);

        // Convert category strings to MongoIds for faster lookup.
        var categoryIds = config.FenceCategoryCounts.Keys.Select(k => new MongoId(k)).ToHashSet();

        var changed = 0;
        changed += ApplyTo(fenceService.GetMainFenceAssort(), items, config, categoryIds, min, max);
        changed += ApplyTo(fenceService.GetDiscountFenceAssort(), items, config, categoryIds, min, max);

        logger.Success(
            $"[Dewar's Tweaks] Fence pricing: {changed} offers priced from flea, " +
            $"durability {min}-{max}%.");

        return Task.CompletedTask;
    }

    private int ApplyTo(TraderAssort? assort, IDictionary<MongoId, TemplateItem> items, ModConfig config,
        HashSet<MongoId> categoryIds, int min, int max)
    {
        if (assort?.Items == null) return 0;

        var count = 0;
        foreach (var rootItem in assort.Items.Where(i => string.Equals(i.SlotId, "hideout", StringComparison.OrdinalIgnoreCase)))
        {
            if (!items.TryGetValue(rootItem.Template, out var dbItem) || dbItem.Properties == null)
                continue;

            // Find which configured category this item belongs to, use its multiplier or global fallback.
            var mult = GetMultiplierForItem(rootItem.Template, categoryIds, items, config);

            // Price = live flea price * multiplier (min 1 rouble).
            var price = Math.Round(ragfairPriceService.GetFleaPriceForItem(rootItem.Template) * mult);
            if (price < 1) price = 1;
            if (assort.BarterScheme.TryGetValue(rootItem.Id, out var scheme) && scheme.Count > 0 && scheme[0].Count > 0)
            {
                scheme[0][0].Count = price;
                scheme[0][0].Template = Money.ROUBLES;
            }
            else
            {
                assort.BarterScheme[rootItem.Id] =
                    [[new BarterScheme { Count = price, Template = Money.ROUBLES }]];
            }

            // Durability percent for this offer.
            var pct = (min == max ? min : randomUtil.GetInt(min, max)) / 100.0;
            rootItem.Upd ??= new Upd();
            var props = dbItem.Properties;

            if ((props.MaximumNumberOfUsage ?? 0) > 1)
            {
                // Keys: NumberOfUsages is uses CONSUMED (0 = pristine).
                var maxUses = props.MaximumNumberOfUsage!.Value;
                var consumed = (int)Math.Round(maxUses * (1 - pct));
                rootItem.Upd.Key = new UpdKey { NumberOfUsages = Math.Clamp(consumed, 0, maxUses - 1) };
            }
            else if ((props.MaxDurability ?? 0) > 0)
            {
                var maxDura = props.MaxDurability!.Value;
                rootItem.Upd.Repairable = new UpdRepairable
                {
                    Durability = Math.Round(maxDura * pct),
                    MaxDurability = maxDura,
                };
            }
            else if ((props.MaxHpResource ?? 0) > 0)
            {
                rootItem.Upd.MedKit = new UpdMedKit { HpResource = Math.Round(props.MaxHpResource!.Value * pct) };
            }
            else if ((props.MaxResource ?? 0) > 0)
            {
                var maxRes = props.MaxResource!.Value;
                var remaining = Math.Round(maxRes * pct);
                rootItem.Upd.Resource = new UpdResource { Value = remaining, UnitsConsumed = maxRes - remaining };
            }

            count++;
        }

        return count;
    }

    /// <summary>
    /// Find which configured category an item belongs to (walk parent chain), then return
    /// its per-category multiplier or the global default.
    /// </summary>
    private double GetMultiplierForItem(MongoId itemId, HashSet<MongoId> categoryIds,
        IDictionary<MongoId, TemplateItem> items, ModConfig config)
    {
        var cur = itemId;
        for (var i = 0; i < 32 && cur != null; i++)
        {
            if (!items.TryGetValue(cur, out var tpl)) break;

            if (categoryIds.Contains(cur))
            {
                // Found the category this item belongs to.
                var categoryIdStr = cur.ToString();
                if (config.FenceCategoryPriceMultipliers.TryGetValue(categoryIdStr, out var mult))
                    return mult;
                break;
            }

            cur = tpl.Parent;
        }

        return config.FencePriceMultiplier;
    }
}
