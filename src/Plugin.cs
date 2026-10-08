using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using InventoryTweaks.Patches;

namespace InventoryTweaks
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "lomserman.obenseuer.inventorytweaks";
        public const string Name = "Inventory Tweaks";
        public const string Version = "0.3.0";

        internal static ManualLogSource Log;

        // Used to run coroutines from static patch code
        internal static Plugin Instance;

        private Harmony _harmony;
        private bool _labelErrorLogged;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void LateUpdate()
        {
            try
            {
                BulkActions.UpdateButtonLabels();
                StackButton.UpdateButtons();
                TakeSimilar.UpdateLabel();
            }
            catch (Exception e)
            {
                // Log once instead of every frame
                if (!_labelErrorLogged)
                {
                    _labelErrorLogged = true;
                    Log.LogError($"Button label update failed: {e}");
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
