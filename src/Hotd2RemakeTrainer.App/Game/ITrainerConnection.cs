namespace Hotd2RemakeTrainer.Game;

public interface ITrainerConnection : IAsyncDisposable
{
    bool IsConnected { get; }

    Task<string?> ReadLineAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(string command, CancellationToken cancellationToken = default);
}

public interface ITrainerConnectionFactory
{
    bool IsGameRunning();

    Task<ITrainerConnection?> TryConnectAsync(CancellationToken cancellationToken = default);
}
