using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

[BepInPlugin("local.hotd2remake.trainerbridge", "HotD2 Remake Trainer Bridge", "1.5.0")]
public sealed class Hotd2TrainerBridge : BaseUnityPlugin
{
    private const string PipeName = "Hotd2RemakeTrainer";
    private const int MinRapidFireRate = 2;
    private const int MaxRapidFireRate = 16;
    private const int DefaultRapidFireRate = 8;
    private const BindingFlags StaticField =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private sealed class RapidFireClock
    {
        public long NextTick;
    }

    private static readonly ConditionalWeakTable<object, RapidFireClock>
        RapidFireClocks =
            new ConditionalWeakTable<object, RapidFireClock>();
    private static int autoFireEnabled;
    private static int rapidFireEnabled;
    private static int rapidFireRate = DefaultRapidFireRate;
    private static FieldInfo playerInputField;
    private static PropertyInfo isHoldingFireProperty;
    private static MethodInfo playerFireMethod;

    private readonly object pipeLock = new object();
    private Thread pipeThread;
    private NamedPipeServerStream pipe;
    private Harmony harmony;
    private volatile bool running;
    private int dirty;
    private int savePending;
    private int desiredGodMode;
    private int desiredAmmo;
    private int desiredContinues;
    private int desiredOneShot;
    private int desiredEasyBoss;
    private int desiredZeroDamage;
    private int desiredAllWeapons;
    private int pendingActions;
    private int persistEnabled;
    private int frame;

    private FieldInfo godModeField;
    private FieldInfo ammoField;
    private FieldInfo continuesField;
    private FieldInfo oneShotField;
    private FieldInfo easyBossField;
    private FieldInfo zeroDamageField;
    private FieldInfo allWeaponsField;
    private FieldInfo allCheatsField;
    private Type cheatType;
    private MethodInfo toggleCheatMethod;
    private MethodInfo getCheatStateMethod;
    private PropertyInfo saveReadyProperty;
    private bool originalGodMode;
    private bool originalAmmo;
    private bool originalContinues;
    private bool originalOneShot;
    private bool originalEasyBoss;
    private bool originalZeroDamage;
    private bool originalAllWeapons;
    private bool originalAllCheats;

    private ConfigEntry<bool> persistConfig;
    private ConfigEntry<bool> godModeConfig;
    private ConfigEntry<bool> ammoConfig;
    private ConfigEntry<bool> continuesConfig;
    private ConfigEntry<bool> autoFireConfig;
    private ConfigEntry<bool> rapidFireConfig;
    private ConfigEntry<int> rapidFireRateConfig;
    private ConfigEntry<bool> oneShotConfig;
    private ConfigEntry<bool> easyBossConfig;
    private ConfigEntry<bool> zeroDamageConfig;
    private ConfigEntry<bool> allWeaponsConfig;

    private void Awake()
    {
        Type cheats = FindType("CR_Cheats");
        Type data = FindType("CR_Data");
        cheatType = FindType("CheatType");
        Type saveDataHandler = FindType("MP_SaveDataHandler");
        if (cheats == null || data == null ||
            cheatType == null || saveDataHandler == null)
        {
            Logger.LogError("Trainer bridge: CR_Cheats/CR_Data not found.");
            return;
        }

        godModeField = cheats.GetField("<isCheatGodModeActive>k__BackingField", StaticField);
        ammoField = cheats.GetField("<isCheatInfiniteAmmoActive>k__BackingField", StaticField);
        continuesField = cheats.GetField("<isCheatUnlimitedTokensActive>k__BackingField", StaticField);
        oneShotField = cheats.GetField("<isCheatOneShotModeActive>k__BackingField", StaticField);
        easyBossField = cheats.GetField("<isCheatEasyBossModeActive>k__BackingField", StaticField);
        zeroDamageField = cheats.GetField("<isCheatZeroDamageActive>k__BackingField", StaticField);
        allWeaponsField = cheats.GetField("<isCheatAllWeaponsUnlockedActive>k__BackingField", StaticField);
        allCheatsField = data.GetField("ARE_ALL_CHEATS_ENABLED", StaticField);
        toggleCheatMethod = cheats.GetMethod("ToggleCheat", StaticField);
        getCheatStateMethod = cheats.GetMethod("GetCheatState", StaticField);
        saveReadyProperty = saveDataHandler.GetProperty("IsReady", StaticField);
        if (godModeField == null || ammoField == null ||
            continuesField == null || oneShotField == null ||
            easyBossField == null || zeroDamageField == null ||
            allWeaponsField == null || allCheatsField == null ||
            toggleCheatMethod == null || getCheatStateMethod == null ||
            saveReadyProperty == null)
        {
            Logger.LogError("Trainer bridge: expected cheat fields not found.");
            return;
        }

        originalGodMode = Read(godModeField);
        originalAmmo = Read(ammoField);
        originalContinues = Read(continuesField);
        originalOneShot = Read(oneShotField);
        originalEasyBoss = Read(easyBossField);
        originalZeroDamage = Read(zeroDamageField);
        originalAllWeapons = Read(allWeaponsField);
        originalAllCheats = Read(allCheatsField);
        BindConfig();
        LoadConfig();
        TryPatchFireModes();

        running = true;
        pipeThread = new Thread(PipeLoop);
        pipeThread.IsBackground = true;
        pipeThread.Name = "HotD2 Trainer Pipe";
        pipeThread.Start();
        Logger.LogInfo("Trainer bridge ready on pipe " + PipeName + ".");
    }

    private void BindConfig()
    {
        persistConfig = Config.Bind("Cheats", "Persist", false);
        godModeConfig = Config.Bind("Cheats", "InfiniteHealth", false);
        ammoConfig = Config.Bind("Cheats", "InfiniteAmmo", false);
        continuesConfig = Config.Bind("Cheats", "InfiniteContinues", false);
        autoFireConfig = Config.Bind("Cheats", "Turbo", false);
        rapidFireConfig = Config.Bind("Cheats", "RapidFire", false);
        rapidFireRateConfig = Config.Bind(
            "Cheats",
            "RapidFireRate",
            DefaultRapidFireRate);
        oneShotConfig = Config.Bind("Cheats", "OneShot", false);
        easyBossConfig = Config.Bind("Cheats", "EasyBoss", false);
        zeroDamageConfig = Config.Bind("Cheats", "ZeroDamage", false);
        allWeaponsConfig = Config.Bind("Cheats", "AllWeapons", false);
    }

    private void LoadConfig()
    {
        if (!persistConfig.Value)
            return;

        bool autoFire = autoFireConfig.Value;
        Interlocked.Exchange(ref persistEnabled, 1);
        Interlocked.Exchange(ref desiredGodMode, godModeConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref desiredAmmo, ammoConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref desiredContinues, continuesConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref desiredOneShot, oneShotConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref desiredEasyBoss, easyBossConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref desiredZeroDamage, zeroDamageConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref desiredAllWeapons, allWeaponsConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref autoFireEnabled, autoFire ? 1 : 0);
        Interlocked.Exchange(
            ref rapidFireEnabled,
            !autoFire && rapidFireConfig.Value ? 1 : 0);
        Interlocked.Exchange(
            ref rapidFireRate,
            ClampRate(rapidFireRateConfig.Value));
        Interlocked.Exchange(ref dirty, 1);
    }

    private void SaveConfig(
        bool godMode,
        bool ammo,
        bool continues,
        bool autoFire,
        bool persist,
        bool rapidFire,
        bool oneShot,
        bool easyBoss,
        bool zeroDamage,
        bool allWeapons,
        int rate)
    {
        godModeConfig.Value = godMode;
        ammoConfig.Value = ammo;
        continuesConfig.Value = continues;
        autoFireConfig.Value = autoFire;
        persistConfig.Value = persist;
        rapidFireConfig.Value = rapidFire;
        rapidFireRateConfig.Value = rate;
        oneShotConfig.Value = oneShot;
        easyBossConfig.Value = easyBoss;
        zeroDamageConfig.Value = zeroDamage;
        allWeaponsConfig.Value = allWeapons;
        Config.Save();
    }

    private void TryPatchFireModes()
    {
        harmony = new Harmony("local.hotd2remake.trainerbridge.firemodes");

        Type holder = FindType("CR_WeaponHolder");
        PropertyInfo autoFireProperty = holder == null
            ? null
            : holder.GetProperty("HasAutoFire", AnyInstance);
        MethodInfo autoFireGetter = autoFireProperty == null
            ? null
            : autoFireProperty.GetGetMethod(true);
        MethodInfo autoFirePostfix = typeof(Hotd2TrainerBridge).GetMethod(
            "ForceAutoFire",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (autoFireGetter == null || autoFirePostfix == null)
        {
            Logger.LogError(
                "Trainer bridge: CR_WeaponHolder.HasAutoFire not found; Auto Fire unavailable.");
        }
        else
        {
            harmony.Patch(
                autoFireGetter,
                postfix: new HarmonyMethod(autoFirePostfix));
            Logger.LogInfo("Trainer bridge: Auto Fire patch ready.");
        }

        Type player = FindType("CR_Player");
        MethodInfo handleAutoFire = player == null
            ? null
            : player.GetMethod("handleAutoFire", AnyInstance);
        playerInputField = player == null
            ? null
            : player.GetField("input", AnyInstance);
        playerFireMethod = player == null
            ? null
            : player.GetMethod("Fire", AnyInstance);
        isHoldingFireProperty = playerInputField == null
            ? null
            : playerInputField.FieldType.GetProperty(
                "IsHoldingFire",
                AnyInstance);
        MethodInfo rapidFirePostfix = typeof(Hotd2TrainerBridge).GetMethod(
            "RepeatNormalFire",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (handleAutoFire == null || playerInputField == null ||
            playerFireMethod == null || isHoldingFireProperty == null ||
            rapidFirePostfix == null)
        {
            Logger.LogError(
                "Trainer bridge: CR_Player fire path not found; Rapid Fire unavailable.");
        }
        else
        {
            harmony.Patch(
                handleAutoFire,
                postfix: new HarmonyMethod(rapidFirePostfix));
            Logger.LogInfo("Trainer bridge: Rapid Fire patch ready.");
        }
    }

    private static void ForceAutoFire(ref bool __result)
    {
        if (Interlocked.CompareExchange(ref autoFireEnabled, 0, 0) != 0)
            __result = true;
    }

    private static void RepeatNormalFire(object __instance)
    {
        if (Interlocked.CompareExchange(ref rapidFireEnabled, 0, 0) == 0)
            return;

        RapidFireClock clock =
            RapidFireClocks.GetValue(__instance, _ => new RapidFireClock());
        object input = playerInputField.GetValue(__instance);
        bool holding = input != null &&
            (bool)isHoldingFireProperty.GetValue(input, null);
        if (!holding)
        {
            clock.NextTick = 0;
            return;
        }

        long now = Stopwatch.GetTimestamp();
        if (now < clock.NextTick)
            return;

        int rate = Interlocked.CompareExchange(ref rapidFireRate, 0, 0);
        clock.NextTick =
            now + Math.Max(1L, Stopwatch.Frequency / rate);
        playerFireMethod.Invoke(__instance, null);
    }

    private void Update()
    {
        if (!running)
            return;

        frame++;
        bool changed = Interlocked.Exchange(ref dirty, 0) != 0;
        if (!changed && frame < 60)
            return;

        frame = 0;
        bool godMode = Interlocked.CompareExchange(ref desiredGodMode, 0, 0) != 0;
        bool ammo = Interlocked.CompareExchange(ref desiredAmmo, 0, 0) != 0;
        bool continues = Interlocked.CompareExchange(ref desiredContinues, 0, 0) != 0;
        bool autoFire = Interlocked.CompareExchange(ref autoFireEnabled, 0, 0) != 0;
        bool rapidFire = Interlocked.CompareExchange(ref rapidFireEnabled, 0, 0) != 0;
        int rate = Interlocked.CompareExchange(ref rapidFireRate, 0, 0);
        bool oneShot = Interlocked.CompareExchange(ref desiredOneShot, 0, 0) != 0;
        bool easyBoss = Interlocked.CompareExchange(ref desiredEasyBoss, 0, 0) != 0;
        bool zeroDamage = Interlocked.CompareExchange(ref desiredZeroDamage, 0, 0) != 0;
        bool allWeapons = Interlocked.CompareExchange(ref desiredAllWeapons, 0, 0) != 0;
        bool persist = Interlocked.CompareExchange(ref persistEnabled, 0, 0) != 0;
        int actions = Interlocked.Exchange(ref pendingActions, 0);
        bool any = godMode || ammo || continues || oneShot ||
            easyBoss || zeroDamage || allWeapons || actions != 0;

        allCheatsField.SetValue(null, originalAllCheats || any);
        godModeField.SetValue(null, godMode);
        ammoField.SetValue(null, ammo);
        continuesField.SetValue(null, continues);
        oneShotField.SetValue(null, oneShot);
        easyBossField.SetValue(null, easyBoss);
        zeroDamageField.SetValue(null, zeroDamage);
        allWeaponsField.SetValue(null, allWeapons);

        if (actions != 0)
            ApplyUnlockActions(actions);

        if (changed && Interlocked.Exchange(ref savePending, 0) != 0)
        {
            SaveConfig(
                godMode,
                ammo,
                continues,
                autoFire,
                persist,
                rapidFire,
                oneShot,
                easyBoss,
                zeroDamage,
                allWeapons,
                rate);
        }
    }

    private void OnDestroy()
    {
        running = false;
        Interlocked.Exchange(ref autoFireEnabled, 0);
        Interlocked.Exchange(ref rapidFireEnabled, 0);
        if (harmony != null)
            harmony.UnpatchSelf();

        lock (pipeLock)
        {
            if (pipe != null)
                pipe.Close();
        }

        if (godModeField != null)
        {
            godModeField.SetValue(null, originalGodMode);
            ammoField.SetValue(null, originalAmmo);
            continuesField.SetValue(null, originalContinues);
            oneShotField.SetValue(null, originalOneShot);
            easyBossField.SetValue(null, originalEasyBoss);
            zeroDamageField.SetValue(null, originalZeroDamage);
            allWeaponsField.SetValue(null, originalAllWeapons);
            allCheatsField.SetValue(null, originalAllCheats);
        }
    }

    private void PipeLoop()
    {
        while (running)
        {
            try
            {
                using (NamedPipeServerStream server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None))
                {
                    lock (pipeLock)
                        pipe = server;

                    server.WaitForConnection();
                    WriteState(server);
                    using (StreamReader reader = new StreamReader(server))
                    {
                        while (running && server.IsConnected)
                        {
                            string line = reader.ReadLine();
                            if (line == null)
                                break;
                            Parse(line);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                if (running)
                    Logger.LogWarning("Trainer bridge pipe reset: " + exception.Message);
            }
            finally
            {
                lock (pipeLock)
                    pipe = null;
            }
        }
    }

    private void WriteState(Stream server)
    {
        string state = String.Format(
            "STATE {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}\n",
            Interlocked.CompareExchange(ref desiredGodMode, 0, 0),
            Interlocked.CompareExchange(ref desiredAmmo, 0, 0),
            Interlocked.CompareExchange(ref desiredContinues, 0, 0),
            Interlocked.CompareExchange(ref autoFireEnabled, 0, 0),
            Interlocked.CompareExchange(ref persistEnabled, 0, 0),
            Interlocked.CompareExchange(ref rapidFireEnabled, 0, 0),
            Interlocked.CompareExchange(ref desiredOneShot, 0, 0),
            Interlocked.CompareExchange(ref desiredEasyBoss, 0, 0),
            Interlocked.CompareExchange(ref desiredZeroDamage, 0, 0),
            Interlocked.CompareExchange(ref desiredAllWeapons, 0, 0),
            Interlocked.CompareExchange(ref rapidFireRate, 0, 0));
        byte[] bytes = Encoding.ASCII.GetBytes(state);
        server.Write(bytes, 0, bytes.Length);
        server.Flush();
    }

    private void Parse(string line)
    {
        string[] parts = line.Split(' ');
        if (parts.Length == 2 && parts[0] == "ACTION")
        {
            int action;
            if (Int32.TryParse(parts[1], out action) &&
                IsUnlockAction(action))
            {
                QueueActions(1 << action);
                Interlocked.Exchange(ref dirty, 1);
            }
            return;
        }

        int godMode = 0;
        int ammo = 0;
        int continues = 0;
        int autoFire = 0;
        int persist = 0;
        int rapidFire = 0;
        int oneShot = 0;
        int easyBoss = 0;
        int zeroDamage = 0;
        int allWeapons = 0;
        int rate = DefaultRapidFireRate;
        if ((parts.Length != 4 && parts.Length != 5 &&
             parts.Length != 6 && parts.Length != 8 &&
             parts.Length != 9 && parts.Length != 12) ||
            parts[0] != "STATE" ||
            !TryBit(parts[1], out godMode) ||
            !TryBit(parts[2], out ammo) ||
            !TryBit(parts[3], out continues) ||
            (parts.Length >= 5 && !TryBit(parts[4], out autoFire)) ||
            (parts.Length >= 6 && !TryBit(parts[5], out persist)) ||
            (parts.Length >= 8 && !TryBit(parts[6], out rapidFire)) ||
            (parts.Length >= 8 && !TryBit(parts[7], out oneShot)) ||
            (parts.Length == 9 && !TryRate(parts[8], out rate)) ||
            (parts.Length == 12 && !TryBit(parts[8], out easyBoss)) ||
            (parts.Length == 12 && !TryBit(parts[9], out zeroDamage)) ||
            (parts.Length == 12 && !TryBit(parts[10], out allWeapons)) ||
            (parts.Length == 12 && !TryRate(parts[11], out rate)) ||
            (autoFire != 0 && rapidFire != 0))
            return;

        bool changed =
            Interlocked.Exchange(ref desiredGodMode, godMode) != godMode;
        changed |= Interlocked.Exchange(ref desiredAmmo, ammo) != ammo;
        changed |=
            Interlocked.Exchange(ref desiredContinues, continues) != continues;
        changed |=
            Interlocked.Exchange(ref autoFireEnabled, autoFire) != autoFire;
        changed |=
            Interlocked.Exchange(ref rapidFireEnabled, rapidFire) != rapidFire;
        changed |=
            Interlocked.Exchange(ref desiredOneShot, oneShot) != oneShot;
        changed |=
            Interlocked.Exchange(ref desiredEasyBoss, easyBoss) != easyBoss;
        changed |=
            Interlocked.Exchange(ref desiredZeroDamage, zeroDamage) != zeroDamage;
        changed |=
            Interlocked.Exchange(ref desiredAllWeapons, allWeapons) != allWeapons;
        changed |= Interlocked.Exchange(ref rapidFireRate, rate) != rate;
        changed |= Interlocked.Exchange(ref persistEnabled, persist) != persist;
        if (!changed)
            return;

        Interlocked.Exchange(ref savePending, 1);
        Interlocked.Exchange(ref dirty, 1);
    }

    private static bool IsUnlockAction(int action)
    {
        return action == 7 || action == 9 || action == 11 ||
            action == 13 || action == 15 || action == 16 ||
            action == 17 || action == 18;
    }

    private void QueueActions(int actions)
    {
        int current;
        do
        {
            current = Interlocked.CompareExchange(ref pendingActions, 0, 0);
        }
        while (Interlocked.CompareExchange(
            ref pendingActions,
            current | actions,
            current) != current);
    }

    private void ApplyUnlockActions(int actions)
    {
        if (!(bool)saveReadyProperty.GetValue(null, null))
        {
            QueueActions(actions);
            Interlocked.Exchange(ref dirty, 1);
            return;
        }

        for (int action = 1; action <= 18; action++)
        {
            if ((actions & (1 << action)) == 0)
                continue;

            object value = Enum.ToObject(cheatType, action);
            try
            {
                if (!(bool)getCheatStateMethod.Invoke(
                    null, new object[] { value }))
                {
                    toggleCheatMethod.Invoke(null, new object[] { value });
                }
                Logger.LogInfo("Trainer bridge: unlock action " + action + " applied.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Trainer bridge: unlock action " + action +
                    " failed: " + exception.Message);
            }
        }
    }
    private static bool TryBit(string text, out int value)
    {
        return Int32.TryParse(text, out value) && (value == 0 || value == 1);
    }

    private static bool TryRate(string text, out int value)
    {
        return Int32.TryParse(text, out value) &&
            value >= MinRapidFireRate &&
            value <= MaxRapidFireRate;
    }

    private static int ClampRate(int value)
    {
        return Math.Max(MinRapidFireRate, Math.Min(MaxRapidFireRate, value));
    }

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name, false);
            if (type != null)
                return type;
        }
        return null;
    }

    private static bool Read(FieldInfo field)
    {
        return (bool)field.GetValue(null);
    }
}
