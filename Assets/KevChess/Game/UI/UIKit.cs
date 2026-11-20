using System;
using System.Collections.Generic;
using KevChess.Core.Ux;
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
        static readonly Dictionary<IconKind, Sprite> icons = new Dictionary<IconKind, Sprite>();

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

        /// <summary>Pictogramme blanc (teinté par Image.color), dessiné une fois depuis Core.Ux.IconShapes.</summary>
        public static Sprite Icon(IconKind kind)
        {
            if (icons.TryGetValue(kind, out var cached) && cached != null) return cached;
            const int size = 96;
            var alpha = IconShapes.Rasterize(kind, size);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, alpha[i]);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            icons[kind] = sprite;
            return sprite;
        }

        /// <summary>Bouton à pictogramme au-dessus d'un libellé court.</summary>
        public static Button IconButton(string name, Transform parent, IconKind icon, string text, Action onClick, Color? accent = null, int fontSize = 26)
        {
            var color = accent ?? Cyan;
            var b = Button(name, parent, text, onClick, color, fontSize);
            var label = b.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.LowerCenter;
            label.fontStyle = FontStyle.Normal;
            Stretch(label.rectTransform, 6, 8, 6, 4);

            var iconRt = Rect("Icon", b.transform);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = Icon(icon);
            img.color = Color.Lerp(Text, color, 0.45f);
            img.raycastTarget = false;
            img.preserveAspect = true;
            Anchor(iconRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(56, 56));
            return b;
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

        /// <summary>Ligne de réglage : libellé à gauche, interrupteur (piste et pastille) à droite. Toucher la ligne bascule.</summary>
        public static Button SwitchRow(string name, Transform parent, string title, Action onClick)
        {
            var b = Row(name, parent, title, onClick);
            var track = Panel("Track", b.transform, new Color(0.08f, 0.10f, 0.20f, 1f), true, TextDim);
            track.raycastTarget = false;
            Anchor(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24, 0), new Vector2(116, 56));
            var knob = Panel("Knob", track.transform, TextDim, false);
            knob.raycastTarget = false;
            Anchor(knob.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28, 0), new Vector2(44, 44));
            return b;
        }

        /// <summary>Met l'interrupteur d'une SwitchRow à oui (pastille à droite, piste allumée) ou non.</summary>
        public static void SetSwitch(Button row, bool on, Color accent)
        {
            var track = row.transform.Find("Track");
            if (track == null) return;
            track.GetComponent<Image>().color = on ? accent * new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(0.08f, 0.10f, 0.20f, 1f);
            var outline = track.Find("Outline");
            if (outline != null) outline.GetComponent<Image>().color = (on ? accent : TextDim) * new Color(1, 1, 1, 0.55f);
            var knob = (RectTransform)track.Find("Knob");
            knob.GetComponent<Image>().color = on ? Text : TextDim;
            knob.anchorMin = knob.anchorMax = new Vector2(on ? 1f : 0f, 0.5f);
            knob.anchoredPosition = new Vector2(on ? -28f : 28f, 0f);
        }

        /// <summary>Ligne de réglage à valeur : libellé à gauche, valeur en couleur d'accent à droite. Toucher change la valeur.</summary>
        public static Button ValueRow(string name, Transform parent, string title, Action onClick)
        {
            var b = Row(name, parent, title, onClick);
            Stretch(b.transform.Find("Label").GetComponent<RectTransform>(), 28, 4, 460, 4);
            var value = Label("Value", b.transform, "", 32, Cyan, TextAnchor.MiddleRight, FontStyle.Bold);
            Anchor(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28, 0), new Vector2(420, 80));
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 18;
            value.resizeTextMaxSize = 32;
            return b;
        }

        public static void SetRowValue(Button row, string value)
        {
            var t = row.transform.Find("Value");
            if (t != null) t.GetComponent<Text>().text = value;
        }

        static Button Row(string name, Transform parent, string title, Action onClick)
        {
            var b = Button(name, parent, title, onClick, TextDim, 34);
            var label = b.transform.Find("Label").GetComponent<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = FontStyle.Normal;
            Stretch(label.rectTransform, 28, 4, 200, 4);
            return b;
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

        /// <summary>
        /// Liste défilante verticale (masquée) ; renvoie le conteneur où ajouter les lignes.
        /// Le parent la dimensionne (LayoutElement flexible ou ancrage).
        /// </summary>
        public static RectTransform ScrollList(string name, Transform parent, float spacing, out ScrollRect scroll)
        {
            var view = Rect(name, parent);
            var bg = view.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.18f); // capte aussi le glisser
            view.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var v = VStack(content, spacing, 8);
            v.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        /// <summary>Barre de progression : fond verre et remplissage néon ; renvoie le remplissage (anchorMax.x = avancement).</summary>
        public static RectTransform ProgressBar(string name, Transform parent, Color accent)
        {
            var back = Panel(name, parent, new Color(0.08f, 0.10f, 0.20f, 0.85f), true, accent);
            var fill = Panel("Fill", back.transform, accent * new Color(1f, 1f, 1f, 0.75f), false);
            fill.raycastTarget = false;
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -4);
            return rt;
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
