using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Hotd2RemakeTrainer.Game;
using Hotd2RemakeTrainer.Protocol;

namespace Hotd2RemakeTrainer.Presentation;

public partial class Hotd2TrainerPanel : UserControl
{
    private readonly Hotd2TrainerSession _session;
    private bool _loaded;

    public Hotd2TrainerPanel(Hotd2TrainerSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        DataContext = session;
        InitializeComponent();
    }

    private void Panel_Loaded(object sender, RoutedEventArgs eventArgs)
    {
        _loaded = true;
    }

    private async void Gameplay_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not ToggleButton { Tag: string id } toggle)
        {
            return;
        }

        var enabled = toggle.IsChecked == true;
        await _session.UpdateStateAsync(state => id switch
        {
            "health" => state with { InfiniteHealth = enabled },
            "ammo" => state with { InfiniteAmmo = enabled },
            "continues" => state with { InfiniteContinues = enabled },
            "one-shot" => state with { OneShot = enabled },
            "easy-boss" => state with { EasyBoss = enabled },
            "zero-damage" => state with { ZeroDamage = enabled },
            "all-weapons" => state with { AllWeapons = enabled },
            "persist" => state with { Persist = enabled },
            _ => state,
        });
    }

    private async void FireMode_Checked(object sender, RoutedEventArgs eventArgs)
    {
        if (!_loaded || sender is not RadioButton { Tag: string mode })
        {
            return;
        }

        var fireMode = mode switch
        {
            "auto" => FireMode.Auto,
            "rapid" => FireMode.Rapid,
            _ => FireMode.Off,
        };
        await _session.UpdateStateAsync(state => state with { FireMode = fireMode });
    }

    private async void RateDown_Click(object sender, RoutedEventArgs eventArgs)
    {
        await ChangeRateAsync(-1);
    }

    private async void RateUp_Click(object sender, RoutedEventArgs eventArgs)
    {
        await ChangeRateAsync(1);
    }

    private Task ChangeRateAsync(int delta) => _session.UpdateStateAsync(state => state with
    {
        RapidFireRate = Math.Clamp(
            state.RapidFireRate + delta,
            TrainerState.MinimumRapidFireRate,
            TrainerState.MaximumRapidFireRate),
    });

    private async void Action_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string text } || !int.TryParse(text, out var action))
        {
            return;
        }

        if (action == 16 && MessageBox.Show(
                "This may permanently unlock platform achievements. Continue?",
                "Unlock all achievements",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        await _session.SendActionAsync(action);
    }
}
