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

namespace Ground_Items_With_Linq
{
    public class Ground_Items_With_Linq : BaseSettingsPlugin<Ground_Items_With_LinqSettings>
    {
        public record FilterDirItem(string Name, string Path);
        public List<CustomItemData> StoredCustomItems { get; set; } = new List<CustomItemData>();
        public List<uint> EntitiesToRemove { get; set; } = new List<uint> { };

        public List<Entity> ValidWorldItems { get; set; } = new List<Entity>();

        private static readonly Stopwatch _timer = new Stopwatch();

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
            if (StoredCustomItems == null)
            {
                StoredCustomItems = new List<CustomItemData>();
            }
            else
            {
                StoredCustomItems.Clear();
            }
        }
        public void UpdateCustomItemData(List<CustomItemData> customItems)
        {
            CustomItemData.UpdateDistance(customItems, GameController);
        }
        public override void EntityRemoved(Entity entity)
        {
            if (entity.Type == EntityType.WorldItem)
            {
                entity.TryGetComponent<WorldItem>(out var worldItemComp);
                if (worldItemComp == null)
                    return;

                EntitiesToRemove.Add(entity.Id);
            }
        }

        public override Job Tick()
        {
            ValidWorldItems = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.WorldItem].ToList();
            LargeMap = GameController.IngameState.IngameUi.Map.LargeMap;

            if (!_timer.IsRunning)
                _timer.Start();

            if (StoredCustomItems != null)
            {
                var validWorldItemIds = new HashSet<uint>(ValidWorldItems.Select(e => e.Id));
                StoredCustomItems.RemoveAll(item => !validWorldItemIds.Contains(item.ServerID));

                UpdateStoredItems(false);
                UpdateCustomItemData(StoredCustomItems);
            }
            else
            {
                StoredCustomItems = new List<CustomItemData>();
            }

            return null;
        }

        public override void Render()
        {
            List<CustomItemData> wantedItems = StoredCustomItems.Where(item => item.IsWanted).ToList();

            if (wantedItems.Count > 0)
            {
                var closestItems = wantedItems.OrderBy(item => item.DistanceCustom).GroupBy(item => item.Name)
                                              .Select(group => new
                                              {
                                                  Name = group.Key,
                                                  ClosestItem = group.OrderBy(item => item.DistanceCustom).First(),
                                                  Count = group.Count()
                                              }).OrderBy(group => group.ClosestItem.DistanceCustom);

                var startingPoint = new Vector2(Settings.RulesLocationX, Settings.RulesLocationY);

                startingPoint.X += 15;

                var textPadding = 10;

                // Find the maximum count and maximum distance
                int maxCount = closestItems.Max(item => item.Count);
                int maxDistance = (int)Math.Ceiling(closestItems.Max(item => item.ClosestItem.DistanceCustom));

                // Calculate the width of the columns based on the maximum count and distance
                int countWidth = maxCount.ToString().Length;
                int distanceWidth = maxDistance.ToString().Length;

                var longestString = closestItems.OrderByDescending(item => item.Name.Length).First();
                var sampleText = $"{longestString.Count.ToString().PadLeft(countWidth)}x ({Math.Round(longestString.ClosestItem.DistanceCustom).ToString().PadLeft(distanceWidth)}) {longestString.Name} ";
                var textHeight = Graphics.MeasureText(sampleText);

                var serverItemsBox = new RectangleF
                {
                    Height = textHeight.Y * closestItems.Count(),
                    Width = textHeight.X + (textPadding * 2),
                    X = startingPoint.X,
                    Y = startingPoint.Y
                };

                var boxColor = new Color(0, 0, 0, 150);
                var textColor = new Color(255, 255, 255, 230);
                Graphics.DrawBox(serverItemsBox, boxColor);

                for (int i = 0; i < closestItems.Count(); i++)
                {
                    string stringItem = $"{closestItems.ElementAt(i).Count.ToString().PadLeft(countWidth)}x ({Math.Round(closestItems.ElementAt(i).ClosestItem.DistanceCustom).ToString().PadLeft(distanceWidth)}) {closestItems.ElementAt(i).Name}";
                    Graphics.DrawText(stringItem, new Vector2N(startingPoint.X + textPadding, startingPoint.Y + (textHeight.Y * i)), textColor);
                }

                if (LargeMap.IsVisible)
                    foreach (var item in wantedItems)
                    {
                        //Draw in world Line from player -> Item (thin, maybe color coded?)
                        //Only issue is the filter needs to be very strict unless they want eye aids

                        Graphics.DrawLine(
                            GameController.IngameState.Data.GetGridMapScreenPosition(item.Location),
                            GameController.IngameState.Data.GetGridMapScreenPosition(GameController.Player.GridPosNum),
                            1f,
                            Color.LimeGreen
                        );
                    }
            }
        }

        private void UpdateStoredItems(bool forceUpdate)
        {
            if (_timer.ElapsedMilliseconds <= Settings.UpdateTimer && !forceUpdate) return;

            if (ValidWorldItems != null && StoredCustomItems != null)
            {
                var storedCustomItemIds = new HashSet<uint>(StoredCustomItems.Select(item => item.ServerID));

                foreach (var entity in ValidWorldItems)
                {
                    if (!storedCustomItemIds.Contains(entity.Id))
                    {
                        var worldItemComponent = entity.GetComponent<WorldItem>();
                        if (worldItemComponent != null && GameController.Files != null)
                        {
                            StoredCustomItems.Add(new CustomItemData(worldItemComponent.ItemEntity, entity, GameController.Files));
                        }
                    }
                }

                foreach (var item in StoredCustomItems.Where(item => !item.HasBeenChecked))
                {
                    if (ItemInFilter(item))
                    {
                        item.IsWanted = true;
                    }
                    item.HasBeenChecked = true;
                }
            }

            _timer.Restart();
        }

        private bool ItemInFilter(ItemData item)
        {
            if (_itemFilters != null)
            {
                return _itemFilters.Any(filter => filter.Matches(item));
            }
            return false;
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

            ImGui.SameLine(); ImGui.Text(" "); ImGui.SameLine();

            if (ImGui.ArrowButton($"##downButton{i}", ImGuiDir.Down) && i < rules.Count - 1)
                (rules[i + 1], rules[i]) = (rules[i], rules[i + 1]);

            ImGui.SameLine(); ImGui.Text(" - "); ImGui.SameLine();
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

            if (!Directory.Exists(pickitConfigFileDirectory))
            {
                if (Directory.CreateDirectory(pickitConfigFileDirectory) == null)
                {
                    LogError($"[{Name}] Failed to create directory.");
                    return;
                }
            }

            var tempPickitRules = new List<GroundRule>(Settings.GroundRules);
            var toRemove = new List<GroundRule>();

            var itemList = new DirectoryInfo(pickitConfigFileDirectory)
                .GetFiles("*.ifl")
                .Select(drItem =>
                {
                    var existingRule = tempPickitRules.FirstOrDefault(rule => rule.Location == drItem.FullName);
                    if (existingRule == null)
                        Settings.GroundRules.Add(new GroundRule(drItem.Name, drItem.FullName, false));
                    return new FilterDirItem(drItem.Name, drItem.FullName);
                })
                .ToList();

            try
            {
                tempPickitRules
                    .Where(rule => !File.Exists(rule.Location))
                    .ToList()
                    .ForEach(rule => { toRemove.Add(rule); LogError($"File '{rule.Name}' not found."); });

                _itemFilters = tempPickitRules
                    .Where(rule => rule.Enabled && File.Exists(rule.Location))
                    .Select(rule => ItemFilter.LoadFromPath(rule.Location))
                    .ToList();

                toRemove.ForEach(rule => Settings.GroundRules.Remove(rule));
            }
            catch (Exception e)
            {
                LogError($"An error occurred while loading rule files: {e.Message}");
            }
        }
        #endregion
    }
}