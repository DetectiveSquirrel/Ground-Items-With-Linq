using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using Ground_Items_With_Linq.Drawing;
using ItemFilterLibrary;
using Newtonsoft.Json;

namespace Ground_Items_With_Linq;

public class GroundItemsWithLinq : BaseSettingsPlugin<GroundItemsWithLinqSettings>
{
    public const string CustomUniqueArtMappingPath = "uniqueArtMapping.json";
    public const string DefaultUniqueArtMappingPath = "uniqueArtMapping.default.json";
    public static GroundItemsWithLinq Main;
    public readonly HashSet<CustomItemData> StoredCustomItems = [];
    public readonly Stopwatch Timer = Stopwatch.StartNew();

    public List<ItemFilter> ItemFilters;
    public Element LargeMap;
    public Dictionary<string, List<string>> UniqueArtMapping = [];

    public GroundItemsWithLinq()
    {
        Name = "Ground Items With Linq";
    }

    public override bool Initialise()
    {
        Main = this;
        GameController.UnderPanel.WantUse(() => Settings.Enable);

        Settings.UniqueIdentificationSettings.RebuildUniqueItemArtMappingBackup.OnPressed += () =>
        {
            var mapping = UniqueArtManager.GetGameFileUniqueArtMapping();

            if (mapping != null)
                File.WriteAllText(
                    Path.Join(DirectoryFullName, CustomUniqueArtMappingPath),
                    JsonConvert.SerializeObject(mapping, Formatting.Indented)
                );
        };

        Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping.OnValueChanged += (_, _) =>
        {
            UniqueArtMapping = UniqueArtManager.LoadUniqueArtMapping(
                Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping
            );
        };

        RulesDisplay.LoadAndApplyRules();
        return true;
    }

    public override void OnLoad()
    {
        Graphics.InitImage("directions.png");
    }

    public override void AreaChange(AreaInstance area)
    {
        UniqueArtMapping = UniqueArtManager.LoadUniqueArtMapping(
            Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping
        );
        StoredCustomItems.Clear();
    }

    public override Job Tick()
    {
        LargeMap = GameController.IngameState.IngameUi.Map.LargeMap;
        UpdateStoredItems(false);
        return null;
    }

    public override void Render()
    {
        var inGameUi = GameController.Game.IngameState.IngameUi;

        if (!Settings.IgnoreFullscreenPanels && inGameUi.FullscreenPanels.Any(x => x.IsVisible)) return;
        if (!Settings.IgnoreRightPanels && inGameUi.OpenRightPanel.IsVisible) return;

        List<CustomItemData> wantedItems;

        if (Settings.OrderByDistance)
            wantedItems = StoredCustomItems
                .Where(item => item.IsWanted == true)
                .OrderBy(group => group.DistanceCustom)
                .ToList();
        else
            wantedItems = StoredCustomItems
                .Where(item => item.IsWanted == true)
                .ToList();

        if (wantedItems.Count <= 0) return;

        DrawingLabels.RenderItemsOnScreen(wantedItems);
    }

    public void UpdateStoredItems(bool forceUpdate)
    {
        UpdateStoredItems(forceUpdate, false);
    }

    public void UpdateStoredItems(bool forceUpdate, bool doProfiler)
    {
        if (Timer.ElapsedMilliseconds <= Settings.UpdateTimer && !forceUpdate) return;

        var profilerTotal = doProfiler ? Stopwatch.StartNew() : null;
        var profilerModifyStored = doProfiler ? Stopwatch.StartNew() : null;

        ItemStateManager.RefreshStoredItems(Settings.UseFastLabelList);

        profilerModifyStored?.Stop();
        var profilerModifyLoopStored = doProfiler ? Stopwatch.StartNew() : null;
        var profilerIsInFilter = doProfiler ? Stopwatch.StartNew() : null;

        foreach (var item in StoredCustomItems)
        {
            if (item.WasDynamicallyUpdated)
            {
                item.IsWanted = null;
                item.WasDynamicallyUpdated = false;
            }

            item.UpdateDynamicCustomData();

            profilerIsInFilter?.Start();
            item.IsWanted ??= ItemFilters?.Any(filter => filter.Matches(item)) ?? false;
            profilerIsInFilter?.Stop();
        }

        if (doProfiler)
            Profiler.LogPerformanceMetrics(profilerModifyLoopStored, profilerTotal, profilerModifyStored,
                profilerIsInFilter);

        Timer.Restart();
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        RulesDisplay.DrawSettings();
    }
}