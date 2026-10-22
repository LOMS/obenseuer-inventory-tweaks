using UnityEngine;

namespace InventoryTweaks
{
    /// <summary>
    /// Modifier keys used by the mod's shortcuts, in one place so every feature agrees.
    /// Shift is the left one only (the game's own Shift handling and the "Walk" binding
    /// use it); Ctrl and Alt accept either side.
    /// </summary>
    internal static class Keys
    {
        public static bool Shift => Input.GetKey(KeyCode.LeftShift);

        public static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        public static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    }
}
