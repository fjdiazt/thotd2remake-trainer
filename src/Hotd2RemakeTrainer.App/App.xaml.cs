using System.Windows;
using Hotd2RemakeTrainer.Game;
using Hotd2RemakeTrainer.Presentation;
using Hotd2RemakeTrainer.Protocol;
using Vholf.Trainer.UI;

namespace Hotd2RemakeTrainer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        if (eventArgs.Args.Any(argument =>
                string.Equals(argument, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(RunSelfTest() ? 0 : 1);
            return;
        }

        var session = new Hotd2TrainerSession(
            new NamedPipeTrainerConnectionFactory(),
            new JsonTrainerStateStore());
        var panel = new Hotd2TrainerPanel(session);
        var shell = new TrainerShellViewModel(session, panel);
        var window = new TrainerWindow(shell);
        MainWindow = window;
        window.Show();
    }

    private static bool RunSelfTest()
    {
        var state = TrainerState.Default with
        {
            InfiniteHealth = true,
            FireMode = FireMode.Rapid,
            RapidFireRate = 16,
        };
        return TrainerProtocol.ParseState(TrainerProtocol.FormatState(state)) == state &&
            TrainerProtocol.FormatAction(13) == "ACTION 13\n";
    }
}
