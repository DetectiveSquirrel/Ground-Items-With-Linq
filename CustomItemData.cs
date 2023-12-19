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
using Vector2 = System.Numerics.Vector2;

public class CustomItemData : ItemData
{
    public CustomItemData(Entity queriedItem, Entity worldEntity, LabelOnGround queriedItemLabel, GameController gc, Dictionary<string, List<string>> uniqueNameCandidates) : base(queriedItem, gc)
    {
        LabelAddress = queriedItemLabel.Address;
        Location = worldEntity.GridPosNum;

        TextColor = queriedItemLabel.Label.TextColor;
        BorderColor = queriedItemLabel.Label.BordColor;
        BackgroundColor = queriedItemLabel.Label.BgColor;
        LabelText = queriedItemLabel.Label.Text;

        UniqueNameCandidates = queriedItem.TryGetComponent<Mods>(out var mods)
                               && !mods.Identified
                               && mods.ItemRarity == ItemRarity.Unique
            ? (uniqueNameCandidates.GetValueOrDefault(queriedItem.GetComponent<RenderItem>()?.ResourcePath) ?? Enumerable.Empty<string>())
                .Where(x => !x.StartsWith("Replica ")).ToList()
            : [];
    }

    public ColorBGRA TextColor { get; set; }
    public ColorBGRA BorderColor { get; set; }
    public ColorBGRA BackgroundColor { get; set; }
    public string LabelText { get; set; }
    public long LabelAddress { get; set; }
    public bool? IsWanted { get; set; }
    public Vector2 Location { get; set; }

    public List<string> UniqueNameCandidates { get; set; }

    public float DistanceCustom { get; set; }

    public override string ToString()
    {
        return $"{Name}, LabelID({LabelAddress}), IsWanted({IsWanted})";
    }
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