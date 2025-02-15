using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExileCore;
using Newtonsoft.Json;

namespace Ground_Items_With_Linq;

public class UniqueArtManager
{
    private readonly bool _debug;
    private readonly string _directoryFullName;
    private readonly GameController _gameController;

    public UniqueArtManager(GameController gameController, string directoryFullName, bool debug)
    {
        _gameController = gameController;
        _directoryFullName = directoryFullName;
        _debug = debug;
    }

    public Dictionary<string, List<string>> GetGameFileUniqueArtMapping()
    {
        if (_gameController.Files.UniqueItemDescriptions.EntriesList.Count == 0)
            _gameController.Files.LoadFiles();

        return _gameController
            .Files.ItemVisualIdentities.EntriesList
            .Where(x => x.ArtPath != null)
            .GroupJoin(
                _gameController.Files.UniqueItemDescriptions.EntriesList
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

    public Dictionary<string, List<string>> LoadUniqueArtMapping(bool ignoreGameMapping)
    {
        Dictionary<string, List<string>> mapping = null;

        if (!ignoreGameMapping &&
            _gameController.Files.UniqueItemDescriptions.EntriesList.Count != 0 &&
            _gameController.Files.ItemVisualIdentities.EntriesList.Count != 0)
            mapping = GetGameFileUniqueArtMapping();

        var customFilePath = Path.Join(_directoryFullName, GroundItemsWithLinq.CustomUniqueArtMappingPath);

        if (File.Exists(customFilePath))
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
                .GetManifestResourceStream(GroundItemsWithLinq.DefaultUniqueArtMappingPath);

            if (stream == null)
            {
                if (_debug) LogMessage($"Embedded stream {GroundItemsWithLinq.DefaultUniqueArtMappingPath} is missing");
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

    private void LogError(string message)
    {
        DebugWindow.LogError($"[UniqueArtManager] {message}");
    }

    private void LogMessage(string message)
    {
        DebugWindow.LogMsg($"[UniqueArtManager] {message}");
    }
}