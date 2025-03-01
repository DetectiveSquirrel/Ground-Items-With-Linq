using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq;

public class ItemStateManager
{


    public static void RefreshStoredItems(bool useFastLabelList)
    {
        var validWorldItems = GetValidWorldItems(useFastLabelList);
        if (validWorldItems == null || Main.GameController?.Files == null) return;

        UpdateExistingItems(validWorldItems);
        AddNewItems(validWorldItems);
    }

    private static List<WorldItemInfo> GetValidWorldItems(bool useFastLabelList)
    {
        return useFastLabelList
            ? Main.GameController?.Game?.IngameState?.IngameUi?
                .ItemsOnGroundLabelElement.VisibleGroundItemLabels
                ?.Select(x => new WorldItemInfo(x.Entity, x.Label))
                .ToList()
            : Main.GameController?.Game?.IngameState?.IngameUi?
                .ItemsOnGroundLabels
                ?.Select(x => new WorldItemInfo(x.ItemOnGround, x.Label))
                .ToList();
    }

    private static void UpdateExistingItems(IEnumerable<WorldItemInfo> validWorldItems)
    {
        var validWorldItemIds = new HashSet<long>(
            validWorldItems.Select(e => e.Label.Address)
        );
        Main.StoredCustomItems.RemoveWhere(item =>
            !validWorldItemIds.Contains(item.LabelAddress)
        );
    }

    private static void AddNewItems(IEnumerable<WorldItemInfo> validWorldItems)
    {
        var existingItemAddresses = new HashSet<long>(
            Main.StoredCustomItems.Select(item => item.LabelAddress)
        );

        foreach (var itemInfo in validWorldItems)
        {
            if (existingItemAddresses.Contains(itemInfo.Label.Address)) continue;
            if (!itemInfo.Entity.TryGetComponent<WorldItem>(out var worldItem)) continue;

            Main.StoredCustomItems.Add(new CustomItemData(
                worldItem.ItemEntity,
                itemInfo.Entity,
                itemInfo.Label,
                Main.GameController,
                Main.UniqueArtMapping
            ));
        }
    }

    private record WorldItemInfo(Entity Entity, Element Label);
}