using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MMPong.UI
{
    /// <summary>
    /// Point d'entrée du hub : déposer ce composant sur un GameObject vide dans la scène suffit.
    /// Crée au runtime le Canvas plein écran, l'EventSystem (si absent), le service de lobby (mock),
    /// le <see cref="GameLauncher"/>, et un GameObject par écran, puis configure le
    /// <see cref="ScreenManager"/> pour démarrer sur <see cref="ScreenId.ModeSelect"/>.
    ///
    /// Aucun câblage manuel d'inspecteur n'est requis. Pour brancher le vrai réseau plus tard,
    /// il suffira de remplacer l'instance de <see cref="FakeLobbyService"/> ici.
    /// </summary>
    public class HubBootstrap : MonoBehaviour
    {
        [Tooltip("Ordre de tri du Canvas du hub (au-dessus du jeu).")]
        [SerializeField] private int sortingOrder = 100;

        [Tooltip("true = mock (FakeLobbyService) pour tester l'UI sans réseau ; false = vrai réseau.")]
        [SerializeField] private bool useMock = false;

        [Tooltip("Police des menus (Space Invaders). Vide → chargée depuis Resources/space_invaders SDF.")]
        [SerializeField] private TMP_FontAsset menuFont;

        void Start()
        {
            // Police des menus : référence d'inspecteur sinon repli sur l'asset dans Resources.
            UIFactory.MenuFont = menuFont != null
                ? menuFont
                : Resources.Load<TMP_FontAsset>("space_invaders SDF");

            EnsureEventSystem();

            // ── Canvas plein écran ───────────────────────────────────────────────
            var canvasGo = new GameObject("HubCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // ── Services + manager + launcher ────────────────────────────────────
            // Le service de lobby vit sur le Canvas ; le launcher vit sur CE GameObject (qui reste
            // actif quand le hub est masqué) pour pouvoir lancer la partie et écouter GameStarted.
            var manager = canvasGo.AddComponent<ScreenManager>();
            var launcher = gameObject.AddComponent<GameLauncher>();

            ILobbyService lobby;
            if (useMock)
            {
                lobby = canvasGo.AddComponent<FakeLobbyService>();
            }
            else
            {
                var net = canvasGo.AddComponent<NetworkLobbyService>();
                net.Init(manager.Session);
                lobby = net;
            }

            // ── Écrans (un GameObject plein écran par écran) ─────────────────────
            var screens = new List<UIScreen>
            {
                CreateScreen<ModeSelectScreen>(canvasGo.transform, "ModeSelectScreen"),
                CreateScreen<OnlineMenuScreen>(canvasGo.transform, "OnlineMenuScreen"),
                CreateScreen<CreateConfigScreen>(canvasGo.transform, "CreateConfigScreen"),
                CreateScreen<HostLobbyScreen>(canvasGo.transform, "HostLobbyScreen"),
                CreateScreen<JoinIpScreen>(canvasGo.transform, "JoinIpScreen"),
                CreateScreen<JoinSetupScreen>(canvasGo.transform, "JoinSetupScreen"),
                CreateScreen<LocalScreen>(canvasGo.transform, "LocalScreen"),
            };

            launcher.Init(manager, manager.Session, lobby);
            manager.Configure(screens.ToArray(), lobby, ScreenId.ModeSelect, canvasGo);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static UIScreen CreateScreen<T>(Transform parent, string name) where T : UIScreen
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return go.AddComponent<T>();
        }
    }
}
