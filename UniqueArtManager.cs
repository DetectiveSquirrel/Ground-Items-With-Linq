using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExileCore;
using Newtonsoft.Json;
using static Ground_Items_With_Linq.GroundItemsWithLinq;

namespace Ground_Items_With_Linq;

public class UniqueArtManager()
{
    public static Dictionary<string, List<string>> GetGameFileUniqueArtMapping()
    {
        if (Main.GameController.Files.UniqueItemDescriptions.EntriesList.Count == 0)
            Main.GameController.Files.LoadFiles();

        return Main.GameController
            .Files.ItemVisualIdentities.EntriesList
            .Where(x => x.ArtPath != null)
            .GroupJoin(
                Main.GameController.Files.UniqueItemDescriptions.EntriesList
                    .Where(x => x.ItemVisualIdentity != null),
                x => x,
                x => x.ItemVisualIdentity,
                (ivi, descriptions) => (ivi.ArtPath, descriptions: descriptions.ToList())
            )
            .GroupBy(x => x.ArtPath, x => x.descriptions)
            .Select(x => (x.Key, Names: x
                .SelectMany(items => items)
                .Select(item => item.UniqueName?.Text)
                .Where(name => name != null)
                .Distinct()
                .ToList()))
            .Where(x => x.Names.Count != 0)
            .ToDictionary(x => x.Key, x => x.Names);
    }

    public static Dictionary<string, List<string>> LoadUniqueArtMapping(bool ignoreGameMapping)
    {
        Dictionary<string, List<string>> mapping = null;

        if (!ignoreGameMapping &&
            Main.GameController.Files.UniqueItemDescriptions.EntriesList.Count != 0 &&
            Main.GameController.Files.ItemVisualIdentities.EntriesList.Count != 0)
            mapping = GetGameFileUniqueArtMapping();

        var customFilePath = Path.Join(Main.DirectoryFullName, CustomUniqueArtMappingPath);

        if (File.Exists(customFilePath))
            try
            {
                mapping ??= JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(
                    File.ReadAllText(customFilePath)
                );
            }
            catch (Exception ex)
            {
                Main.LogError($"Unable to load custom art mapping: {ex}");
            }

        mapping ??= GetEmbeddedUniqueArtMapping();
        mapping ??= [];
        return mapping;
    }

    private static Dictionary<string, List<string>> GetEmbeddedUniqueArtMapping()
    {
        try
        {
            using var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream(DefaultUniqueArtMappingPath);

            if (stream == null)
            {
                if (Main.Settings.Debug) LogMessage($"Embedded stream {DefaultUniqueArtMappingPath} is missing");
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

    private static void LogError(string message)
    {
        DebugWindow.LogError($"[UniqueArtManager] {message}");
    }

    private static void LogMessage(string message)
    {
        DebugWindow.LogMsg($"[UniqueArtManager] {message}");
    }
}