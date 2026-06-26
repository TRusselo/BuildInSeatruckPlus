using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Nautilus.Handlers;
using BuildInSeatruckPlus.Buildables;

namespace BuildInSeatruckPlus
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.snmodding.nautilus")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.tristyn.buildinseatruckplus";
        public const string NAME = "Build In Seatruck Plus";
        public const string VERSION = "1.0.7";

        public static ManualLogSource Log { get; private set; }
        public static new Config Config { get; private set; }

        private void Awake()
        {
            Log = Logger;
            Config = OptionsPanelHandler.RegisterModOptions<Config>();
            MiniBuildables.Register();
            new Harmony(GUID).PatchAll();
            Log.LogInfo($"{NAME} {VERSION} loaded.");
            gameObject.AddComponent<HotkeyController>();
        }
    }
}
