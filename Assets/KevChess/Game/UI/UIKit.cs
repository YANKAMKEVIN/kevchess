using System;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>Palette et fabriques d'éléments uGUI au style « verre sombre + liseré néon ».</summary>
    public static class UIKit
    {
        public static readonly Color Cyan = new Color(0.30f, 0.92f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.30f, 0.85f);
        public static readonly Color Glass = new Color(0.05f, 0.06f, 0.12f, 0.78f);
        public static readonly Color GlassStrong = new Color(0.04f, 0.04f, 0.09f, 0.92f);
        public static readonly Color Text = new Color(0.90f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.68f, 0.82f);
        public static readonly Color Danger = new Color(1f, 0.32f, 0.38f);

        static Font font;
        static Sprite rounded, roundedOutline, glow;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>Rectangle arrondi blanc (9-slice), teinté par Image.color.</summary>
        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 22, false));
        public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : (roundedOutline = MakeRounded(64, 22, true));
        public static Sprite Glow => glow != null ? glow : (glow = MakeGlow(64));

        static Sprite MakeRounded(int size, int radius, bool outlineOnly)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius; // < 0 à l'intérieur
                    float a = Mathf.Clamp01(0.5f - d);
                    if (outlineOnly) a *= Mathf.Clamp01(d + 3.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        static Sprite MakeGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, bool outline = true, Color? outlineColor = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            if (outline)
            {
                var ol = Rect("Outline", rt);
                Stretch(ol);
                var oi = ol.gameObject.AddComponent<Image>();
                oi.sprite = RoundedOutline;
                oi.type = Image.Type.Sliced;
                oi.color = (outlineColor ?? Cyan) * new Color(1, 1, 1, 0.55f);
                oi.raycastTarget = false;
                // Le liseré ne doit pas compter comme un élément des layout groups du panneau.
                ol.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, Color? accent = null, int fontSize = 40)
        {
            var color = accent ?? Cyan;
            var img = Panel(name, parent, new Color(0.08f, 0.10f, 0.20f, 0.85f), true, color);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.3f);
            colors.pressedColor = new Color(0.7f, 0.85f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
            var label = Label("Label", img.transform, text, fontSize, Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 12, 4, 12, 4);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = fontSize;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int padding = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, int padding = 0)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement Size(Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleWidth = -1)
        {
            // Pas de « ?? » sur un objet Unity : GetComponent peut renvoyer un faux null dans l'éditeur.
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }
    }
}
