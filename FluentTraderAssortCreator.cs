using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace DewarsTweaks;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class FluentTraderAssortCreator(
    DatabaseService databaseService,
    ISptLogger<FluentTraderAssortCreator> logger)
{
    private readonly List<Item> _itemsToSell = [];
    private readonly Dictionary<string, List<List<BarterScheme>>> _barterScheme = new();
    private readonly Dictionary<string, int> _loyaltyLevel = new();

    public FluentTraderAssortCreator CreateSingleAssortItem(MongoId itemTpl, MongoId? itemId = null)
    {
        var newItemToAdd = new Item
        {
            Id = itemId ?? new MongoId(),
            Template = itemTpl,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = false,
                StackObjectsCount = 100
            }
        };

        _itemsToSell.Add(newItemToAdd);
        return this;
    }

    public FluentTraderAssortCreator AddStackCount(int stackCount)
    {
        _itemsToSell[0].Upd.StackObjectsCount = stackCount;
        return this;
    }

    public FluentTraderAssortCreator AddUnlimitedStackCount()
    {
        _itemsToSell[0].Upd.StackObjectsCount = 999999;
        _itemsToSell[0].Upd.UnlimitedCount = true;
        return this;
    }

    public FluentTraderAssortCreator AddBuyRestriction(int maxBuyLimit)
    {
        _itemsToSell[0].Upd.BuyRestrictionMax = maxBuyLimit;
        _itemsToSell[0].Upd.BuyRestrictionCurrent = 0;
        return this;
    }

    public FluentTraderAssortCreator AddLoyaltyLevel(int level)
    {
        _loyaltyLevel[_itemsToSell[0].Id] = level;
        return this;
    }

    public FluentTraderAssortCreator AddMoneyCost(string currencyType, int amount)
    {
        var dataToAdd = new BarterScheme
        {
            Count = amount,
            Template = currencyType
        };

        if (!_barterScheme.TryAdd(_itemsToSell[0].Id, [[dataToAdd]]))
        {
            logger.Warning($"Unable to add barter scheme currency: {currencyType}");
        }

        return this;
    }

    public FluentTraderAssortCreator? Export(string traderId)
    {
        var traderData = databaseService.GetTables().Traders.GetValueOrDefault(traderId);

        var rootItemAddedId = _itemsToSell.FirstOrDefault().Id;
        if (traderData.Assort.Items.Exists(x => x.Id == rootItemAddedId))
        {
            logger.Error($"Unable to add item with key: {_itemsToSell[0].Id}, key already in use");
            _itemsToSell.Clear();
            _barterScheme.Clear();
            _loyaltyLevel.Clear();
            return null;
        }

        traderData.Assort.Items.AddRange(_itemsToSell);
        traderData.Assort.BarterScheme[rootItemAddedId] = _barterScheme[rootItemAddedId];
        traderData.Assort.LoyalLevelItems[rootItemAddedId] = _loyaltyLevel[rootItemAddedId];

        _itemsToSell.Clear();
        _barterScheme.Clear();
        _loyaltyLevel.Clear();

        return this;
    }
}
