using UnityEngine;
using UnityEngine.EventSystems;

namespace InventoryTweaks
{
    /// <summary>
    /// Shows the game's text tooltip (ToolTip.instance) while the pointer is over the
    /// object. The provider is asked on every hover, so the text can depend on the
    /// current state; returning false shows nothing.
    /// </summary>
    internal class HintTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public delegate bool Provider(out string title, out string details);

        private Provider _provider;
        private bool _shown;

        public static HintTooltip Attach(GameObject target, Provider provider)
        {
            // Explicit Unity null check (?? bypasses UnityEngine.Object's == operator)
            HintTooltip tooltip = target.GetComponent<HintTooltip>();
            if (tooltip == null)
            {
                tooltip = target.AddComponent<HintTooltip>();
            }
            tooltip._provider = provider;
            return tooltip;
        }

        public static HintTooltip Attach(GameObject target, string title, string details)
        {
            return Attach(target, (out string t, out string d) =>
            {
                t = title;
                d = details;
                return true;
            });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_provider == null || ToolTip.instance == null)
            {
                return;
            }
            if (_provider(out string title, out string details))
            {
                ToolTip.instance.Activate(title, details);
                _shown = true;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void Hide()
        {
            if (_shown && ToolTip.instance != null)
            {
                ToolTip.instance.Deactivate();
            }
            _shown = false;
        }
    }
}
