using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using ItemFilterLibrary;
using System.Collections.Generic;
using Color = SharpDX.Color;
using System.Numerics;

public class CustomItemData : ItemData
{
    public CustomItemData(Entity queriedItem, Entity worldEntity, LabelOnGround queriedItemLabel, FilesContainer fs) : base(queriedItem, fs)
    {
        ServerID = worldEntity.Id;
        LabelAddress = queriedItemLabel.Address;
        Location = worldEntity.GridPosNum;
        DistanceCustom = worldEntity.DistancePlayer;

        TextColor = ConvertToSharpDXColor(queriedItemLabel.Label.TextColor);
        BorderColor = ConvertToSharpDXColor(queriedItemLabel.Label.BordColor);
        BackgroundColor = ConvertToSharpDXColor(queriedItemLabel.Label.BgColor);
    }
    public Color TextColor { get; set; }
    public Color BorderColor { get; set; }
    public Color BackgroundColor { get; set; }
    public uint ServerID { get; set; }
    public long LabelAddress { get; set; }
    public bool? IsWanted { get; set; }
    public Vector2 Location { get; set; }

    public float DistanceCustom { get; set; }

    public override string ToString()
    {
        return $"{Name}, ID({ServerID}), IsWanted({IsWanted})";
    }

    public static void UpdateDistance(List<CustomItemData> customItems, GameController gc)
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
    public static Color ConvertToSharpDXColor(BGRA bgra)
    {
        return new Color(bgra.R, bgra.G, bgra.B, bgra.A);
    }
}