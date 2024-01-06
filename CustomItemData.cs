using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ItemFilterLibrary;
using SharpDX;
using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory;
using Vector2 = System.Numerics.Vector2;

namespace Ground_Items_With_Linq;

public class CustomItemData(
    Entity queriedItem,
    Entity worldEntity,
    Element label,
    GameController gc,
    IReadOnlyDictionary<string, List<string>> uniqueNameCandidates) : ItemData(queriedItem, worldEntity, gc)
{
    public ColorBGRA TextColor { get; set; } = label.TextColor;
    public ColorBGRA BorderColor { get; set; } = label.BordColor;
    public ColorBGRA BackgroundColor { get; set; } = label.BgColor;
    public string LabelText { get; set; } = label.Text;
    public long LabelAddress { get; set; } = label.Address;
    public bool? IsWanted { get; set; }
    public Vector2 Location { get; set; } = worldEntity.GridPosNum;

    public List<string> UniqueNameCandidates { get; set; }
        = queriedItem.TryGetComponent<Mods>(out var mods) && !mods.Identified && mods.ItemRarity == ItemRarity.Unique
            ? (uniqueNameCandidates.GetValueOrDefault(
                  queriedItem.GetComponent<RenderItem>()
                             ?.ResourcePath
              ) ?? Enumerable.Empty<string>())
              .Where(x => !x.StartsWith("Replica "))
              .ToList() : [];

    public float DistanceCustom { get; set; }

    public override string ToString() => $"{Name}, LabelID({LabelAddress}), IsWanted({IsWanted})";
}

public static class ItemExtensions
{
    public static void UpdateDynamicCustomData(this CustomItemData item)
    {
        if (item.IsWanted == true)
        {
            item.DistanceCustom = item.GameController.Player.GridPosNum.Distance(item.Location);
        }
    }
}