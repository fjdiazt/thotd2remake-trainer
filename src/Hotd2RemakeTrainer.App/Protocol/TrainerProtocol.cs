using System.Globalization;

namespace Hotd2RemakeTrainer.Protocol;

public static class TrainerProtocol
{
    private static readonly HashSet<int> SupportedActions =
    [
        7,
        9,
        11,
        13,
        15,
        16,
        17,
        18,
    ];

    public static string FormatState(TrainerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateRate(state.RapidFireRate);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"STATE {Bit(state.InfiniteHealth)} {Bit(state.InfiniteAmmo)} " +
            $"{Bit(state.InfiniteContinues)} {Bit(state.FireMode == FireMode.Auto)} " +
            $"{Bit(state.Persist)} {Bit(state.FireMode == FireMode.Rapid)} " +
            $"{Bit(state.OneShot)} {Bit(state.EasyBoss)} {Bit(state.ZeroDamage)} " +
            $"{Bit(state.AllWeapons)} {state.RapidFireRate}\n");
    }

    public static TrainerState ParseState(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var line = command.TrimEnd('\r', '\n');
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 12 || !string.Equals(parts[0], "STATE", StringComparison.Ordinal))
        {
            throw new FormatException("Invalid STATE command.");
        }

        var values = new int[11];
        for (var index = 0; index < values.Length; index++)
        {
            if (!int.TryParse(parts[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out values[index]))
            {
                throw new FormatException("STATE contains a non-numeric value.");
            }
        }

        for (var index = 0; index < 10; index++)
        {
            if (values[index] is not (0 or 1))
            {
                throw new FormatException("STATE contains a non-boolean value.");
            }
        }

        if (values[3] != 0 && values[5] != 0)
        {
            throw new FormatException("STATE enables both fire modes.");
        }

        ValidateRate(values[10]);
        var fireMode = values[3] != 0
            ? FireMode.Auto
            : values[5] != 0
                ? FireMode.Rapid
                : FireMode.Off;

        return new TrainerState(
            values[0] != 0,
            values[1] != 0,
            values[2] != 0,
            fireMode,
            values[4] != 0,
            values[6] != 0,
            values[7] != 0,
            values[8] != 0,
            values[9] != 0,
            values[10]);
    }

    public static string FormatAction(int cheatType)
    {
        if (!SupportedActions.Contains(cheatType))
        {
            throw new ArgumentOutOfRangeException(nameof(cheatType));
        }

        return string.Create(CultureInfo.InvariantCulture, $"ACTION {cheatType}\n");
    }

    private static int Bit(bool value) => value ? 1 : 0;

    private static void ValidateRate(int rate)
    {
        if (rate is < TrainerState.MinimumRapidFireRate or > TrainerState.MaximumRapidFireRate)
        {
            throw new FormatException(
                $"Rapid-fire rate must be {TrainerState.MinimumRapidFireRate}-{TrainerState.MaximumRapidFireRate}.");
        }
    }
}
