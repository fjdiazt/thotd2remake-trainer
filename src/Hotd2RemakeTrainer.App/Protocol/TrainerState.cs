namespace Hotd2RemakeTrainer.Protocol;

public enum FireMode
{
    Off,
    Auto,
    Rapid,
}

public sealed record TrainerState(
    bool InfiniteHealth,
    bool InfiniteAmmo,
    bool InfiniteContinues,
    FireMode FireMode,
    bool Persist,
    bool OneShot,
    bool EasyBoss,
    bool ZeroDamage,
    bool AllWeapons,
    int RapidFireRate)
{
    public const int MinimumRapidFireRate = 2;
    public const int MaximumRapidFireRate = 16;
    public const int DefaultRapidFireRate = 8;

    public static TrainerState Default { get; } = new(
        false,
        false,
        false,
        FireMode.Off,
        false,
        false,
        false,
        false,
        false,
        DefaultRapidFireRate);
}
