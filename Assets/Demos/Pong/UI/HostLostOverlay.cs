using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MMPong.UI
{
    /// <summary>
    /// Overlay plein écran « hôte déconnecté », affiché côté client quand le serveur (host) ne
    /// répond plus. Après un court délai (ou clic), recharge la scène active → retour au menu
    /// d'accueil avec un état repartant de zéro (le « refresh »). Auto-construit par code sur son
    /// propre Canvas (au-dessus du HUD), sans câblage inspecteur.
    /// </summary>
    public class HostLostOverlay : MonoBehaviour
    {
        public float delayBeforeReload = 3f;
        float timer;
        bool reloading;

        // Évite d'empiler plusieurs overlays si l'événement de perte se déclenche en rafale.
        static bool active;

        /// <summary>Affiche l'overlay une seule fois (idempotent).</summary>
        public static void Show()
        {
            if (active) return;
            active = true;
            var go = new GameObject("HostLostOverlay");
            go.AddComponent<HostLostOverlay>().Build();
        }

        void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // au-dessus du HUD de jeu et du hub
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var root = UIFactory.CreateScreenRoot(canvasGo.transform, "Root");
            root.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.96f);

            var col = UIFactory.CreateColumn(root.transform);
            UIFactory.CreateTitle(col, "Hôte déconnecté");
            UIFactory.CreateLabel(col, "Message",
                "L'hôte s'est déconnecté, la partie est terminée.\nRetour au menu d'accueil…",
                26f, new Vector2(840f, 96f));
            UIFactory.CreateButton(col, "MenuButton", "Retour au menu", ReloadToMenu);
        }

        void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= delayBeforeReload)
                ReloadToMenu();
        }

        void ReloadToMenu()
        {
            if (reloading) return;
            reloading = true;
            active = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
