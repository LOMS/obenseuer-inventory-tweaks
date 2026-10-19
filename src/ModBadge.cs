using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace InventoryTweaks
{
    /// <summary>
    /// Marks a button whose behavior the mod changes with a small transparent hammer in its top-right corner.
    /// The icon is the embedded resource assets\hammer.png. The badge never takes pointer events
    /// and is ignored by layout groups.
    /// </summary>
    internal class ModBadge : MonoBehaviour
    {
        private const float Margin = 2f;
        private const float Opacity = 0.2f;
        private const string ResourceName = "hammer.png";
        private const string BadgeName = "InventoryTweaks.Badge";

        private static Sprite _sprite;
        private static bool _loadFailed;

        private RectTransform _button;
        private RectTransform _badge;
        private Func<bool> _visible;

        /// <summary>
        /// Adds the badge. With a predicate the badge is shown only while it returns true
        /// (for buttons the game reuses for other actions).
        /// </summary>
        public static ModBadge Attach(GameObject button, Func<bool> visible = null)
        {
            // Explicit Unity null check (?? bypasses UnityEngine.Object's == operator)
            ModBadge badge = button.GetComponent<ModBadge>();
            if (badge == null)
            {
                badge = button.AddComponent<ModBadge>();
            }
            badge._visible = visible;
            badge.EnsureBadge();
            return badge;
        }

        private void EnsureBadge()
        {
            _button = transform as RectTransform;
            Sprite sprite = LoadSprite();
            if (_button == null || sprite == null)
            {
                return;
            }

            // A button cloned from another one already has the child (the clone's fields are empty)
            Transform existing = transform.Find(BadgeName);
            if (existing != null)
            {
                _badge = existing as RectTransform;
            }
            else
            {
                var go = new GameObject(BadgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.GetComponent<LayoutElement>().ignoreLayout = true;

                Image image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, Opacity);

                _badge = (RectTransform)go.transform;
                _badge.anchorMin = _badge.anchorMax = _badge.pivot = new Vector2(1f, 1f);
                _badge.anchoredPosition = new Vector2(-Margin, -Margin);
            }

            Resize();
            UpdateVisibility();
        }

        private void OnEnable()
        {
            Resize();
        }

        // Sent by the RectTransform of the button whenever its size changes (layout, relayout)
        private void OnRectTransformDimensionsChange()
        {
            Resize();
        }

        private void Update()
        {
            if (_visible != null)
            {
                UpdateVisibility();
            }
        }

        private void Resize()
        {
            if (_badge == null || _button == null)
            {
                return;
            }
            float side = Mathf.Max(0f, _button.rect.height / 3f - Margin);
            _badge.sizeDelta = new Vector2(side, side);
        }

        private void UpdateVisibility()
        {
            if (_badge == null)
            {
                return;
            }
            bool visible = true;
            if (_visible != null)
            {
                try
                {
                    visible = _visible();
                }
                catch (Exception)
                {
                    visible = false;
                }
            }
            if (_badge.gameObject.activeSelf != visible)
            {
                _badge.gameObject.SetActive(visible);
            }
        }

        private static Sprite LoadSprite()
        {
            if (_sprite != null || _loadFailed)
            {
                return _sprite;
            }

            using (Stream stream = typeof(ModBadge).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    _loadFailed = true;
                    Plugin.Log.LogWarning($"Badge icon '{ResourceName}' is not embedded in the plugin");
                    return null;
                }

                var bytes = new MemoryStream();
                stream.CopyTo(bytes);

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
                {
                    name = "InventoryTweaks.Hammer",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                if (!ImageConversion.LoadImage(texture, bytes.ToArray()))
                {
                    _loadFailed = true;
                    UnityEngine.Object.Destroy(texture);
                    Plugin.Log.LogWarning($"Badge icon '{ResourceName}' could not be decoded");
                    return null;
                }

                _sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return _sprite;
        }
    }
}
