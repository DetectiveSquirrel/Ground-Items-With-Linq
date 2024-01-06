using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using ItemFilterLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2N = System.Numerics.Vector2;

namespace Ground_Items_With_Linq;

public class Ground_Items_With_Linq : BaseSettingsPlugin<Ground_Items_With_LinqSettings>
{
    private const string CustomUniqueArtMappingPath = "uniqueArtMapping.json";
    private const string DefaultUniqueArtMappingPath = "uniqueArtMapping.default.json";
    public static Graphics _graphics;
    private readonly Stopwatch _timer = Stopwatch.StartNew();
    private readonly HashSet<CustomItemData> _storedCustomItems = [];

    private List<ItemFilter> _itemFilters;
    private Element _largeMap;
    public Dictionary<string, List<string>> UniqueArtMapping = [];

    public Ground_Items_With_Linq()
    {
        Name = "Ground Items With Linq";
    }

    public override bool Initialise()
    {
        _graphics = Graphics;
        GameController.UnderPanel.WantUse(() => Settings.Enable);

        Settings.UniqueIdentificationSettings.RebuildUniqueItemArtMappingBackup.OnPressed += () =>
        {
            var mapping = GetGameFileUniqueArtMapping();

            if (mapping != null)
            {
                File.WriteAllText(
                    Path.Join(DirectoryFullName, CustomUniqueArtMappingPath),
                    JsonConvert.SerializeObject(mapping, Formatting.Indented)
                );
            }
        };

        Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping.OnValueChanged += (_, _) =>
        {
            UniqueArtMapping = GetUniqueArtMapping();
        };

        Settings.ReloadFilters.OnPressed = LoadRuleFiles;
        LoadRuleFiles();
        return true;
    }

    private Dictionary<string, List<string>> GetGameFileUniqueArtMapping()
    {
        if (GameController.Files.UniqueItemDescriptions.EntriesList.Count == 0)
        {
            GameController.Files.LoadFiles();
        }

        return GameController
               .Files.ItemVisualIdentities.EntriesList.Where(x => x.ArtPath != null)
               .GroupJoin(
                   GameController.Files.UniqueItemDescriptions.EntriesList.Where(x => x.ItemVisualIdentity != null),
                   x => x,
                   x => x.ItemVisualIdentity,
                   (ivi, descriptions) => (ivi.ArtPath, descriptions: descriptions.ToList())
               )
               .GroupBy(x => x.ArtPath, x => x.descriptions)
               .Select(
                   x => (x.Key, Names: x
                                       .SelectMany(items => items)
                                       .Select(item => item.UniqueName?.Text)
                                       .Where(name => name != null)
                                       .Distinct()
                                       .ToList())
               )
               .Where(x => x.Names.Count != 0)
               .ToDictionary(x => x.Key, x => x.Names);
    }

    private Dictionary<string, List<string>> GetUniqueArtMapping()
    {
        Dictionary<string, List<string>> mapping = null;

        if (!Settings.UniqueIdentificationSettings.IgnoreGameUniqueArtMapping &&
            GameController.Files.UniqueItemDescriptions.EntriesList.Count != 0 &&
            GameController.Files.ItemVisualIdentities.EntriesList.Count != 0)
        {
            mapping = GetGameFileUniqueArtMapping();
        }

        var customFilePath = Path.Join(DirectoryFullName, CustomUniqueArtMappingPath);

        if (File.Exists(customFilePath))
        {
            try
            {
                mapping ??= JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(
                    File.ReadAllText(customFilePath)
                );
            }
            catch (Exception ex)
            {
                LogError($"Unable to load custom art mapping: {ex}");
            }
        }

        mapping ??= GetEmbeddedUniqueArtMapping();
        mapping ??= [];
        return mapping;
    }

    private Dictionary<string, List<string>> GetEmbeddedUniqueArtMapping()
    {
        try
        {
            using var stream = Assembly
                               .GetExecutingAssembly()
                               .GetManifestResourceStream(DefaultUniqueArtMappingPath);

            if (stream == null)
            {
                if (Settings.Debug)
                {
                    LogMessage($"Embedded stream {DefaultUniqueArtMappingPath} is missing");
                }

                return null;
            }

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(content);
        }
        catch (Exception ex)
        {
            LogError($"Unable to load embedded art mapping: {ex}");
            return null;
        }
    }

    public override void OnLoad()
    {
        Graphics.InitImage("directions.png");
    }

    public override void AreaChange(AreaInstance area)
    {
        UniqueArtMapping = GetUniqueArtMapping();
        _storedCustomItems.Clear();
    }

    public override Job Tick()
    {
        _largeMap = GameController.IngameState.IngameUi.Map.LargeMap;
        UpdateStoredItems(false);
        return null;
    }

    public override void Render()
    {
        var inGameUi = GameController.Game.IngameState.IngameUi;

        if (!Settings.IgnoreFullscreenPanels && inGameUi.FullscreenPanels.Any(x => x.IsVisible))
        {
            return;
        }

        if (!Settings.IgnoreRightPanels && inGameUi.OpenRightPanel.IsVisible)
        {
            return;
        }

        //var socketColors = new List<string>();

        //if (GameController.IngameState.UIHover is { Address: not 0 } h && h.Entity.IsValid)
        //{
        //    var entity = GameController.IngameState.UIHover?.Entity;
        //    if (entity.TryGetComponent<Sockets>(out var sockets))
        //    {
        //        socketColors = sockets.SocketGroup;

        //        BaseItemType baseItemType = GameController.Files.BaseItemTypes.Translate(entity.Path);
        //        if (baseItemType != null)
        //        {
        //            SocketEmulation(socketColors, baseItemType.Width == 1);
        //        }
        //    }
        //}
        List<CustomItemData> wantedItems;

        if (Settings.OrderByDistance)
        {
            wantedItems = _storedCustomItems
                          .Where(item => item.IsWanted == true)
                          .OrderBy(group => group.DistanceCustom)
                          .ToList();
        }
        else
        {
            wantedItems = _storedCustomItems
                          .Where(item => item.IsWanted == true)
                          .ToList();
        }

        if (wantedItems.Count <= 0)
        {
            return;
        }

        {
            var playerPos = GameController.Player.GridPosNum;
            var position = GameController.UnderPanel.StartDrawPoint.ToVector2Num();
            position.X += Settings.LabelShift;

            var defaultAlertDrawStyle = new AlertDrawStyle(
                "<SOMETHINGS WRONG>",
                Color.White,
                Settings.BorderWidth,
                Color.White,
                Color.Black
            );

            if (Settings.EnableTextDrawing)
            {
                foreach (var entity in wantedItems)
                {
                    var text = entity.UniqueNameCandidates.Count != 0
                        ? string.Join(" \\\n", entity.UniqueNameCandidates) : entity.LabelText;

                    var alertDrawStyle = defaultAlertDrawStyle with
                    {
                        Text = text,
                        TextColor = entity.TextColor,
                        BackgroundColor = entity.BackgroundColor,
                        BorderColor = entity.BorderColor
                    };

                    position = DrawText(
                        playerPos,
                        position,
                        Settings.ItemSpacing,
                        alertDrawStyle,
                        entity
                    );
                }
            }

            if (!Settings.EnableMapDrawing || !_largeMap.IsVisible)
            {
                return;
            }

            foreach (var item in wantedItems)
                Graphics.DrawLine(
                    GameController.IngameState.Data.GetGridMapScreenPosition(item.Location),
                    GameController.IngameState.Data.GetGridMapScreenPosition(GameController.Player.GridPosNum),
                    Settings.MapLineThickness,
                    Settings.MapLineColor
                );
        }
    }

    private Vector2N DrawText(Vector2N playerPos, Vector2N position, float bottomMargin, AlertDrawStyle drawStyle,
        CustomItemData entity)
    {
        var delta = entity.Location - playerPos;
        var itemSize = DrawItem(drawStyle, delta, position, drawStyle.Text, entity);

        if (itemSize != 0)
        {
            position.Y += itemSize + bottomMargin;
        }

        return position;
    }

    private float DrawItem(AlertDrawStyle drawStyle, Vector2N delta, Vector2N position, string text, CustomItemData entity)
    {
        var padding = Settings.TextPadding.Value;
        var compassOffset = Settings.DrawCompass ? Settings.TextSize * ImGui.GetFontSize() * 2 : 0;
        position += new Vector2N(-drawStyle.BorderWidth - compassOffset, drawStyle.BorderWidth);
        var distance = delta.GetPolarCoordinates(out var phi);
        var sockets = entity.SocketInfo.SocketNumber;
        float singleRowTextHeight;

        using (Graphics.SetTextScale(Settings.TextSize))
        {
            singleRowTextHeight = Graphics.MeasureText("aAyY").Y;
        }

        var enableSocketDisplay = sockets > 0 && Settings.SocketDisplaySettings.ShowSockets;

        int socketWidth;
        int socketHeight;
        int socketPadding;

        if (enableSocketDisplay)
        {
            int NSocketSpace(int socketCount) => Settings.SocketDisplaySettings.SocketSize * socketCount + Settings.SocketDisplaySettings.SocketSpacing * (socketCount - 1);
            socketPadding = Settings.SocketDisplaySettings.SocketPadding;
            if (entity.Width == 1 || sockets == 1)
            {
                socketWidth = NSocketSpace(1);
                socketHeight = NSocketSpace(sockets);
            }
            else
            {
                socketWidth = NSocketSpace(2);
                socketHeight = sockets switch
                {
                    < 3 => NSocketSpace(1),
                    < 5 => NSocketSpace(2),
                    _ => NSocketSpace(3)
                };
            }
        }
        else
        {
            socketHeight = 0;
            socketWidth = 0;
            socketPadding = 0;
        }

        var isDefaultFont = string.IsNullOrEmpty(Settings.FontOverride.Value);
        var baseTextSize = isDefaultFont
            ? Graphics.MeasureText(text)
            : Graphics.MeasureText(text, Settings.FontOverride.Value);
        float actualTextScale = isDefaultFont || Settings.ScaleFontWhenCustom
            ? Settings.TextSize
            : 1;
        var textSize = baseTextSize * actualTextScale;

        var socketAreaWidth = socketWidth + (enableSocketDisplay ? Math.Max(padding.X, socketPadding) + socketPadding : padding.X);
        var fullWidth = textSize.X + padding.X + socketAreaWidth;
        var textHeightWithPadding = textSize.Y + padding.Y * 2;
        var fullHeight = Math.Max(textHeightWithPadding, socketHeight + socketPadding * 2);

        var boxRect = new RectangleF(position.X - fullWidth, position.Y, fullWidth, fullHeight);
        Graphics.DrawBox(boxRect, drawStyle.BackgroundColor);
        if (drawStyle.BorderWidth > 0)
        {
            var frameRect = boxRect;
            frameRect.Inflate(drawStyle.BorderWidth, drawStyle.BorderWidth);
            Graphics.DrawFrame(frameRect, drawStyle.BorderColor, drawStyle.BorderWidth);
        }

        using (Graphics.SetTextScale(actualTextScale))
        {
            var textPos = position + new Vector2N(-socketAreaWidth, fullHeight / 2 - textSize.Y / 2);

            float DrawLine(string line, Vector2N pos) =>
                isDefaultFont 
                    ? Graphics.DrawText(line, pos, drawStyle.TextColor, FontAlign.Right).Y 
                    : Graphics.DrawText(line, pos, drawStyle.TextColor, Settings.FontOverride.Value, FontAlign.Right).Y;

            if (Settings.AlignItemTextToCenter)
            {
                foreach (var line in text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var lineSize = isDefaultFont 
                        ? Graphics.MeasureText(line) 
                        : Graphics.MeasureText(line, Settings.FontOverride.Value);
                    textPos.Y += DrawLine(line, textPos + new Vector2N(-textSize.X / 2 + lineSize.X / 2, 0));
                }
            }
            else
            {
                DrawLine(text, textPos);
            }
        }

        if (Settings.DrawCompass)
        {
            var compassUv = MathHepler.GetDirectionsUV(phi, distance);
            var compassRect = new RectangleF(
                position.X + drawStyle.BorderWidth + compassOffset / 2 - singleRowTextHeight / 2,
                Settings.AlignCompassToCenter ? boxRect.Center.Y - singleRowTextHeight / 2 : boxRect.Top,
                singleRowTextHeight,
                singleRowTextHeight
            );

            Graphics.DrawImage("directions.png", compassRect, compassUv);
        }

        if (enableSocketDisplay)
        {
            var socketStartingPoint = new Vector2N(
                boxRect.Right - socketWidth - socketPadding,
                boxRect.Center.Y - socketHeight / 2f
            );

            SocketEmulation(entity.SocketInfo.SocketGroups, socketStartingPoint, entity.Width == 1);
        }

        return fullHeight + drawStyle.BorderWidth * 2;
    }

    private void UpdateStoredItems(bool forceUpdate) => UpdateStoredItems(forceUpdate, false);

    private void UpdateStoredItems(bool forceUpdate, bool doProfiler)
    {
        if (_timer.ElapsedMilliseconds <= Settings.UpdateTimer && !forceUpdate)
        {
            return;
        }

        var validWorldItems = Settings.UseFastLabelList
            ? GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelElement.VisibleGroundItemLabels?.Select(x => new { x.Entity, x.Label }).ToList() ?? []
            : GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Select(x => new { Entity = x.ItemOnGround, x.Label }).ToList() ?? [];
        var profilerTotal = doProfiler ? Stopwatch.StartNew() : null;
        var profilerModifyStored = doProfiler ? Stopwatch.StartNew() : null;

        if (validWorldItems != null && GameController?.Files != null)
        {
            var validWorldItemIds = new HashSet<long>(validWorldItems.Select(e => e.Label.Address));
            _storedCustomItems.RemoveWhere(item => !validWorldItemIds.Contains(item.LabelAddress));
            var existingItemAddresses = new HashSet<long>(_storedCustomItems.Select(item => item.LabelAddress));

            foreach (var itemInfo in validWorldItems)
                if (!existingItemAddresses.Contains(itemInfo.Label.Address) &&
                    itemInfo.Entity.TryGetComponent<WorldItem>(out var worldItem))
                {
                    _storedCustomItems.Add(
                        new CustomItemData(
                            worldItem.ItemEntity,
                            itemInfo.Entity,
                            itemInfo.Label,
                            GameController,
                            UniqueArtMapping
                        )
                    );
                }
        }

        profilerModifyStored?.Stop();
        var profilerModifyLoopStored = doProfiler ? Stopwatch.StartNew() : null;
        var profilerIsInFilter = doProfiler ? Stopwatch.StartNew() : null;

        foreach (var item in _storedCustomItems)
        {
            if (item.WasDynamicallyUpdated)
            {
                item.IsWanted = null;
                item.WasDynamicallyUpdated = false;
            }

            item.UpdateDynamicCustomData();

            profilerIsInFilter?.Start();
            item.IsWanted ??= ItemInFilter(item);
            profilerIsInFilter?.Stop();
        }

        if (doProfiler)
        {
            UpdateProfilerResults(profilerModifyLoopStored, profilerTotal, profilerModifyStored, profilerIsInFilter);
        }

        _timer.Restart();
    }

    private static void UpdateProfilerResults(Stopwatch profilerModifyLoopStored, Stopwatch profilerTotal, Stopwatch profilerModifyStored, Stopwatch profilerIsInFilter)
    {
        profilerModifyLoopStored?.Stop();
        profilerTotal?.Stop();

        var logLines = new List<string>
        {
            // Add headers
            "Profiler | Ticks | Nanoseconds (ns) | Milliseconds (ms)",
            new('-', 60) // Temporary separator length
        };

        // Add profiler results
        AddProfilerResult("Modify Stored", profilerModifyStored);
        AddProfilerResult("Modify Loop Stored", profilerModifyLoopStored);
        AddProfilerResult("Check Is Wanted", profilerIsInFilter);
        AddProfilerResult("Total", profilerTotal);

        // Calculate the maximum width for each column
        var columnWidths = CalculateColumnWidths(logLines);

        // Adjust and print each log line
        foreach (var line in logLines)
            DebugWindow.LogMsg(FormatLine(line, columnWidths), 10);

        void AddProfilerResult(string profilerName, Stopwatch profiler)
        {
            var ticks = profiler.ElapsedTicks;
            var nanoseconds = (double)ticks / Stopwatch.Frequency * 1_000_000_000;
            var milliseconds = profiler.Elapsed.TotalMilliseconds;
            logLines.Add($"{profilerName} | {ticks} | {nanoseconds:N0} | {milliseconds:N2}");
        }

        int[] CalculateColumnWidths(IEnumerable<string> lines)
        {
            return lines
                .Select(line => line.Split('|'))
                .Where(columns => columns.Length == 4)
                .Select(columns => columns.Select(c => c.Trim().Length).ToList())
                .Aggregate((int[]) [0, 0, 0, 0], (max, columns) => max.Zip(columns, Math.Max).ToArray());
        }

        string FormatLine(string line, IReadOnlyList<int> widths)
        {
            var columns = line.Split('|');

            return columns.Length == 4
                ? $"{columns[0].Trim().PadRight(widths[0])} | {columns[1].Trim().PadLeft(widths[1])} | {columns[2].Trim().PadLeft(widths[2])} | {columns[3].Trim().PadLeft(widths[3])}"
                : line; // Return the line as-is for headers and separators
        }
    }

    private bool ItemInFilter(ItemData item)
    {
        return _itemFilters != null && _itemFilters.Any(filter => filter.Matches(item));
    }

    #region Socket and Link Emulation

    public void SocketEmulation(List<string> socketGroups, bool oneHander)
    {
        var startingPoint = new Vector2N(
            GameController.IngameState.MousePosX + 30,
            GameController.IngameState.MousePosY
        );

        SocketEmulation(socketGroups, startingPoint, oneHander);
    }

    private static readonly Dictionary<int, Direction?> NormalSocketDirections = new()
    {
        [0] = Direction.Right,
        [1] = Direction.Down,
        [2] = Direction.Left,
        [3] = Direction.Down,
        [4] = Direction.Right,
    };

    private static readonly Dictionary<int, Direction?> OneHandedSocketDirections = new()
    {
        [0] = Direction.Down,
        [1] = Direction.Down,
    };

    public void SocketEmulation(IEnumerable<string> socketGroups, Vector2N startingPoint, bool oneHander)
    {
        var socketDisplaySettings = Settings.SocketDisplaySettings;
        var socketPosDiff = socketDisplaySettings.SocketSize + socketDisplaySettings.SocketSpacing;

        var sockets = new List<Socket>();
        Direction IndexToDirection(int index) => (oneHander ? OneHandedSocketDirections : NormalSocketDirections).GetValueOrDefault(index) ?? Direction.None;
        var currentPosition = Vector2N.Zero;
        var socketIndex = 0;

        // Parse socket information and create sockets
        foreach (var socketItem in socketGroups)
            for (var charIndex = 0; charIndex < socketItem.Length; charIndex++)
            {
                var charColor = socketItem[charIndex];
                var trueDirection = IndexToDirection(socketIndex);
                var drawDirection = charIndex == socketItem.Length - 1 ? Direction.None : trueDirection;
                var socket = new Socket(GetSocketColor(charColor), currentPosition, drawDirection, socketDisplaySettings.LinkColor);
                currentPosition += trueDirection switch
                {
                    Direction.Right => Vector2N.UnitX * socketPosDiff,
                    Direction.Down => Vector2N.UnitY * socketPosDiff,
                    Direction.Left => -Vector2N.UnitX * socketPosDiff,
                    _ => Vector2N.Zero,
                };

                sockets.Add(socket);
                socketIndex += 1;
            }

        SetSocketConnections(sockets);

        foreach (var socket in sockets)
            socket.Draw(new Vector2N(socketDisplaySettings.SocketSize, socketDisplaySettings.SocketSize), socketDisplaySettings.LinkWidth, startingPoint);
    }

    private Color GetSocketColor(char charColor)
    {
        return charColor switch
        {
            'R' => (Color)Settings.SocketDisplaySettings.RedSocketColor,
            'G' => (Color)Settings.SocketDisplaySettings.GreenSocketColor,
            'B' => (Color)Settings.SocketDisplaySettings.BlueSocketColor,
            'W' => (Color)Settings.SocketDisplaySettings.WhiteSocketColor,
            'A' => (Color)Settings.SocketDisplaySettings.AbyssalSocketColor,
            'O' => (Color)Settings.SocketDisplaySettings.ResonatorSocketColor,
            _ => Color.Black
        };
    }

    private static void SetSocketConnections(IReadOnlyList<Socket> sockets)
    {
        for (var i = 0; i < sockets.Count - 1; i++)
        {
            sockets[i].Link = sockets[i + 1];
        }
    }

    public record Socket(Color Color, Vector2N Position, Direction Direction, Color LinkColor)
    {
        public Socket Link { get; set; }

        public void Draw(Vector2N boxSize, float linkWidth, Vector2N startDrawLocation)
        {
            var drawPosition = startDrawLocation + Position;

            DrawLineToNextSocketIfPresent(boxSize, startDrawLocation, drawPosition, linkWidth);
            DrawBoxAtPosition(boxSize, drawPosition);
        }

        private void DrawBoxAtPosition(Vector2N boxSize, Vector2N drawPosition)
        {
            DrawBox(new RectangleF(drawPosition.X, drawPosition.Y, boxSize.X, boxSize.Y), Color);
        }

        private void DrawLineToNextSocketIfPresent(Vector2N boxSize, Vector2N startDrawLocation, Vector2N drawPosition, float linkWidth)
        {
            if (Link == null)
            {
                return;
            }

            if (Direction != Direction.None)
            {
                DrawLine(drawPosition + boxSize / 2, startDrawLocation + Link.Position + boxSize / 2, linkWidth);
            }
        }

        public void DrawBox(RectangleF rect, Color color)
        {
            // Your own DrawBox implementation
            _graphics.DrawBox(rect, color);
        }

        public void DrawLine(Vector2N p1, Vector2N p2, float borderWidth)
        {
            // Your own DrawLine implementation
            _graphics.DrawLine(p1, p2, borderWidth, LinkColor);
        }
    }

    public enum Direction
    {
        None,
        Right,
        Down,
        Left
    }

    #endregion Socket and Link Emulation

    #region Rule Drawing and Loading

    public override void DrawSettings()
    {
        base.DrawSettings();

        if (ImGui.Button("Clear StoredCustomItems and ReRun (PROFILER)"))
        {
            _storedCustomItems.Clear();
            UpdateStoredItems(true, true);
        }

        if (ImGui.Button("Recheck all StoredCustomItems for IsWanted (PROFILER)"))
        {
            foreach (var item in _storedCustomItems)
            {
                item.IsWanted = null;
                item.WasDynamicallyUpdated = false;
            }

            UpdateStoredItems(true, true);
        }

        if (ImGui.Button("Open Build Folder"))
        {
            var configDir = ConfigDirectory;

            var customConfigFileDirectory = !string.IsNullOrEmpty(Settings.CustomConfigDir) ? Path.Combine(
                Path.GetDirectoryName(ConfigDirectory),
                Settings.CustomConfigDir
            ) : null;

            var directoryToOpen = Directory.Exists(customConfigFileDirectory) ? customConfigFileDirectory : configDir;
            Process.Start("explorer.exe", directoryToOpen);
        }

        ImGui.Separator();
        DrawGroundRulesOptions();
    }

    private void DrawGroundRulesOptions()
    {
        ImGui.BulletText("Select Rules To Load");

        ImGui.BulletText(
            "Ordering rule sets so general items will match first rather than last will improve performance"
        );

        var tempNPCInvRules = new List<GroundRule>(Settings.GroundRules); // Create a copy

        for (var i = 0; i < tempNPCInvRules.Count; i++)
        {
            DrawRuleMovementButtons(tempNPCInvRules, i);
            DrawRuleCheckbox(tempNPCInvRules, i);
        }

        Settings.GroundRules = tempNPCInvRules;
    }

    private static void DrawRuleMovementButtons(IList<GroundRule> rules, int i)
    {
        if (ImGui.ArrowButton($"##upButton{i}", ImGuiDir.Up) && i > 0)
        {
            (rules[i - 1], rules[i]) = (rules[i], rules[i - 1]);
        }

        ImGui.SameLine();
        ImGui.Text(" ");
        ImGui.SameLine();

        if (ImGui.ArrowButton($"##downButton{i}", ImGuiDir.Down) && i < rules.Count - 1)
        {
            (rules[i + 1], rules[i]) = (rules[i], rules[i + 1]);
        }

        ImGui.SameLine();
        ImGui.Text(" - ");
        ImGui.SameLine();
    }

    private void DrawRuleCheckbox(IReadOnlyList<GroundRule> rules, int i)
    {
        var refToggle = rules[i].Enabled;

        if (!ImGui.Checkbox($"{rules[i].Name}##checkbox{i}", ref refToggle))
        {
            return;
        }

        rules[i].Enabled = refToggle;
        LoadRuleFiles();
    }

    private void LoadRuleFiles()
    {
        var pickitConfigFileDirectory = ConfigDirectory;
        var existingRules = Settings.GroundRules;

        if (!string.IsNullOrEmpty(Settings.CustomConfigDir))
        {
            var customConfigFileDirectory = Path.Combine(
                Path.GetDirectoryName(ConfigDirectory),
                Settings.CustomConfigDir
            );

            if (Directory.Exists(customConfigFileDirectory))
            {
                pickitConfigFileDirectory = customConfigFileDirectory;
            }
            else
            {
                DebugWindow.LogError("[Ground Items] custom config folder does not exist.", 15);
            }
        }

        try
        {
            var newRules = new DirectoryInfo(pickitConfigFileDirectory)
                           .GetFiles("*.ifl")
                           .Select(
                               fileInfo => new GroundRule(
                                   fileInfo.Name,
                                   Path.GetRelativePath(pickitConfigFileDirectory, fileInfo.FullName),
                                   false
                               )
                           )
                           .ExceptBy(existingRules.Select(rule => rule.Location), groundRule => groundRule.Location)
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
                           .Select(
                               rule => ItemFilter.LoadFromPath(Path.Combine(pickitConfigFileDirectory, rule.Location))
                           )
                           .ToList();

            Settings.GroundRules = newRules;
        }
        catch (Exception e)
        {
            LogError($"An error occurred while loading rule files: {e.Message}");
        }

        _storedCustomItems.Clear();
        UpdateStoredItems(true);
    }

    #endregion Rule Drawing and Loading
}