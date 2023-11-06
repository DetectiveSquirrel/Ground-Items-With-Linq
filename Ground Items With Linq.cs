using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ImGuiNET;
using ItemFilterLibrary;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Vector2N = System.Numerics.Vector2;
using ExileCore.Shared.Helpers;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory;

namespace Ground_Items_With_Linq;

public class Ground_Items_With_Linq : BaseSettingsPlugin<Ground_Items_With_LinqSettings>
{
    private readonly HashSet<CustomItemData> StoredCustomItems = new HashSet<CustomItemData>();

    private readonly Stopwatch _timer = Stopwatch.StartNew();

    private List<ItemFilter> _itemFilters;
    private Element LargeMap;

    public Ground_Items_With_Linq()
    {
        Name = "Ground Items With Linq";
    }

    public override bool Initialise()
    {
        GameController.UnderPanel.WantUse(() => Settings.Enable);

        Settings.ReloadFilters.OnPressed = LoadRuleFiles;
        LoadRuleFiles();

        return true;
    }

    public override void OnLoad()
    {
        Graphics.InitImage("directions.png");
    }

    public override void AreaChange(AreaInstance area)
    {
        StoredCustomItems.Clear();
    }

    public override Job Tick()
    {
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
            var playerPos = GameController.Player.GridPosNum;
            var position = GameController.UnderPanel.StartDrawPoint.ToVector2Num();
            var defaultAlertDrawStyle = new AlertDrawStyle("<SOMETHINGS WRONG>", Settings.LabelText, 1, Settings.LabelTrim, Settings.LabelBackground);

            foreach (var entity in wantedItems)
            {
                var alertDrawStyle = defaultAlertDrawStyle with { Text = entity.LabelText, TextColor = entity.TextColor, BackgroundColor = entity.BackgroundColor, BorderColor = entity.BorderColor };
                position = DrawText(playerPos, position, Settings.TextPadding * Settings.TextSize, alertDrawStyle, entity);

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

    private Vector2N DrawText(Vector2N playerPos, Vector2N position, float BOTTOM_MARGIN, AlertDrawStyle kv, CustomItemData entity)
    {
        var padding = new Vector2N(5, 2);
        var delta = entity.Location - playerPos;
        var itemSize = DrawItem(kv, delta, position, padding, kv.Text);
        if (itemSize != new Vector2()) position.Y += itemSize.Y + BOTTOM_MARGIN;

        return position;
    }

    private Vector2 DrawItem(AlertDrawStyle drawStyle, Vector2N delta, Vector2N position, Vector2N padding, string text)
    {
        padding.X -= drawStyle.BorderWidth;
        padding.Y -= drawStyle.BorderWidth;
        double phi;
        var distance = delta.GetPolarCoordinates(out phi);
        //float compassOffset = 15 + (Settings.TextSize * ImGui.GetFontSize() * 2); Using Fontin-SmallCaps
        float compassOffset = 0 + (Settings.TextSize * ImGui.GetFontSize() * 2);
        var textPos = position.Translate(-padding.X - compassOffset, padding.Y);
        Vector2N textSize = new Vector2N(0, 0);
        using (Graphics.SetTextScale(Settings.TextSize))
        {
            //textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, "Fontin-SmallCaps:30", FontAlign.Right); // GGG's in game font
            textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, FontAlign.Right); // GGG's in game font
        }
        var fullHeight = textSize.Y + 2 * padding.Y + 2 * drawStyle.BorderWidth;
        var fullWidth = textSize.X + 2 * padding.X + 2 * drawStyle.BorderWidth + compassOffset;
        var boxRect = new RectangleF(position.X - fullWidth, position.Y, fullWidth - compassOffset, fullHeight);
        Graphics.DrawBox(boxRect, drawStyle.BackgroundColor);
        var rectUV = MathHepler.GetDirectionsUV(phi, distance);
        var rectangleF = new RectangleF(position.X - padding.X - compassOffset + 6, position.Y + padding.Y, textSize.Y, textSize.Y);
        Graphics.DrawImage("directions.png", rectangleF, rectUV);

        if (drawStyle.BorderWidth > 0) Graphics.DrawFrame(boxRect, drawStyle.BorderColor, drawStyle.BorderWidth);

        return new Vector2(fullWidth, fullHeight);
    }

    private void UpdateStoredItems(bool forceUpdate)
    {
        if (_timer.ElapsedMilliseconds <= Settings.UpdateTimer && !forceUpdate) return;

        var ValidWorldItems = GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.ToList();

        if (ValidWorldItems != null && GameController.Files != null)
        {
            var validWorldItemIds = ValidWorldItems.Select(e => e.Address).ToHashSet();
            StoredCustomItems.RemoveWhere(item => !validWorldItemIds.Contains(item.LabelAddress));
            foreach (var entity in ValidWorldItems
                         .ExceptBy(StoredCustomItems.Select(item => item.LabelAddress), x => x.Address))
            {
                if (entity.ItemOnGround.TryGetComponent<WorldItem>(out var worldItem))
                {
                    StoredCustomItems.Add(new CustomItemData(worldItem.ItemEntity, entity.ItemOnGround, entity, GameController.Files));
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