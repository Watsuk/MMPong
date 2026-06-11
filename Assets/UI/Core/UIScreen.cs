using UnityEngine;
using UnityEngine.UI;

namespace MMPong.UI
{
    /// <summary>
    /// Classe de base de tout écran plein écran du hub. Fournit :
    /// - l'identité de l'écran (<see cref="Id"/>),
    /// - l'accès au <see cref="ScreenManager"/> et à la <see cref="UISession"/> partagée,
    /// - un bouton retour câblé automatiquement sur <see cref="ScreenManager.Back"/>
    ///   (mécanisme commun, non réimplémenté écran par écran),
    /// - des hooks <see cref="OnEnter"/> / <see cref="OnExit"/>.
    ///
    /// Un écran ne référence JAMAIS un autre écran : toute navigation passe par
    /// <c>Manager.Show(ScreenId)</c> / <c>Manager.Back()</c>.
    /// </summary>
    public abstract class UIScreen : MonoBehaviour
    {
        [Header("Navigation")]
        [Tooltip("Bouton retour optionnel. S'il est renseigné, il est câblé automatiquement sur Back().")]
        [SerializeField] private Button backButton;

        /// <summary>Identifiant unique de cet écran (utilisé par le manager pour le routage).</summary>
        public abstract ScreenId Id { get; }

        /// <summary>Manager propriétaire, injecté par <see cref="ScreenManager"/> au démarrage.</summary>
        protected ScreenManager Manager { get; private set; }

        /// <summary>État partagé du hub, injecté par <see cref="ScreenManager"/> au démarrage.</summary>
        protected UISession Session { get; private set; }

        /// <summary>Abstraction réseau (mock ou réel), injectée par <see cref="ScreenManager"/>.</summary>
        protected ILobbyService Lobby { get; private set; }

        /// <summary>
        /// Initialisation appelée une seule fois par le manager (injection des dépendances
        /// + câblage du bouton retour). Ne pas appeler manuellement.
        /// </summary>
        public void Init(ScreenManager manager, UISession session, ILobbyService lobby)
        {
            Manager = manager;
            Session = session;
            Lobby = lobby;

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(() => Manager.Back());
            }

            OnInit();
        }

        /// <summary>
        /// Crée un bouton retour standard (coin haut-gauche) câblé sur <see cref="ScreenManager.Back"/>.
        /// Mécanisme commun fourni par la base : les écrans non-racine l'appellent dans <see cref="OnInit"/>.
        /// </summary>
        protected Button AddBackButton(Transform parent)
        {
            var btn = UIFactory.CreateButton(parent, "BackButton", "← Retour", () => Manager.Back(),
                size: new Vector2(150f, 44f));
            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            return btn;
        }

        /// <summary>Affiche l'écran et déclenche son hook d'entrée.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            OnEnter();
        }

        /// <summary>Masque l'écran et déclenche son hook de sortie.</summary>
        public void Hide()
        {
            OnExit();
            gameObject.SetActive(false);
        }

        /// <summary>Hook d'initialisation unique (câblage des boutons propres à l'écran, etc.).</summary>
        protected virtual void OnInit() { }

        /// <summary>Appelé à chaque fois que l'écran devient visible.</summary>
        protected virtual void OnEnter() { }

        /// <summary>Appelé à chaque fois que l'écran est masqué.</summary>
        protected virtual void OnExit() { }
    }
}
