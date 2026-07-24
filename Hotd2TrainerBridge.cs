using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;

[BepInPlugin("local.hotd2remake.trainerbridge", "HotD2 Remake Trainer Bridge", "1.2.0")]
public sealed class Hotd2TrainerBridge : BaseUnityPlugin
{
    private const string PipeName = "Hotd2RemakeTrainer";
    private const BindingFlags StaticField =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static int turboEnabled;

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
    private int persistEnabled;
    private int frame;

    private FieldInfo godModeField;
    private FieldInfo ammoField;
    private FieldInfo continuesField;
    private FieldInfo allCheatsField;
    private bool originalGodMode;
    private bool originalAmmo;
    private bool originalContinues;
    private bool originalAllCheats;

    private ConfigEntry<bool> persistConfig;
    private ConfigEntry<bool> godModeConfig;
    private ConfigEntry<bool> ammoConfig;
    private ConfigEntry<bool> continuesConfig;
    private ConfigEntry<bool> turboConfig;

    private void Awake()
    {
        Type cheats = FindType("CR_Cheats");
        Type data = FindType("CR_Data");
        if (cheats == null || data == null)
        {
            Logger.LogError("Trainer bridge: CR_Cheats/CR_Data not found.");
            return;
        }

        godModeField = cheats.GetField("<isCheatGodModeActive>k__BackingField", StaticField);
        ammoField = cheats.GetField("<isCheatInfiniteAmmoActive>k__BackingField", StaticField);
        continuesField = cheats.GetField("<isCheatUnlimitedTokensActive>k__BackingField", StaticField);
        allCheatsField = data.GetField("ARE_ALL_CHEATS_ENABLED", StaticField);
        if (godModeField == null || ammoField == null ||
            continuesField == null || allCheatsField == null)
        {
            Logger.LogError("Trainer bridge: expected cheat fields not found.");
            return;
        }

        originalGodMode = Read(godModeField);
        originalAmmo = Read(ammoField);
        originalContinues = Read(continuesField);
        originalAllCheats = Read(allCheatsField);
        BindConfig();
        LoadConfig();
        TryPatchTurbo();

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
        turboConfig = Config.Bind("Cheats", "Turbo", false);
    }

    private void LoadConfig()
    {
        if (!persistConfig.Value)
            return;

        Interlocked.Exchange(ref persistEnabled, 1);
        Interlocked.Exchange(ref desiredGodMode, godModeConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref desiredAmmo, ammoConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref desiredContinues, continuesConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref turboEnabled, turboConfig.Value ? 1 : 0);
        Interlocked.Exchange(ref dirty, 1);
    }

    private void SaveConfig(
        bool godMode,
        bool ammo,
        bool continues,
        bool turbo,
        bool persist)
    {
        godModeConfig.Value = godMode;
        ammoConfig.Value = ammo;
        continuesConfig.Value = continues;
        turboConfig.Value = turbo;
        persistConfig.Value = persist;
        Config.Save();
    }

    private void TryPatchTurbo()
    {
        Type holder = FindType("CR_WeaponHolder");
        PropertyInfo property = holder == null
            ? null
            : holder.GetProperty("HasAutoFire", AnyInstance);
        MethodInfo getter = property == null ? null : property.GetGetMethod(true);
        MethodInfo postfix = typeof(Hotd2TrainerBridge).GetMethod(
            "ForceAutoFire",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (getter == null || postfix == null)
        {
            Logger.LogError("Trainer bridge: CR_WeaponHolder.HasAutoFire not found; Turbo unavailable.");
            return;
        }

        harmony = new Harmony("local.hotd2remake.trainerbridge.turbo");
        harmony.Patch(getter, postfix: new HarmonyMethod(postfix));
        Logger.LogInfo("Trainer bridge: Turbo fire patch ready.");
    }

    private static void ForceAutoFire(ref bool __result)
    {
        if (Interlocked.CompareExchange(ref turboEnabled, 0, 0) != 0)
            __result = true;
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
        bool turbo = Interlocked.CompareExchange(ref turboEnabled, 0, 0) != 0;
        bool persist = Interlocked.CompareExchange(ref persistEnabled, 0, 0) != 0;
        bool any = godMode || ammo || continues;

        allCheatsField.SetValue(null, originalAllCheats || any);
        godModeField.SetValue(null, godMode);
        ammoField.SetValue(null, ammo);
        continuesField.SetValue(null, continues);

        if (changed && Interlocked.Exchange(ref savePending, 0) != 0)
            SaveConfig(godMode, ammo, continues, turbo, persist);
    }

    private void OnDestroy()
    {
        running = false;
        Interlocked.Exchange(ref turboEnabled, 0);
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
            "STATE {0} {1} {2} {3} {4}\n",
            Interlocked.CompareExchange(ref desiredGodMode, 0, 0),
            Interlocked.CompareExchange(ref desiredAmmo, 0, 0),
            Interlocked.CompareExchange(ref desiredContinues, 0, 0),
            Interlocked.CompareExchange(ref turboEnabled, 0, 0),
            Interlocked.CompareExchange(ref persistEnabled, 0, 0));
        byte[] bytes = Encoding.ASCII.GetBytes(state);
        server.Write(bytes, 0, bytes.Length);
        server.Flush();
    }

    private void Parse(string line)
    {
        string[] parts = line.Split(' ');
        int godMode;
        int ammo;
        int continues;
        int turbo = 0;
        int persist = 0;
        if ((parts.Length != 4 && parts.Length != 5 && parts.Length != 6) ||
            parts[0] != "STATE" ||
            !TryBit(parts[1], out godMode) ||
            !TryBit(parts[2], out ammo) ||
            !TryBit(parts[3], out continues) ||
            (parts.Length >= 5 && !TryBit(parts[4], out turbo)) ||
            (parts.Length == 6 && !TryBit(parts[5], out persist)))
            return;

        bool changed =
            Interlocked.Exchange(ref desiredGodMode, godMode) != godMode;
        changed |= Interlocked.Exchange(ref desiredAmmo, ammo) != ammo;
        changed |=
            Interlocked.Exchange(ref desiredContinues, continues) != continues;
        changed |= Interlocked.Exchange(ref turboEnabled, turbo) != turbo;
        changed |= Interlocked.Exchange(ref persistEnabled, persist) != persist;
        if (!changed)
            return;

        Interlocked.Exchange(ref savePending, 1);
        Interlocked.Exchange(ref dirty, 1);
    }

    private static bool TryBit(string text, out int value)
    {
        return Int32.TryParse(text, out value) && (value == 0 || value == 1);
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
