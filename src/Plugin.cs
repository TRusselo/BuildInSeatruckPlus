using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SeatruckJukebox
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.snmodding.nautilus")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.tristyn.seatruckjukebox";
        public const string NAME = "Seatruck Jukebox";
        public const string VERSION = "1.0.0";

        public static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            new Harmony(GUID).PatchAll();
            Log.LogInfo($"{NAME} {VERSION} loaded.");
        }
    }
}
