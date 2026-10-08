using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace InventoryTweaks
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "lomserman.obenseuer.inventorytweaks";
        public const string Name = "Inventory Tweaks";
        public const string Version = "0.2.0";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
