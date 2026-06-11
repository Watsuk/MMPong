using System.Collections.Generic;
using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// Machine à états des écrans du hub (scène unique). Connaît tous les écrans,
    /// n'en affiche qu'un seul à la fois et expose la navigation :
    /// <see cref="Show"/> (avance) et <see cref="Back"/> (revient en arrière via une pile).
    ///
    /// Deux chemins d'initialisation :
    /// - <b>Inspecteur</b> : renseigner <c>screens</c> + <c>lobbyBehaviour</c> → init auto au démarrage.
    /// - <b>Code</b> : <see cref="Configure"/> (utilisé par <c>HubBootstrap</c> qui crée tout au runtime).
    ///
    /// C'est le SEUL point qui connaît la topologie des écrans : aucun écran ne
    /// référence un autre écran directement.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        [Header("Écrans (chemin inspecteur — optionnel)")]
        [Tooltip("Tous les écrans de la scène. Laisser vide si l'init se fait par code via Configure().")]
        [SerializeField] private UIScreen[] screens;

        [Tooltip("Écran affiché au démarrage.")]
        [SerializeField] private ScreenId initialScreen = ScreenId.ModeSelect;

        [Tooltip("Composant implémentant ILobbyService (mock ou réel).")]
        [SerializeField] private MonoBehaviour lobbyBehaviour;

        /// <summary>État partagé entre écrans (créé au démarrage du manager).</summary>
        public UISession Session { get; private set; }

        /// <summary>Abstraction réseau injectée aux écrans (mock ou réel).</summary>
        public ILobbyService Lobby { get; private set; }

        private readonly Dictionary<ScreenId, UIScreen> byId = new Dictionary<ScreenId, UIScreen>();
        private readonly Stack<ScreenId> history = new Stack<ScreenId>();
        private ScreenId current;
        private bool built;
        private bool shown;
        private GameObject hubRoot;

        void Awake()
        {
            Session = new UISession();

            // Chemin inspecteur : si des écrans sont déjà renseignés, on construit ici.
            if (screens != null && screens.Length > 0)
                Build(screens, lobbyBehaviour as ILobbyService, initialScreen, gameObject);
        }

        void Start()
        {
            // Chemin inspecteur : l'écran initial s'affiche au premier frame.
            if (built && !shown)
                ShowInitial();
        }

        /// <summary>
        /// Configuration par code (point d'entrée de <c>HubBootstrap</c>) : indexe les écrans,
        /// injecte les dépendances et affiche immédiatement l'écran initial.
        /// </summary>
        public void Configure(UIScreen[] allScreens, ILobbyService lobby, ScreenId initial, GameObject root = null)
        {
            Build(allScreens, lobby, initial, root != null ? root : gameObject);
            ShowInitial();
        }

        private void Build(UIScreen[] allScreens, ILobbyService lobby, ScreenId initial, GameObject root)
        {
            if (built)
                return;

            if (Session == null)
                Session = new UISession();

            Lobby = lobby;
            hubRoot = root;
            initialScreen = initial;

            byId.Clear();
            foreach (var screen in allScreens)
            {
                if (screen == null)
                    continue;

                byId[screen.Id] = screen;
                screen.Init(this, Session, Lobby);
                screen.gameObject.SetActive(false);
            }

            built = true;
        }

        private void ShowInitial()
        {
            current = initialScreen;
            history.Clear();
            shown = true;

            if (byId.TryGetValue(current, out var screen))
                screen.Show();
            else
                Debug.LogError($"[ScreenManager] Écran initial introuvable : {current}");
        }

        /// <summary>
        /// Affiche l'écran <paramref name="id"/> : masque l'écran courant, l'empile dans
        /// l'historique, puis montre la cible. Un seul écran reste visible.
        /// </summary>
        public void Show(ScreenId id)
        {
            if (!byId.TryGetValue(id, out var next))
            {
                Debug.LogError($"[ScreenManager] Écran inconnu : {id}");
                return;
            }

            if (id == current)
                return;

            if (byId.TryGetValue(current, out var cur))
                cur.Hide();

            history.Push(current);
            current = id;
            next.Show();
        }

        /// <summary>
        /// Revient à l'écran précédent. Sans effet si l'on est déjà sur l'écran racine
        /// (pile d'historique vide).
        /// </summary>
        public void Back()
        {
            if (history.Count == 0)
                return;

            var previous = history.Pop();

            if (byId.TryGetValue(current, out var cur))
                cur.Hide();

            current = previous;
            if (byId.TryGetValue(current, out var prev))
                prev.Show();
        }

        /// <summary>Masque tout le hub (au lancement de la partie, qui vit dans la même scène).</summary>
        public void HideHub()
        {
            if (hubRoot != null)
                hubRoot.SetActive(false);
        }

        /// <summary>Réaffiche le hub (retour au menu).</summary>
        public void ShowHub()
        {
            if (hubRoot != null)
                hubRoot.SetActive(true);
        }
    }
}
