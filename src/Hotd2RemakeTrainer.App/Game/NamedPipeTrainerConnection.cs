using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Hotd2RemakeTrainer.Game;

public sealed class NamedPipeTrainerConnectionFactory : ITrainerConnectionFactory
{
    private const string GameProcessName = "THE HOUSE OF THE DEAD 2 Remake";
    private const string PipeName = "Hotd2RemakeTrainer";

    public bool IsGameRunning()
    {
        var processes = Process.GetProcessesByName(GameProcessName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public async Task<ITrainerConnection?> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        var pipe = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(150));
            await pipe.ConnectAsync(timeout.Token);
            return new NamedPipeTrainerConnection(pipe);
        }
        catch (Exception exception) when (
            exception is IOException or TimeoutException or OperationCanceledException)
        {
            await pipe.DisposeAsync();
            return null;
        }
    }
}

internal sealed class NamedPipeTrainerConnection : ITrainerConnection
{
    private readonly NamedPipeClientStream _pipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    public NamedPipeTrainerConnection(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
        _reader = new StreamReader(pipe, Encoding.ASCII, false, 1024, true);
        _writer = new StreamWriter(pipe, Encoding.ASCII, 1024, true)
        {
            AutoFlush = true,
            NewLine = "\n",
        };
    }

    public bool IsConnected => _pipe.IsConnected;

    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default) =>
        _reader.ReadLineAsync(cancellationToken).AsTask();

    public async Task WriteAsync(string command, CancellationToken cancellationToken = default)
    {
        await _writer.WriteAsync(command.AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _writer.Dispose();
        _reader.Dispose();
        await _pipe.DisposeAsync();
    }
}
