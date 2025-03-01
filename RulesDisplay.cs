using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using ExileCore;
using ImGuiNET;
using ItemFilterLibrary;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq;

public class RulesDisplay
{
    public static void DrawSettings()
    {
        ImGui.Separator();
        if (ImGui.Button("Open Filter Folder"))
        {
            var configDirectory = Main.ConfigDirectory;
            var customConfigDirectory = !string.IsNullOrEmpty(Main.Settings.CustomConfigDir)
                ? Path.Combine(Path.GetDirectoryName(Main.ConfigDirectory)!, Main.Settings.CustomConfigDir)
                : null;

            var directoryToOpen = Directory.Exists(customConfigDirectory)
                ? customConfigDirectory
                : configDirectory;

            Process.Start("explorer.exe", directoryToOpen);
        }

        if (ImGui.Button("Reload Rules"))
            LoadAndApplyRules();

        ImGui.Separator();
        ImGui.Text(
            "Rule Files\nFiles are loaded in order, so easier to process (common item queries hit more often that others) rule sets should be loaded first.");
        ImGui.Separator();

        if (ImGui.BeginTable("RulesTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Drag", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Toggle", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("File", ImGuiTableColumnFlags.None);
            ImGui.TableHeadersRow();

            var reorderPending = false;
            var pendingSrcIndex = -1;
            var pendingNewIndex = -1;

            var rules = Main.Settings.GroundRules;
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.PushID($"drag_{rule.Location}");

                var dropTargetStart = ImGui.GetCursorScreenPos();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                ImGui.Button("=", new Vector2(30, 20));
                ImGui.PopStyleColor();

                if (ImGui.BeginDragDropSource())
                {
                    ImGuiHelpers.SetDragDropPayload("RuleIndex", i);
                    ImGui.Text(rule.Name);
                    ImGui.EndDragDropSource();
                }
                else if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Drag me to reorder");
                }

                ImGui.SetCursorScreenPos(dropTargetStart);
                ImGui.InvisibleButton($"dropTarget_{rule.Location}", new Vector2(30, 20));

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGuiHelpers.AcceptDragDropPayload<int>("RuleIndex");
                    if (payload != null && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        var movedRule = rules[payload.Value];
                        rules.RemoveAt(payload.Value);
                        rules.Insert(i, movedRule);
                        LoadAndApplyRules();
                    }

                    ImGui.EndDragDropTarget();
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(1);
                ImGui.PushID($"toggle_{rule.Location}");
                var enabled = rule.Enabled;
                if (ImGui.Checkbox("", ref enabled))
                {
                    rule.Enabled = enabled;
                    LoadAndApplyRules();
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(2);
                ImGui.PushID(rule.Location);

                var directoryPart =
                    Path.GetDirectoryName(rule.Location)?.Replace("\\", "/") ?? "";
                var fileName = Path.GetFileName(rule.Location);
                var fileFullPath = Path.Combine(GetPickitConfigFileDirectory(), rule.Location);

                var cellWidth = ImGui.GetContentRegionAvail().X;

                ImGui.InvisibleButton($"FileCell_{rule.Location}", new Vector2(cellWidth, ImGui.GetFrameHeight()));

                ImGui.SameLine();

                StartContextMenu(fileName, fileFullPath, $"FileCell_{rule.Location}");

                var textPos = ImGui.GetItemRectMin();
                ImGui.SetCursorScreenPos(textPos);

                if (!string.IsNullOrEmpty(directoryPart))
                {
                    ImGui.TextColored(
                        new Vector4(0.4f, 0.7f, 1.0f, 1.0f), directoryPart + "/"
                    );
                    ImGui.SameLine(0, 0);
                    ImGui.Text(fileName);
                }
                else
                {
                    ImGui.Text(fileName);
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        void StartContextMenu(string fileName, string fileFullPath, string contextMenuId)
        {
            if (ImGui.BeginPopupContextItem(contextMenuId))
            {
                if (ImGui.MenuItem("Open"))
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fileFullPath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        DebugWindow.LogError(
                            $"[DrawSettings] Failed to open file: {ex.Message}",
                            10
                        );
                    }

                ImGui.EndPopup();
            }
        }
    }
    private static string GetPickitConfigFileDirectory()
    {
        var pickitConfigFileDirectory = Main.ConfigDirectory;
        if (!string.IsNullOrEmpty(Main.Settings.CustomConfigDir))
        {
            var customConfigFileDirectory = Path.Combine(Path.GetDirectoryName(Main.ConfigDirectory),
                Main.Settings.CustomConfigDir);
            if (Directory.Exists(customConfigFileDirectory))
                pickitConfigFileDirectory = customConfigFileDirectory;
            else
                DebugWindow.LogError("[Ground Items] Custom config folder does not exist.", 10);
        }

        return pickitConfigFileDirectory;
    }

    private static ItemFilter LoadItemFilterWithRetry(string rulePath)
    {
        const int maxRetries = 10;
        var attempt = 0;
        while (true)
            try
            {
                return ItemFilter.LoadFromPath(rulePath);
            }
            catch (IOException ex)
            {
                attempt++;
                if (attempt >= maxRetries)
                    throw new IOException($"Failed to load file after {maxRetries} attempts: {rulePath}", ex);
                Thread.Sleep(100);
            }
    }

    public static void LoadAndApplyRules()
    {
        var pickitConfigFileDirectory = GetPickitConfigFileDirectory();
        var existingRules = Main.Settings.GroundRules;
        try
        {
            var diskFiles = new DirectoryInfo(pickitConfigFileDirectory)
                .GetFiles("*.ifl", SearchOption.AllDirectories)
                .ToList();

            var newRules = diskFiles
                .Select(fileInfo => new GroundRule(
                    fileInfo.Name,
                    Path.GetRelativePath(pickitConfigFileDirectory, fileInfo.FullName),
                    false))
                .ExceptBy(existingRules.Select(rule => rule.Location), groundRule => groundRule.Location)
                .ToList();

            foreach (var groundRule in existingRules)
            {
                var fullPath = Path.Combine(pickitConfigFileDirectory, groundRule.Location);
                if (File.Exists(fullPath))
                    newRules.Add(groundRule);
                else
                    DebugWindow.LogError($"[LoadAndApplyRules] File '{groundRule.Name}' not found.", 10);
            }

            Main.ItemFilters = newRules
                .Where(rule => rule.Enabled)
                .Select(rule =>
                {
                    var rulePath = Path.Combine(pickitConfigFileDirectory, rule.Location);
                    return LoadItemFilterWithRetry(rulePath);
                })
                .ToList();

            Main.Settings.GroundRules = newRules;
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"[LoadAndApplyRules] An error occurred while loading rule files: {e.Message}", 10);
        }

        Main.StoredCustomItems.Clear();
        Main.UpdateStoredItems(true);
    }
}