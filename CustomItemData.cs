using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using ItemFilterLibrary;
using System.Collections.Generic;
using Color = SharpDX.Color;
using System.Numerics;
using SharpDX;
using Vector2 = System.Numerics.Vector2;

public class CustomItemData : ItemData
{
    public CustomItemData(Entity queriedItem, Entity worldEntity, LabelOnGround queriedItemLabel, FilesContainer fs, AreaController area) : base(queriedItem, fs)
    {
        LabelAddress = queriedItemLabel.Address;
        Location = worldEntity.GridPosNum;

        TextColor = queriedItemLabel.Label.TextColor;
        BorderColor = queriedItemLabel.Label.BordColor;
        BackgroundColor = queriedItemLabel.Label.BgColor;
        LabelText = queriedItemLabel.Label.Text;

        AreaInfo = new AreaData(area.CurrentArea.RealLevel, area.CurrentArea.Name, area.CurrentArea.Act, area.CurrentArea.Act > 10);
    }
    public ColorBGRA TextColor { get; set; }
    public ColorBGRA BorderColor { get; set; }
    public ColorBGRA BackgroundColor { get; set; }
    public string LabelText { get; set; }
    public long LabelAddress { get; set; }
    public bool? IsWanted { get; set; }
    public Vector2 Location { get; set; }

    public float DistanceCustom { get; set; }

    public override string ToString()
    {
        return $"{Name}, LabelID({LabelAddress}), IsWanted({IsWanted})";
    }

    public static void UpdateDistance(HashSet<CustomItemData> customItems, GameController gc)
    {
        foreach (var item in customItems)
        {
            // Only update wanted items as everything else is not needed.
            if (item.IsWanted == true)
            {
                item.DistanceCustom = gc.Player.GridPosNum.Distance(item.Location);
            }
        }
    }
}