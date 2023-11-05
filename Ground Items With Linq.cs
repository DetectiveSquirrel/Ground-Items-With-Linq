using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ImGuiNET;
using ItemFilterLibrary;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Color = SharpDX.Color;
using Vector2N = System.Numerics.Vector2;

namespace Ground_Items_With_Linq;

public class Ground_Items_With_Linq : BaseSettingsPlugin<Ground_Items_With_LinqSettings>
{
    private readonly List<CustomItemData> StoredCustomItems = new List<CustomItemData>();
    private List<Entity> ValidWorldItems = new List<Entity>();

    private readonly Stopwatch _timer = Stopwatch.StartNew();

    private List<ItemFilter> _itemFilters;
    private Element LargeMap;

    public Ground_Items_With_Linq()
    {
        Name = "Ground Items With Linq";
    }

    public override bool Initialise()
    {
        Settings.ReloadFilters.OnPressed = LoadRuleFiles;
        LoadRuleFiles();

        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        StoredCustomItems.Clear();
    }

    public override Job Tick()
    {
        ValidWorldItems = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.WorldItem].ToList();
        LargeMap = GameController.IngameState.IngameUi.Map.LargeMap;

        UpdateStoredItems(false);
        CustomItemData.UpdateDistance(StoredCustomItems, GameController);

        return null;
    }

    public override void Render()
    {
        var wantedItems = StoredCustomItems.Where(item => item.IsWanted == true).ToList();

        if (wantedItems.Count > 0)
        {
            if (Settings.EnableTextDrawing)
            {
                var closestItems = wantedItems
                    .GroupBy(item => item.Name)
                    .Select(group => new
                    {
                        Name = group.Key,
                        ClosestItem = group.MinBy(item => item.DistanceCustom),
                        Count = group.Count()
                    })
                    .OrderBy(group => group.ClosestItem.DistanceCustom)
                    .ToList();

                var startingPoint = new Vector2(Settings.RulesLocationX, Settings.RulesLocationY);

                // Find the maximum count and maximum distance
                int maxDistance = (int)Math.Ceiling(closestItems.Max(item => item.ClosestItem.DistanceCustom));

                // Calculate the width of the columns based on the maximum count and distance
                int countWidth = closestItems.Max(item => item.Count).ToString().Length;
                int distanceWidth = maxDistance.ToString().Length;

                var longestString = closestItems.MaxBy(item => item.Name.Length);
                var sampleText = $"{longestString.Count.ToString().PadLeft(countWidth)}x ({((int)Math.Round(longestString.ClosestItem.DistanceCustom)).ToString().PadLeft(distanceWidth)}) {longestString.Name} ";
                var textSize = Graphics.MeasureText(sampleText);

                var serverItemsBox = new RectangleF
                {
                    Height = textSize.Y * closestItems.Count,
                    Width = textSize.X,
                    X = startingPoint.X,
                    Y = startingPoint.Y
                };
                var textPadding = 10;
                serverItemsBox.Inflate(textPadding, textPadding);

                var boxColor = new Color(0, 0, 0, 150);
                var textColor = new Color(255, 255, 255, 230);
                Graphics.DrawBox(serverItemsBox, boxColor);

                for (int i = 0; i < closestItems.Count; i++)
                {
                    var group = closestItems[i];
                    string stringItem = $"{group.Count.ToString().PadLeft(countWidth)}x ({((int)Math.Round(group.ClosestItem.DistanceCustom)).ToString().PadLeft(distanceWidth)}) {group.Name}";
                    Graphics.DrawText(stringItem, new Vector2N(startingPoint.X, startingPoint.Y + textSize.Y * i), textColor);
                }
            }

            if (Settings.EnableMapDrawing && LargeMap.IsVisible)
                foreach (var item in wantedItems)
                {
                    //Draw in world Line from player -> Item (thin, maybe color coded?)
                    //Only issue is the filter needs to be very strict unless they want eye aids

                    Graphics.DrawLine(
                        GameController.IngameState.Data.GetGridMapScreenPosition(item.Location),
                        GameController.IngameState.Data.GetGridMapScreenPosition(GameController.Player.GridPosNum),
                        Settings.MapLineThickness,
                        Settings.MapLineColor
                    );
                }
        }
    }

    private void UpdateStoredItems(bool forceUpdate)
    {
        if (_timer.ElapsedMilliseconds <= Settings.UpdateTimer && !forceUpdate) return;

        if (ValidWorldItems != null && GameController.Files != null)
        {
            var validWorldItemIds = ValidWorldItems.Select(e => e.Id).ToHashSet();
            StoredCustomItems.RemoveAll(item => !validWorldItemIds.Contains(item.ServerID));
            foreach (var entity in ValidWorldItems
                         .ExceptBy(StoredCustomItems.Select(item => item.ServerID), x => x.Id))
            {
                if (entity.TryGetComponent<WorldItem>(out var worldItem))
                {
                    StoredCustomItems.Add(new CustomItemData(worldItem.ItemEntity, entity, GameController.Files));
                }
            }
        }

        foreach (var item in StoredCustomItems)
        {
            item.IsWanted ??= ItemInFilter(item);
        }

        _timer.Restart();
    }

    private bool ItemInFilter(ItemData item)
    {
        return _itemFilters != null && 
               _itemFilters.Any(filter => filter.Matches(item));
    }

    #region Rule Drawing and Loading

    public override void DrawSettings()
    {
        base.DrawSettings();

        DrawFileExplorerOptions();
        ImGui.Separator();
        DrawGroundRulesOptions();
    }

    private void DrawFileExplorerOptions()
    {
        if (ImGui.Button("Open Build Folder"))
            Process.Start("explorer.exe", ConfigDirectory);

        ImGui.Separator();
    }

    private void DrawGroundRulesOptions()
    {
        ImGui.BulletText("Select Rules To Load");
        ImGui.BulletText("Ordering rule sets so general items will match first rather than last will improve performance");

        var tempNPCInvRules = new List<GroundRule>(Settings.GroundRules); // Create a copy

        for (int i = 0; i < tempNPCInvRules.Count; i++)
        {
            DrawRuleMovementButtons(tempNPCInvRules, i);
            DrawRuleCheckbox(tempNPCInvRules, i);
        }

        Settings.GroundRules = tempNPCInvRules;
    }

    private void DrawRuleMovementButtons(List<GroundRule> rules, int i)
    {
        if (ImGui.ArrowButton($"##upButton{i}", ImGuiDir.Up) && i > 0)
            (rules[i - 1], rules[i]) = (rules[i], rules[i - 1]);

        ImGui.SameLine();
        ImGui.Text(" ");
        ImGui.SameLine();

        if (ImGui.ArrowButton($"##downButton{i}", ImGuiDir.Down) && i < rules.Count - 1)
            (rules[i + 1], rules[i]) = (rules[i], rules[i + 1]);

        ImGui.SameLine();
        ImGui.Text(" - ");
        ImGui.SameLine();
    }

    private void DrawRuleCheckbox(List<GroundRule> rules, int i)
    {
        var refToggle = rules[i].Enabled;
        if (ImGui.Checkbox($"{rules[i].Name}##checkbox{i}", ref refToggle))
        {
            rules[i].Enabled = refToggle;
            LoadRuleFiles();
            StoredCustomItems.Clear();
            UpdateStoredItems(true);
        }
    }

    private void LoadRuleFiles()
    {
        var pickitConfigFileDirectory = ConfigDirectory;
        var existingRules = Settings.GroundRules;

        try
        {
            var newRules = new DirectoryInfo(pickitConfigFileDirectory).GetFiles("*.ifl")
                .Select(x => new GroundRule(x.Name, Path.GetRelativePath(pickitConfigFileDirectory, x.FullName), false))
                .ExceptBy(existingRules.Select(x => x.Location), x => x.Location)
                .ToList();
            foreach (var groundRule in existingRules)
            {
                var fullPath = Path.Combine(pickitConfigFileDirectory, groundRule.Location);
                if (File.Exists(fullPath))
                {
                    newRules.Add(groundRule);
                }
                else
                {
                    LogError($"File '{groundRule.Name}' not found.");
                }
            }

            _itemFilters = newRules
                .Where(rule => rule.Enabled)
                .Select(rule => ItemFilter.LoadFromPath(Path.Combine(pickitConfigFileDirectory, rule.Location)))
                .ToList();

            Settings.GroundRules = newRules;
        }
        catch (Exception e)
        {
            LogError($"An error occurred while loading rule files: {e.Message}");
        }
    }

    #endregion
}