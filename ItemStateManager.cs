using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace Ground_Items_With_Linq;

public class ItemStateManager
{
    private readonly GameController _gameController;
    private readonly HashSet<CustomItemData> _storedItems;
    private readonly Dictionary<string, List<string>> _uniqueArtMapping;

    public ItemStateManager(
        HashSet<CustomItemData> storedItems,
        GameController gameController,
        Dictionary<string, List<string>> uniqueArtMapping)
    {
        _storedItems = storedItems;
        _gameController = gameController;
        _uniqueArtMapping = uniqueArtMapping;
    }

    public void RefreshStoredItems(bool useFastLabelList)
    {
        var validWorldItems = GetValidWorldItems(useFastLabelList);
        if (validWorldItems == null || _gameController?.Files == null) return;

        UpdateExistingItems(validWorldItems);
        AddNewItems(validWorldItems);
    }

    private List<WorldItemInfo> GetValidWorldItems(bool useFastLabelList)
    {
        return useFastLabelList
            ? _gameController?.Game?.IngameState?.IngameUi?
                .ItemsOnGroundLabelElement.VisibleGroundItemLabels
                ?.Select(x => new WorldItemInfo(x.Entity, x.Label))
                .ToList()
            : _gameController?.Game?.IngameState?.IngameUi?
                .ItemsOnGroundLabels
                ?.Select(x => new WorldItemInfo(x.ItemOnGround, x.Label))
                .ToList();
    }

    private void UpdateExistingItems(IEnumerable<WorldItemInfo> validWorldItems)
    {
        var validWorldItemIds = new HashSet<long>(
            validWorldItems.Select(e => e.Label.Address)
        );
        _storedItems.RemoveWhere(item =>
            !validWorldItemIds.Contains(item.LabelAddress)
        );
    }

    private void AddNewItems(IEnumerable<WorldItemInfo> validWorldItems)
    {
        var existingItemAddresses = new HashSet<long>(
            _storedItems.Select(item => item.LabelAddress)
        );

        foreach (var itemInfo in validWorldItems)
        {
            if (existingItemAddresses.Contains(itemInfo.Label.Address)) continue;
            if (!itemInfo.Entity.TryGetComponent<WorldItem>(out var worldItem)) continue;

            _storedItems.Add(new CustomItemData(
                worldItem.ItemEntity,
                itemInfo.Entity,
                itemInfo.Label,
                _gameController,
                _uniqueArtMapping
            ));
        }
    }

    private record WorldItemInfo(Entity Entity, Element Label);
}