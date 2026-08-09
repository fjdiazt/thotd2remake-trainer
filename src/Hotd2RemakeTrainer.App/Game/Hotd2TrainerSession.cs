using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Hotd2RemakeTrainer.Protocol;
using Vholf.Trainer.UI;

namespace Hotd2RemakeTrainer.Game;

public sealed class Hotd2TrainerSession : ITrainerSession, INotifyPropertyChanged
{
    private static readonly IReadOnlyList<TrainerOptionViewModel> NoOptions = [];
    private readonly ITrainerConnectionFactory _connectionFactory;
    private readonly ITrainerStateStore _stateStore;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ITrainerConnection? _connection;
    private TrainerState _currentState;
    private TrainerConnectionState _connectionState = TrainerConnectionState.Waiting;
    private string _statusText = "Waiting for Remake";
    private string _actionStatus = "Start the game to enable unlock actions.";
    private bool _localStatePending;
    private bool _disposed;

    public Hotd2TrainerSession(
        ITrainerConnectionFactory connectionFactory,
        ITrainerStateStore? stateStore = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _stateStore = stateStore ?? NullTrainerStateStore.Instance;
        var saved = _stateStore.Load();
        _currentState = saved ?? TrainerState.Default;
        _localStatePending = saved is not null;
    }

    public TrainerIdentity Identity { get; } = new(
        "HOTD 2: REMAKE",
        "THE HOUSE OF THE DEAD 2 TRAINER",
        "BEPINEX 5 • BUILT-IN CHEAT BRIDGE",
        "/Hotd2RemakeTrainer;component/Assets/game.png",
        "BEPINEX BRIDGE • BUILT-IN CHEATS",
        Stretch.UniformToFill);

    public IReadOnlyList<TrainerOptionViewModel> Options => NoOptions;

    public TrainerConnectionState ConnectionState => _connectionState;

    public string StatusText => _statusText;

    public bool RequiresShutdownCleanup => _connection?.IsConnected == true;

    public TrainerState CurrentState => _currentState;

    public bool InfiniteHealth => _currentState.InfiniteHealth;

    public bool InfiniteAmmo => _currentState.InfiniteAmmo;

    public bool InfiniteContinues => _currentState.InfiniteContinues;

    public bool OneShot => _currentState.OneShot;

    public bool EasyBoss => _currentState.EasyBoss;

    public bool ZeroDamage => _currentState.ZeroDamage;

    public bool AllWeapons => _currentState.AllWeapons;

    public bool Persist => _currentState.Persist;

    public bool IsFireOff => _currentState.FireMode == FireMode.Off;

    public bool IsAutoFire => _currentState.FireMode == FireMode.Auto;

    public bool IsRapidFire => _currentState.FireMode == FireMode.Rapid;

    public int RapidFireRate => _currentState.RapidFireRate;

    public bool ActionsEnabled => _connection?.IsConnected == true;

    public string ActionStatus
    {
        get => _actionStatus;
        private set => SetField(ref _actionStatus, value);
    }

    public event EventHandler? StateChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task PollAsync()
    {
        await _gate.WaitAsync();
        try
        {
            ThrowIfDisposed();
            if (!_connectionFactory.IsGameRunning())
            {
                await DisconnectAsync();
                SetConnectionState(TrainerConnectionState.Waiting, "Waiting for Remake");
                ActionStatus = "Start the game to enable unlock actions.";
                return;
            }

            if (_connection is null || !_connection.IsConnected)
            {
                await DisconnectAsync();
                SetConnectionState(TrainerConnectionState.Scanning, "Game found; connecting bridge");
                var connection = await _connectionFactory.TryConnectAsync();
                if (connection is null)
                {
                    SetConnectionState(TrainerConnectionState.Error, "Game found; BepInEx bridge offline");
                    ActionStatus = "BepInEx bridge unavailable.";
                    return;
                }

                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    var line = await connection.ReadLineAsync(timeout.Token);
                    if (line is null)
                    {
                        await connection.DisposeAsync();
                        SetConnectionState(TrainerConnectionState.Error, "Bridge returned no state");
                        return;
                    }

                    var bridgeState = TrainerProtocol.ParseState(line);
                    _connection = connection;
                    if (!_localStatePending)
                    {
                        SetCurrentState(bridgeState, save: true);
                    }
                }
                catch (Exception exception) when (
                    exception is IOException or FormatException or OperationCanceledException)
                {
                    await connection.DisposeAsync();
                    SetConnectionState(TrainerConnectionState.Error, "Bridge state rejected");
                    return;
                }
            }

            await SendCurrentStateAsync();
            _localStatePending = false;
            SetConnectedState();
            ActionStatus = "Ready. Unlock actions apply to the current save.";
        }
        catch (IOException)
        {
            await DisconnectAsync();
            SetConnectionState(TrainerConnectionState.Error, "Game found; reconnecting bridge");
            ActionStatus = "Bridge disconnected. Reconnecting...";
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ToggleAsync(string optionId) => Task.CompletedTask;

    public async Task UpdateStateAsync(Func<TrainerState, TrainerState> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        await _gate.WaitAsync();
        try
        {
            ThrowIfDisposed();
            var next = update(_currentState);
            _ = TrainerProtocol.FormatState(next);
            SetCurrentState(next, save: true);
            if (_connection?.IsConnected == true)
            {
                await SendCurrentStateAsync();
                SetConnectedState();
            }
            else
            {
                _localStatePending = true;
            }
        }
        catch (IOException)
        {
            _localStatePending = true;
            await DisconnectAsync();
            SetConnectionState(TrainerConnectionState.Error, "Bridge disconnected; state queued");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SendActionAsync(int cheatType)
    {
        var command = TrainerProtocol.FormatAction(cheatType);
        await _gate.WaitAsync();
        try
        {
            ThrowIfDisposed();
            if (_connection?.IsConnected != true)
            {
                ActionStatus = "Start the game to enable unlock actions.";
                return;
            }

            await _connection.WriteAsync(command);
            ActionStatus = "Unlock request sent to the game.";
        }
        catch (IOException)
        {
            await DisconnectAsync();
            SetConnectionState(TrainerConnectionState.Error, "Game found; reconnecting bridge");
            ActionStatus = "Bridge disconnected. Reconnect and try again.";
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_connection?.IsConnected == true && !_currentState.Persist)
            {
                var disabled = TrainerState.Default with
                {
                    RapidFireRate = _currentState.RapidFireRate,
                };
                try
                {
                    await _connection.WriteAsync(TrainerProtocol.FormatState(disabled));
                }
                catch (IOException)
                {
                }
            }

            await DisconnectAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task SendCurrentStateAsync()
    {
        if (_connection?.IsConnected != true)
        {
            throw new IOException("Bridge is disconnected.");
        }

        await _connection.WriteAsync(TrainerProtocol.FormatState(_currentState));
    }

    private async Task DisconnectAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
            OnPropertyChanged(nameof(ActionsEnabled));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetCurrentState(TrainerState state, bool save)
    {
        if (_currentState == state)
        {
            return;
        }

        _currentState = state;
        if (save)
        {
            _stateStore.Save(state);
        }
        OnPropertyChanged(string.Empty);
    }

    private void SetConnectedState()
    {
        var enabled = _currentState.InfiniteHealth ||
            _currentState.InfiniteAmmo ||
            _currentState.InfiniteContinues ||
            _currentState.OneShot ||
            _currentState.EasyBoss ||
            _currentState.ZeroDamage ||
            _currentState.AllWeapons ||
            _currentState.FireMode != FireMode.Off;
        SetConnectionState(
            enabled ? TrainerConnectionState.Enabled : TrainerConnectionState.Ready,
            "Connected to Remake");
        OnPropertyChanged(nameof(ActionsEnabled));
    }

    private void SetConnectionState(TrainerConnectionState state, string text)
    {
        if (_connectionState == state && string.Equals(_statusText, text, StringComparison.Ordinal))
        {
            return;
        }

        _connectionState = state;
        _statusText = text;
        StateChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(ActionsEnabled));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
