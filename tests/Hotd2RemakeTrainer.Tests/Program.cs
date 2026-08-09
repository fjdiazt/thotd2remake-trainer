using Hotd2RemakeTrainer.Protocol;
using Hotd2RemakeTrainer.Game;
using System.Text.Json;

var failures = new List<string>();

void Test(string name, Action action)
{
    try
    {
        action();
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.Message}");
    }
}

void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

var enabledState = new TrainerState(
    InfiniteHealth: true,
    InfiniteAmmo: false,
    InfiniteContinues: true,
    FireMode: FireMode.Auto,
    Persist: true,
    OneShot: true,
    EasyBoss: false,
    ZeroDamage: true,
    AllWeapons: false,
    RapidFireRate: 12);

Test("state format preserves bridge wire order", () =>
{
    Equal("STATE 1 0 1 1 1 0 1 0 1 0 12\n", TrainerProtocol.FormatState(enabledState));
});

Test("state round trip preserves values", () =>
{
    Equal(enabledState, TrainerProtocol.ParseState(TrainerProtocol.FormatState(enabledState)));
});

Test("state rejects simultaneous fire modes", () =>
{
    Throws<FormatException>(() =>
        TrainerProtocol.ParseState("STATE 0 0 0 1 0 1 0 0 0 0 8\n"));
});

Test("state rejects non boolean values", () =>
{
    Throws<FormatException>(() =>
        TrainerProtocol.ParseState("STATE 2 0 0 0 0 0 0 0 0 0 8\n"));
});

Test("state rejects rapid fire rate outside 2 through 16", () =>
{
    Throws<FormatException>(() =>
        TrainerProtocol.ParseState("STATE 0 0 0 0 0 0 0 0 0 0 17\n"));
});

Test("state rejects trailing data", () =>
{
    Throws<FormatException>(() =>
        TrainerProtocol.ParseState("STATE 0 0 0 0 0 0 0 0 0 0 8 junk\n"));
});

Test("actions preserve supported cheat ids", () =>
{
    Equal("ACTION 13\n", TrainerProtocol.FormatAction(13));
    Equal("ACTION 18\n", TrainerProtocol.FormatAction(18));
    Throws<ArgumentOutOfRangeException>(() => TrainerProtocol.FormatAction(8));
});

Test("first connection accepts bridge state when local state is untouched", () =>
{
    var remote = enabledState with { FireMode = FireMode.Rapid };
    var connection = new FakeTrainerConnection(TrainerProtocol.FormatState(remote));
    var session = new Hotd2TrainerSession(new FakeConnectionFactory(connection));

    session.PollAsync().GetAwaiter().GetResult();

    Equal(remote, session.CurrentState);
});

Test("local state wins when changed before first connection", () =>
{
    var remote = TrainerState.Default with { InfiniteAmmo = true };
    var local = TrainerState.Default with { InfiniteHealth = true };
    var connection = new FakeTrainerConnection(TrainerProtocol.FormatState(remote));
    var session = new Hotd2TrainerSession(new FakeConnectionFactory(connection));

    session.UpdateStateAsync(_ => local).GetAwaiter().GetResult();
    session.PollAsync().GetAwaiter().GetResult();

    Equal(local, session.CurrentState);
    Equal(TrainerProtocol.FormatState(local), connection.Writes[^1]);
});

Test("shutdown clears non persistent cheats", () =>
{
    var remote = enabledState with { Persist = false };
    var connection = new FakeTrainerConnection(TrainerProtocol.FormatState(remote));
    var session = new Hotd2TrainerSession(new FakeConnectionFactory(connection));
    session.PollAsync().GetAwaiter().GetResult();

    session.DisposeAsync().AsTask().GetAwaiter().GetResult();

    var disabled = TrainerState.Default with { RapidFireRate = remote.RapidFireRate };
    Equal(TrainerProtocol.FormatState(disabled), connection.Writes[^1]);
});

Test("settings store rejects protocol-invalid state", () =>
{
    var path = Path.Combine(Path.GetTempPath(), $"hotd2-trainer-{Guid.NewGuid():N}.json");
    try
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(TrainerState.Default with { RapidFireRate = 99 }));

        Equal<TrainerState?>(null, new JsonTrainerStateStore(path).Load());
    }
    finally
    {
        File.Delete(path);
    }
});

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"FAIL {failure}");
    }

    return 1;
}

Console.WriteLine("PASS HotD2 trainer tests");
return 0;

internal sealed class FakeConnectionFactory(FakeTrainerConnection connection) : ITrainerConnectionFactory
{
    public bool IsGameRunning() => true;

    public Task<ITrainerConnection?> TryConnectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ITrainerConnection?>(connection);
}

internal sealed class FakeTrainerConnection(string initialState) : ITrainerConnection
{
    private bool _read;

    public bool IsConnected { get; private set; } = true;

    public List<string> Writes { get; } = [];

    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var value = _read ? null : initialState.TrimEnd('\r', '\n');
        _read = true;
        return Task.FromResult(value);
    }

    public Task WriteAsync(string command, CancellationToken cancellationToken = default)
    {
        Writes.Add(command);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
