using System.IO;
using System.Text.Json;
using Hotd2RemakeTrainer.Protocol;

namespace Hotd2RemakeTrainer.Game;

public interface ITrainerStateStore
{
    TrainerState? Load();

    void Save(TrainerState state);
}

public sealed class JsonTrainerStateStore : ITrainerStateStore
{
    private readonly string _path;

    public JsonTrainerStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "vholf",
            "Hotd2RemakeTrainer",
            "settings.json");
    }

    public TrainerState? Load()
    {
        try
        {
            var state = File.Exists(_path)
                ? JsonSerializer.Deserialize<TrainerState>(File.ReadAllText(_path))
                : null;
            if (state is not null)
            {
                _ = TrainerProtocol.FormatState(state);
            }
            return state;
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or FormatException)
        {
            return null;
        }
    }

    public void Save(TrainerState state)
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Settings path has no directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(_path, JsonSerializer.Serialize(state));
    }
}

internal sealed class NullTrainerStateStore : ITrainerStateStore
{
    public static NullTrainerStateStore Instance { get; } = new();

    public TrainerState? Load() => null;

    public void Save(TrainerState state)
    {
    }
}
