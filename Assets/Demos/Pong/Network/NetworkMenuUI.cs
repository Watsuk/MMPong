using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MMPong.Network
{
    /// <summary>
    /// Vrai menu d'accueil (uGUI) construit <b>par code</b> — même philosophie que la couche
    /// réseau (aucun prefab à câbler). S'affiche au lancement avec le jeu figé, puis se masque
    /// dès qu'on choisit « Héberger » ou « Rejoindre », ce qui démarre la partie.
    /// Remplace le lanceur jetable <see cref="DevNetLauncher"/>.
    /// </summary>
    [RequireComponent(typeof(GameBootstrap))]
    public class NetworkMenuUI : MonoBehaviour
    {
        public string defaultIp = "127.0.0.1";

        GameBootstrap bootstrap;
        GameObject root;       // le Canvas entier : masqué une fois le choix fait
        InputField ipField;
        Font font;

        void Awake()
        {
            bootstrap = GetComponent<GameBootstrap>();
            // L'IMGUI/uGUI a besoin d'une police : on prend celle intégrée à Unity.
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        void Start()
        {
            EnsureEventSystem();
            BuildMenu();
            Time.timeScale = 0f;   // le jeu reste figé tant que le joueur n'a pas choisi
        }

        /// <summary>Un EventSystem est obligatoire pour que les clics uGUI fonctionnent.</summary>
        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        void BuildMenu()
        {
            // 1) Canvas plein écran, au-dessus de tout le reste.
            root = new GameObject("NetworkMenuCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            root.AddComponent<GraphicRaycaster>();

            // 2) Panneau central semi-opaque.
            var panel = CreateImage("Panel", root.transform, new Color(0.10f, 0.10f, 0.13f, 0.96f),
                Vector2.zero, new Vector2(560, 420));

            CreateText("Title", panel.transform, "MMPong — Réseau", 38, FontStyle.Bold,
                new Vector2(0, 160), new Vector2(520, 60));
            CreateText("IpLabel", panel.transform, "IP du serveur", 24, FontStyle.Normal,
                new Vector2(0, 80), new Vector2(520, 40));

            ipField = CreateInputField("IpField", panel.transform, defaultIp,
                new Vector2(0, 30), new Vector2(440, 56));

            CreateButton("HostBtn", panel.transform, "Héberger", new Color(0.20f, 0.55f, 0.30f),
                new Vector2(0, -50), new Vector2(440, 64), () => Launch(host: true));
            CreateButton("JoinBtn", panel.transform, "Rejoindre", new Color(0.20f, 0.45f, 0.80f),
                new Vector2(0, -140), new Vector2(440, 64), () => Launch(host: false));
        }

        /// <summary>Lance la partie : relance le temps, démarre le réseau, masque le menu.</summary>
        void Launch(bool host)
        {
            Time.timeScale = 1f;
            if (host) bootstrap.StartHost();
            else bootstrap.StartClient(ipField.text);
            root.SetActive(false);
        }

        // ---- Fabriques uGUI (centrées sur le parent via ancre/pivot au milieu) ----

        Image CreateImage(string name, Transform parent, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            Place(img.rectTransform, pos, size);
            return img;
        }

        Text CreateText(string name, Transform parent, string content, int size, FontStyle style,
                        Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = content; t.font = font; t.fontSize = size; t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            Place(t.rectTransform, pos, sizeDelta);
            return t;
        }

        InputField CreateInputField(string name, Transform parent, string value, Vector2 pos, Vector2 size)
        {
            var bg = CreateImage(name, parent, Color.white, pos, size);
            var field = bg.gameObject.AddComponent<InputField>();
            var text = CreateText("Text", bg.transform, value, 24, FontStyle.Normal,
                Vector2.zero, size - new Vector2(20, 8));
            text.color = Color.black; text.alignment = TextAnchor.MiddleLeft;
            field.textComponent = text;
            field.text = value;
            return field;
        }

        void CreateButton(string name, Transform parent, string label, Color color,
                          Vector2 pos, Vector2 size, UnityAction onClick)
        {
            var img = CreateImage(name, parent, color, pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            CreateText("Label", img.transform, label, 28, FontStyle.Bold, Vector2.zero, size);
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
