using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using ItemFilterLibrary;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2N = System.Numerics.Vector2;

namespace Ground_Items_With_Linq;

public class Ground_Items_With_Linq : BaseSettingsPlugin<Ground_Items_With_LinqSettings>
{
    private readonly HashSet<CustomItemData> StoredCustomItems = new HashSet<CustomItemData>();
    public static Graphics _graphics;
    private readonly Stopwatch _timer = Stopwatch.StartNew();

    private List<ItemFilter> _itemFilters;
    private Element LargeMap;

    public Ground_Items_With_Linq()
    {
        Name = "Ground Items With Linq";
    }

    public override bool Initialise()
    {
        _graphics = this.Graphics;
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

        return null;
    }

    public override void Render()
    {
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

        var wantedItems = new List<CustomItemData>();

        if (Settings.OrderByDistance)
        {
            wantedItems = StoredCustomItems.Where(item => item.IsWanted == true).OrderBy(group => group.DistanceCustom).ToList();
        }
        else
        {
            wantedItems = StoredCustomItems.Where(item => item.IsWanted == true).ToList();
        }

        if (wantedItems.Count > 0)
        {
            var playerPos = GameController.Player.GridPosNum;
            var position = GameController.UnderPanel.StartDrawPoint.ToVector2Num();
            position.X += Settings.LabelShift;

            var defaultAlertDrawStyle = new AlertDrawStyle("<SOMETHINGS WRONG>", Color.White, 1, Color.White, Color.Black);

            if (Settings.EnableTextDrawing)
            {
                foreach (var entity in wantedItems)
                {
                    var alertDrawStyle = defaultAlertDrawStyle with { Text = entity.LabelText, TextColor = entity.TextColor, BackgroundColor = entity.BackgroundColor, BorderColor = entity.BorderColor };
                    position = DrawText(playerPos, position, Settings.TextPadding * Settings.TextSize, alertDrawStyle, entity);
                }
            }

            if (Settings.EnableMapDrawing && LargeMap.IsVisible)
                foreach (var item in wantedItems)
                {
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
        var itemSize = DrawItem(kv, delta, position, padding, kv.Text, entity);
        if (itemSize != new Vector2()) position.Y += itemSize.Y + BOTTOM_MARGIN;

        return position;
    }

    private Vector2 DrawItem(AlertDrawStyle drawStyle, Vector2N delta, Vector2N position, Vector2N padding, string text, CustomItemData entity)
    {
        padding.X -= drawStyle.BorderWidth;
        padding.Y -= drawStyle.BorderWidth;
        double phi;
        var distance = delta.GetPolarCoordinates(out phi);
        var socketBorderSpacing = 5;
        var textToBorderSpacing = 2;

        int sockets = entity.SocketInfo.SocketNumber;
        Vector2N singleRowText = new Vector2N(0, 0);
        using (Graphics.SetTextScale(Settings.TextSize))
        {
            singleRowText = Graphics.MeasureText("aAyY");
        }
        if (sockets > 0)
        {
            var socketsSpacing = (Settings.EmuSocketSize * 2) + Settings.EmuSocketSpacing + socketBorderSpacing;
            var socketsSize = sockets < 5 ? Settings.EmuSocketSize * 2 : Settings.EmuSocketSize * 3;
            var socketsSpacingHeight = sockets < 5 ? Settings.EmuSocketSpacing : Settings.EmuSocketSpacing * 2;

            if (entity.Width == 1 || sockets == 1)
            {
                socketsSpacing = Settings.EmuSocketSize + socketBorderSpacing; // width of socket drawing
                socketsSize = Settings.EmuSocketSize * sockets; // amount of sockets for height
                socketsSpacingHeight = Settings.EmuSocketSpacing * 2; // amount of link spacing (socket total - 1)
            }

            float compassOffset = 0 + (Settings.TextSize * ImGui.GetFontSize() * 2);
            var textPos = position.Translate(-padding.X - compassOffset - socketsSpacing, padding.Y + textToBorderSpacing);
            Vector2N textSize = new Vector2N(0, 0);

            switch (Settings.ScaleFontWhenCustom.Value)
            {
                case true:
                    using (Graphics.SetTextScale(Settings.TextSize))
                        if (string.IsNullOrEmpty(Settings.FontOverride.Value))
                            textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, FontAlign.Right);
                        else
                            textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, Settings.FontOverride.Value, FontAlign.Right);
                    break;

                case false:
                    if (string.IsNullOrEmpty(Settings.FontOverride.Value))
                        using (Graphics.SetTextScale(Settings.TextSize))
                            textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, FontAlign.Right);
                    else
                        textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, Settings.FontOverride.Value, FontAlign.Right);
                    break;
            }

            var fullWidth = textSize.X + textToBorderSpacing * padding.X + textToBorderSpacing * drawStyle.BorderWidth + compassOffset + socketsSpacing;
            var socketsHeight = socketsSize + socketsSpacingHeight + socketBorderSpacing;
            var actualFullHeight = textSize.Y + textToBorderSpacing * padding.Y * drawStyle.BorderWidth;
            var socketOverflow = Math.Max(0, socketsHeight - (actualFullHeight - socketBorderSpacing));
            var fullHeight = actualFullHeight + Math.Max(socketBorderSpacing, socketOverflow);
            var boxRect = new RectangleF(position.X - fullWidth, position.Y, fullWidth - compassOffset, fullHeight);
            Graphics.DrawBox(boxRect, drawStyle.BackgroundColor);
            var rectUV = MathHepler.GetDirectionsUV(phi, distance);
            var rectangleF = new RectangleF(position.X - padding.X - compassOffset + 6, position.Y + padding.Y, singleRowText.Y, singleRowText.Y);
            Graphics.DrawImage("directions.png", rectangleF, rectUV);

            if (drawStyle.BorderWidth > 0)
            {
                Graphics.DrawFrame(boxRect, drawStyle.BorderColor, drawStyle.BorderWidth);
            }

            var socketStartingPoint = new Vector2N(boxRect.TopRight.X - socketsSpacing, boxRect.TopRight.Y + socketBorderSpacing);
            SocketEmulation(entity.SocketInfo.SocketGroups.ToList(), socketStartingPoint, entity.Width == 1);

            return new Vector2(fullWidth, fullHeight);
        }
        else
        {
            float compassOffset = 0 + (Settings.TextSize * ImGui.GetFontSize() * 2);
            var textPos = position.Translate(-padding.X - compassOffset + 1, padding.Y + textToBorderSpacing);
            Vector2N textSize = new Vector2N(0, 0);

            switch (Settings.ScaleFontWhenCustom.Value)
            {
                case true:
                    using (Graphics.SetTextScale(Settings.TextSize))
                        if (string.IsNullOrEmpty(Settings.FontOverride.Value))
                            textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, FontAlign.Right);
                        else
                            textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, Settings.FontOverride.Value, FontAlign.Right);
                    break;

                case false:
                    if (string.IsNullOrEmpty(Settings.FontOverride.Value))
                        using (Graphics.SetTextScale(Settings.TextSize))
                            textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, FontAlign.Right);
                    else
                        textSize = Graphics.DrawText(text, textPos, drawStyle.TextColor, Settings.FontOverride.Value, FontAlign.Right);
                    break;
            }

            var fullHeight = textSize.Y + textToBorderSpacing * padding.Y + textToBorderSpacing + textToBorderSpacing * drawStyle.BorderWidth;
            var fullWidth = textSize.X + textToBorderSpacing * padding.X * drawStyle.BorderWidth + compassOffset;
            var boxRect = new RectangleF(position.X - fullWidth, position.Y, fullWidth - compassOffset, fullHeight);
            Graphics.DrawBox(boxRect, drawStyle.BackgroundColor);
            var rectUV = MathHepler.GetDirectionsUV(phi, distance);
            var rectangleF = new RectangleF(position.X - padding.X - compassOffset + 6, position.Y + padding.Y, singleRowText.Y, singleRowText.Y);
            Graphics.DrawImage("directions.png", rectangleF, rectUV);

            if (drawStyle.BorderWidth > 0)
            {
                Graphics.DrawFrame(boxRect, drawStyle.BorderColor, drawStyle.BorderWidth);
            }

            return new Vector2(fullWidth, fullHeight);
        }
    }

    private void UpdateStoredItems(bool forceUpdate) => UpdateStoredItems(forceUpdate, false);

    private void UpdateStoredItems(bool forceUpdate, bool doProfiler)
    {
        if (_timer.ElapsedMilliseconds <= Settings.UpdateTimer && !forceUpdate) return;

        var ValidWorldItems = GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.ToList() ?? [];

        var profilerTotal = doProfiler ? Stopwatch.StartNew() : null;

        var profilerModifyStored = doProfiler ? Stopwatch.StartNew() : null;

        if (ValidWorldItems != null && GameController.Files != null)
        {
            var validWorldItemIds = new HashSet<long>(ValidWorldItems.Select(e => e.Address));

            StoredCustomItems.RemoveWhere(item => !validWorldItemIds.Contains(item.LabelAddress));

            var existingItemAddresses = new HashSet<long>(StoredCustomItems.Select(item => item.LabelAddress));

            foreach (var entity in ValidWorldItems)
            {
                if (!existingItemAddresses.Contains(entity.Address) && entity.ItemOnGround.TryGetComponent<WorldItem>(out var worldItem))
                {
                    StoredCustomItems.Add(new CustomItemData(worldItem.ItemEntity, entity.ItemOnGround, entity, GameController));
                }
            }
        }

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

            if (doProfiler)
                profilerIsInFilter.Start();

            item.IsWanted ??= ItemInFilter(item);

            if (doProfiler)
                profilerIsInFilter.Stop();
        }

        if (doProfiler)
        {
            profilerModifyLoopStored?.Stop();
            profilerTotal?.Stop();

            var logLines = new List<string>
            {
                // Add headers
                "Profiler | Ticks | Nanoseconds (ns) | Milliseconds (ms)",
                new string('-', 60) // Temporary separator length
            };

            // Add profiler results
            AddProfilerResult("Modify Stored", profilerModifyStored, logLines);
            AddProfilerResult("Modify Loop Stored", profilerModifyLoopStored, logLines);
            AddProfilerResult("Check Is Wanted", profilerIsInFilter, logLines);
            AddProfilerResult("Total", profilerTotal, logLines);

            // Calculate the maximum width for each column
            int[] columnWidths = CalculateColumnWidths(logLines);

            // Adjust and print each log line
            foreach (var line in logLines)
            {
                DebugWindow.LogMsg(FormatLine(line, columnWidths), 10);
            }
        }

        void AddProfilerResult(string profilerName, Stopwatch profiler, List<string> logLines)
        {
            long ticks = profiler.ElapsedTicks;
            double nanoseconds = (double)ticks / Stopwatch.Frequency * 1_000_000_000;
            double milliseconds = profiler.Elapsed.TotalMilliseconds;

            logLines.Add($"{profilerName} | {ticks} | {nanoseconds:N0} | {milliseconds:N2}");
        }

        int[] CalculateColumnWidths(List<string> lines)
        {
            int maxCol1 = 0, maxCol2 = 0, maxCol3 = 0, maxCol4 = 0;

            foreach (var line in lines)
            {
                var columns = line.Split('|');
                if (columns.Length == 4) // Ensure there are 4 columns
                {
                    maxCol1 = Math.Max(maxCol1, columns[0].Trim().Length);
                    maxCol2 = Math.Max(maxCol2, columns[1].Trim().Length);
                    maxCol3 = Math.Max(maxCol3, columns[2].Trim().Length);
                    maxCol4 = Math.Max(maxCol4, columns[3].Trim().Length);
                }
            }

            return new int[] { maxCol1, maxCol2, maxCol3, maxCol4 };
        }

        string FormatLine(string line, int[] widths)
        {
            var columns = line.Split('|');
            if (columns.Length == 4)
            {
                return $"{columns[0].Trim().PadRight(widths[0])} | {columns[1].Trim().PadLeft(widths[1])} | {columns[2].Trim().PadLeft(widths[2])} | {columns[3].Trim().PadLeft(widths[3])}";
            }
            else
            {
                return line; // Return the line as-is for headers and separators
            }
        }

        _timer.Restart();
    }

    private bool ItemInFilter(ItemData item)
    {
        return _itemFilters != null &&
               _itemFilters.Any(filter => filter.Matches(item));
    }

    #region Socket and Link Emulation

    public void SocketEmulation(List<string> socketGroups, bool oneHander)
    {
        var startingPoint = new Vector2N(GameController.IngameState.MousePosX + 30, GameController.IngameState.MousePosY);
        SocketEmulation(socketGroups, startingPoint, oneHander);
    }

    public void SocketEmulation(List<string> socketGroups, Vector2N startingPoint, bool oneHander)
    {
        var socketSize = Settings.EmuSocketSize;
        var spacing = Settings.EmuSocketSpacing;

        var socketLayout = new List<Vector2N>
            {
                new Vector2N(0, 0),
                new Vector2N(0 + socketSize + spacing, 0),
                new Vector2N(socketSize + spacing, socketSize + spacing),
                new Vector2N(0, socketSize + spacing),
                new Vector2N(0, (socketSize + spacing) * 2),
                new Vector2N(socketSize + spacing, (socketSize + spacing) * 2),
            };

        if (oneHander)
        {
            socketLayout = new List<Vector2N>
            {
                new Vector2N(0, 0),
                new Vector2N(0, socketSize + spacing),
                new Vector2N(0, (socketSize + spacing) * 2),
            };
        }

        var sockets = new List<Socket>();
        var currentGroup = 0;

        var socketIndex = 0;
        // Parse socket information and create sockets
        foreach (string socketItem in socketGroups)
        {
            var group = socketItem;

            for (int charIndex = 0; charIndex < group.Length; charIndex++)
            {
                var charColor = group[charIndex];
                var currentPosition = socketLayout[socketIndex];
                var direction = Direction.None; // Default direction

                switch (socketIndex)
                {
                    case 0:
                        direction = Direction.Right;
                        break;

                    case 1:
                        direction = Direction.Down;
                        break;

                    case 2:
                        direction = Direction.Left;
                        break;

                    case 3:
                        direction = Direction.Down;
                        break;

                    case 4:
                        direction = Direction.Right;
                        break;

                    case 5:
                        direction = Direction.None;
                        break;
                }

                if (oneHander)
                    direction = Direction.Down;

                var socket = new Socket(Color.White, currentPosition, direction, Settings.EmuLinkColor, oneHander);
                SetSocketColor(charColor, socket);

                if (charIndex == group.Length - 1)
                {
                    socket.Direction = Direction.None;
                }

                sockets.Add(socket);
                socketIndex += 1;
            }

            currentGroup++;
        }

        SetSocketConnections(sockets);

        foreach (var socket in sockets)
        {
            socket.Draw(new Size2F(socketSize, socketSize), socket.Color, startingPoint, oneHander);
        }
    }

    private void SetSocketColor(char charColor, Socket socket)
    {
        switch (charColor)
        {
            case 'R':
                socket.Color = Settings.EmuRedSocket;
                break;

            case 'G':
                socket.Color = Settings.EmuGreenSocket;
                break;

            case 'B':
                socket.Color = Settings.EmuBlueSocket;
                break;

            case 'W':
                socket.Color = Settings.EmuWhiteSocket;
                break;

            case 'A':
                socket.Color = Settings.EmuAbyssalSocket;
                break;

            case 'O':
                socket.Color = Settings.EmuResonatorSocket;
                break;

            default:
                socket.Color = Color.Black;
                break;
        }
    }

    private void SetSocketConnections(List<Socket> sockets)
    {
        for (int i = 0; i < sockets.Count - 1; i++)
        {
            if (i < sockets.Count - 1)
            {
                sockets[i].Link = sockets[i + 1];
            }
        }
    }

    public class Socket
    {
        public Color Color { get; set; }
        public Color LinkColor { get; set; }
        public Vector2N Position { get; set; }
        public Socket Link { get; set; }
        public Direction Direction { get; set; }
        public bool OneHander { get; set; }

        public Socket(Color color, Vector2N position, Direction direction, Color linkColor, bool oneHander)
        {
            Color = color;
            Position = position;
            Direction = direction;
            LinkColor = linkColor;
            OneHander = oneHander;
        }

        public void Draw(Size2F boxSize, Color color, Vector2N startDrawLocation, bool oneHander)
        {
            var newPosition = new Vector2N(startDrawLocation.X + Position.X, startDrawLocation.Y + Position.Y);
            if (oneHander)
                newPosition = new Vector2N(startDrawLocation.X, startDrawLocation.Y + Position.Y);

            DrawLineToNextSocketIfPresent(boxSize, startDrawLocation, newPosition);
            DrawBoxAtPosition(boxSize, color, newPosition);
        }

        private void DrawBoxAtPosition(Size2F boxSize, Color color, Vector2N newPosition)
        {
            // Draw box at current position
            DrawBox(new RectangleF(newPosition.X, newPosition.Y, boxSize.Width, boxSize.Height), color);
        }

        private void DrawLineToNextSocketIfPresent(Size2F boxSize, Vector2N startDrawLocation, Vector2N newPosition)
        {
            if (Link != null)
            {
                var linkPosition = new Vector2N(startDrawLocation.X + Link.Position.X, startDrawLocation.Y + Link.Position.Y);

                switch (Direction)
                {
                    case Direction.Right:
                        DrawLine(new Vector2N(startDrawLocation.X + Position.X + boxSize.Width / 2, newPosition.Y + boxSize.Height / 2),
                                 new Vector2N(linkPosition.X + boxSize.Width / 2, linkPosition.Y + boxSize.Width / 2), 4);
                        break;

                    case Direction.Down:
                        DrawLine(new Vector2N(startDrawLocation.X + Position.X + boxSize.Width / 2, startDrawLocation.Y + Position.Y + boxSize.Height / 2),
                                 new Vector2N(linkPosition.X + boxSize.Width / 2, linkPosition.Y + boxSize.Height / 2), 4);
                        break;

                    case Direction.Left:
                        DrawLine(new Vector2N(startDrawLocation.X + Position.X + boxSize.Width / 2, newPosition.Y + boxSize.Width / 2),
                                 new Vector2N(linkPosition.X + boxSize.Height / 2, linkPosition.Y + boxSize.Height / 2), 4);
                        break;

                    case Direction.None:
                        break;
                }
            }
        }

        public void DrawBox(RectangleF rect, Color color)
        {
            // Your own DrawBox implementation
            _graphics.DrawBox(rect, color);
        }

        public void DrawLine(Vector2N p1, Vector2N p2, int borderWidth)
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
            StoredCustomItems.Clear();

            UpdateStoredItems(true, true);
        }

        if (ImGui.Button("Recheck all StoredCustomItems for IsWanted (PROFILER)"))
        {
            foreach (var item in StoredCustomItems)
            {
                item.IsWanted = null;
                item.WasDynamicallyUpdated = false;
            }

            UpdateStoredItems(true, true);
        }

        if (ImGui.Button("Open Build Folder"))
        {
            var configDir = ConfigDirectory;
            var customConfigFileDirectory = !string.IsNullOrEmpty(Settings.CustomConfigDir)
                ? Path.Combine(Path.GetDirectoryName(ConfigDirectory), Settings.CustomConfigDir)
                : null;

            var directoryToOpen = Directory.Exists(customConfigFileDirectory)
                ? customConfigFileDirectory
                : configDir;

            Process.Start("explorer.exe", directoryToOpen);
        }

        ImGui.Separator();

        DrawGroundRulesOptions();
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
        }
    }

    private void LoadRuleFiles()
    {
        var pickitConfigFileDirectory = ConfigDirectory;
        var existingRules = Settings.GroundRules;

        if (!string.IsNullOrEmpty(Settings.CustomConfigDir))
        {
            var customConfigFileDirectory = Path.Combine(Path.GetDirectoryName(ConfigDirectory), Settings.CustomConfigDir);

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

        StoredCustomItems.Clear();
        UpdateStoredItems(true);
    }

    #endregion Rule Drawing and Loading
}