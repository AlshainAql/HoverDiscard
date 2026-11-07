using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoverDiscard.Core;

public enum UiLanguage { English, Japanese }
public enum SelectionKey { Shift, Ctrl, Alt }

public sealed class CharacterSettings
{
    public bool RememberNewItems { get; set; } = true;
    public HashSet<ItemKey> RememberedItems { get; set; } = [];
    public HashSet<uint> WhitelistedItems { get; set; } = [];
}

public sealed class Configuration
{
    public int Version { get; set; } = 2;
    public UiLanguage Language { get; set; }
    public SelectionKey SelectionKey { get; set; } = SelectionKey.Shift;
    public Dictionary<ulong, CharacterSettings> Characters { get; set; } = [];

    public CharacterSettings ForCharacter(ulong contentId)
    {
        if (contentId == 0)
            throw new ArgumentOutOfRangeException(nameof(contentId));
        if (!Characters.TryGetValue(contentId, out var settings))
            Characters.Add(contentId, settings = new CharacterSettings());
        return settings;
    }
}

public static class ConfigurationFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Configuration Load(string path, UiLanguage initialLanguage)
    {
        if (!File.Exists(path))
            return new Configuration { Language = initialLanguage };
        var result = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The configuration is empty.");
        if (result.Version is not (1 or 2) || !Enum.IsDefined(result.Language) || !Enum.IsDefined(result.SelectionKey)
            || result.Characters == null
            || result.Characters.Any(pair => pair.Key == 0 || pair.Value == null
                || pair.Value.RememberedItems == null
                || pair.Value.WhitelistedItems == null || pair.Value.WhitelistedItems.Contains(0)
                || pair.Value.RememberedItems.Any(k => k.ItemId == 0 || !Enum.IsDefined(k.Quality))))
            throw new InvalidDataException("The configuration contains unsupported values.");
        result.Version = 2;
        return result;
    }

    public static void Save(string path, Configuration configuration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write(JsonSerializer.Serialize(configuration, Options));
            writer.Write('\n');
            writer.Flush();
            stream.Flush(true);
        }
        File.Move(temporary, path, true);
    }
}
