using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using ItemFilterLibrary;
using System.Collections.Generic;
using System.Numerics;

public class CustomItemData : ItemData
{
    public CustomItemData(Entity queriedItem, Entity worldEntity, FilesContainer fs) : base(queriedItem, fs)
    {
        ServerID = worldEntity.Id;
        Location = worldEntity.GridPosNum;
        DistanceCustom = worldEntity.DistancePlayer;
    }

    public uint ServerID { get; set; }
    public bool IsWanted { get; set; } = false;
    public bool HasBeenChecked { get; set; } = false;
    public Vector2 Location { get; set; }

    public float DistanceCustom { get; set; } = float.MaxValue;

    public override string ToString()
    {
        return $"{Name}, ID({ServerID}), IsWanted({IsWanted})";
    }

    public static void UpdateDistance(List<CustomItemData> customItems, GameController gc)
    {
        foreach (var item in customItems)
        {
            // Only update wanted items as everything else is not needed.
            if (item.IsWanted)
            {
                item.DistanceCustom = gc.Player.GridPosNum.Distance(item.Location);
            }
        }
    }
}