using MMPong.Network;
using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// Hand-off entre le hub UI et la partie (même scène, pas de <c>LoadScene</c>).
    ///
    /// - <b>En ligne</b> : la couche réseau est déjà créée par <see cref="NetworkLobbyService"/>
    ///   dès l'ouverture/connexion au salon. À la réception du START (<see cref="ILobbyService.GameStarted"/>),
    ///   il n'y a donc plus rien à instancier : on masque simplement le hub.
    /// - <b>Local</b> : <see cref="LaunchLocal"/> règle <see cref="GameBootstrap"/> en mode Local et
    ///   démarre la partie directement (config aléatoire).
    /// </summary>
    public class GameLauncher : MonoBehaviour
    {
        private ScreenManager manager;
        private UISession session;
        private ILobbyService lobby;
        private GameBootstrap bootstrap;

        /// <summary>Injection des dépendances (appelée par <c>HubBootstrap</c>).</summary>
        public void Init(ScreenManager manager, UISession session, ILobbyService lobby)
        {
            this.manager = manager;
            this.session = session;
            this.lobby = lobby;

            bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
                Debug.LogWarning("[GameLauncher] Aucun GameBootstrap dans la scène : la partie ne pourra pas démarrer.");

            if (lobby != null)
                lobby.GameStarted += OnGameStarted;
        }

        void OnDestroy()
        {
            if (lobby != null)
                lobby.GameStarted -= OnGameStarted;
        }

        /// <summary>Lance une partie locale rapide (config aléatoire, aucun écran en ligne).</summary>
        public void LaunchLocal()
        {
            if (bootstrap == null)
            {
                Debug.LogError("[GameLauncher] GameBootstrap introuvable, lancement local annulé.");
                return;
            }

            var config = MatchConfig.CreateRandom();
            if (PongGameManager.Instance != null)
                PongGameManager.Instance.SetActivePlayers(config.MaxPlayerCount);

            bootstrap.mode = GameMode.Local;
            manager?.HideHub();
            bootstrap.StartGame(string.IsNullOrEmpty(session.Pseudo) ? "Joueur" : session.Pseudo);
        }

        // En ligne : le réseau tourne déjà ; START reçu → on masque le hub (le HUD/jeu est dessous).
        private void OnGameStarted()
        {
            manager?.HideHub();
        }
    }
}
