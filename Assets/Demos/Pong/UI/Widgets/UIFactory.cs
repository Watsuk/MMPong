using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MMPong.UI
{
    /// <summary>
    /// Fabrique d'éléments uGUI par code (style Unity par défaut). Factorise les patterns
    /// déjà présents dans <c>PongWinUI</c> (RectTransform ancré, TMP, ColorBlock) pour que
    /// tous les écrans du hub se construisent sans câblage manuel dans l'inspecteur.
    ///
    /// EPIC 2 thèmera ces helpers de façon centralisée (couleurs/polices via UITheme) ;
    /// pour l'instant les valeurs sont codées en dur (fonctionnel d'abord).
    /// </summary>
    public static class UIFactory
    {
        // Palette « par défaut » provisoire (sera remplacée par UITheme en EPIC 2).
        static readonly Color ScreenBg = new Color(0.10f, 0.11f, 0.14f, 1f);
        static readonly Color ButtonBg = new Color(0.20f, 0.55f, 0.95f, 1f);
        static readonly Color FieldBg = new Color(1f, 1f, 1f, 0.92f);
        static readonly Color TextColor = Color.white;

        /// <summary>Crée un panneau racine plein écran (fond opaque) sous <paramref name="parent"/>.</summary>
        public static GameObject CreateScreenRoot(Transform parent, string name)
        {
            var go = NewUI(name, parent);
            var rect = go.GetComponent<RectTransform>();
            Stretch(rect);
            var bg = go.AddComponent<Image>();
            bg.color = ScreenBg;
            return go;
        }

        /// <summary>
        /// Crée une colonne centrée (VerticalLayoutGroup + ContentSizeFitter) qui empile
        /// automatiquement titres, boutons et champs.
        /// </summary>
        public static RectTransform CreateColumn(Transform parent, string name = "Column", float spacing = 16f)
        {
            var go = NewUI(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        /// <summary>Crée une rangée horizontale (HorizontalLayoutGroup) pour aligner des éléments.</summary>
        public static RectTransform CreateRow(Transform parent, string name = "Row", float spacing = 12f)
        {
            var go = NewUI(name, parent);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>Titre de l'écran.</summary>
        public static TextMeshProUGUI CreateTitle(Transform parent, string text)
        {
            return CreateLabel(parent, "Title", text, 38f, new Vector2(760f, 64f));
        }

        /// <summary>Texte simple (label, message d'erreur, info).</summary>
        public static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            float fontSize = 28f, Vector2 size = default)
        {
            if (size == Vector2.zero) size = new Vector2(560f, 44f);

            var go = NewUI(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextColor;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14f;
            tmp.fontSizeMax = fontSize;

            AddLayoutElement(go, size);
            return tmp;
        }

        /// <summary>Bouton plein avec libellé TMP, câblé sur <paramref name="onClick"/>.</summary>
        public static Button CreateButton(Transform parent, string name, string label,
            Action onClick, Vector2 size = default)
        {
            if (size == Vector2.zero) size = new Vector2(280f, 56f);

            var go = NewUI(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = ButtonBg;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = ButtonBg;
            colors.highlightedColor = new Color(0.30f, 0.65f, 1f, 1f);
            colors.pressedColor = new Color(0.12f, 0.40f, 0.78f, 1f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.40f, 1f);
            btn.colors = colors;

            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            var textGo = NewUI("Text", go.transform);
            Stretch(textGo.GetComponent<RectTransform>());
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextColor;
            tmp.raycastTarget = false;

            AddLayoutElement(go, size);
            return btn;
        }

        /// <summary>Champ de saisie TMP avec placeholder.</summary>
        public static TMP_InputField CreateInputField(Transform parent, string name, string placeholder,
            Vector2 size = default)
        {
            if (size == Vector2.zero) size = new Vector2(480f, 48f);

            var go = NewUI(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var bg = go.AddComponent<Image>();
            bg.color = FieldBg;

            var input = go.AddComponent<TMP_InputField>();

            // Zone de texte (viewport masqué).
            var areaGo = NewUI("Text Area", go.transform);
            var areaRect = areaGo.GetComponent<RectTransform>();
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(12f, 6f);
            areaRect.offsetMax = new Vector2(-12f, -6f);
            areaGo.AddComponent<RectMask2D>();

            var placeholderTmp = NewUI("Placeholder", areaGo.transform).AddComponent<TextMeshProUGUI>();
            Stretch(placeholderTmp.GetComponent<RectTransform>());
            placeholderTmp.text = placeholder;
            placeholderTmp.fontSize = 20f;
            placeholderTmp.fontStyle = FontStyles.Italic;
            placeholderTmp.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            placeholderTmp.alignment = TextAlignmentOptions.Left;

            var textTmp = NewUI("Text", areaGo.transform).AddComponent<TextMeshProUGUI>();
            Stretch(textTmp.GetComponent<RectTransform>());
            textTmp.fontSize = 20f;
            textTmp.color = Color.black;
            textTmp.alignment = TextAlignmentOptions.Left;

            input.textViewport = areaRect;
            input.textComponent = textTmp;
            input.placeholder = placeholderTmp;
            input.text = "";

            AddLayoutElement(go, size);
            return input;
        }

        /// <summary>
        /// Sélecteur numérique « [label] [−] valeur [+] » borné à [<paramref name="min"/>, <paramref name="max"/>].
        /// Appelle <paramref name="onChanged"/> à chaque modification.
        /// </summary>
        public static RectTransform CreateStepper(Transform parent, string label, int initial, int min, int max,
            Action<int> onChanged)
        {
            int value = Mathf.Clamp(initial, min, max);

            var row = CreateRow(parent, label + "Stepper");
            CreateLabel(row, "Label", label, 24f, new Vector2(220f, 44f));
            var minus = CreateButton(row, "Minus", "−", null, new Vector2(48f, 44f));
            var valueLabel = CreateLabel(row, "Value", value.ToString(), 26f, new Vector2(60f, 44f));
            var plus = CreateButton(row, "Plus", "+", null, new Vector2(48f, 44f));

            void Apply(int delta)
            {
                value = Mathf.Clamp(value + delta, min, max);
                valueLabel.text = value.ToString();
                onChanged?.Invoke(value);
            }

            minus.onClick.AddListener(() => Apply(-1));
            plus.onClick.AddListener(() => Apply(+1));
            onChanged?.Invoke(value);
            return row;
        }

        /// <summary>
        /// Sélecteur de couleur : une rangée de pastilles cliquables (couleurs de
        /// <see cref="PongPaddle.Palette"/>). Le nom de la couleur choisie est affiché à côté du
        /// libellé. Appelle <paramref name="onChanged"/> avec l'index sélectionné.
        /// </summary>
        public static void CreateColorSelector(Transform parent, string label, int initial, Action<int> onChanged)
        {
            int selected = Mathf.Clamp(initial, 0, PongPaddle.Palette.Length - 1);

            var header = CreateRow(parent, label + "Header");
            CreateLabel(header, "Label", label, 22f, new Vector2(150f, 40f));
            var valueLabel = CreateLabel(header, "Value", PongPaddle.PaletteNames[selected], 20f,
                new Vector2(150f, 40f));

            var row = CreateRow(parent, label + "Swatches", 8f);
            for (int i = 0; i < PongPaddle.Palette.Length; i++)
            {
                int idx = i;
                var btn = CreateButton(row, "Color" + i, "", () =>
                {
                    selected = idx;
                    valueLabel.text = PongPaddle.PaletteNames[idx];
                    onChanged?.Invoke(idx);
                }, new Vector2(40f, 40f));

                // Teinte le bouton à la couleur de la palette (normal + survol).
                var img = btn.GetComponent<Image>();
                img.color = PongPaddle.Palette[idx];
                var colors = btn.colors;
                colors.normalColor = PongPaddle.Palette[idx];
                colors.highlightedColor = Color.Lerp(PongPaddle.Palette[idx], Color.white, 0.3f);
                colors.pressedColor = Color.Lerp(PongPaddle.Palette[idx], Color.black, 0.2f);
                btn.colors = colors;
            }

            onChanged?.Invoke(selected);
        }

        // ── Helpers internes ─────────────────────────────────────────────────────

        static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (parent != null)
                go.layer = parent.gameObject.layer;
            return go;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void AddLayoutElement(GameObject go, Vector2 size)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
        }
    }
}
