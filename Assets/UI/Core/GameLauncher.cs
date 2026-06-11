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

        /// <summary>
        /// Lance une partie locale à 2 joueurs. Les noms d'équipe et pseudos saisis dans le menu
        /// alimentent le HUD (équipe A = gauche, équipe B = droite ; joueur 1 = gauche, 2 = droite).
        /// Les valeurs vides retombent sur des libellés par défaut.
        /// </summary>
        public void LaunchLocal(string teamA = null, string teamB = null, string pseudo1 = null, string pseudo2 = null,
            int color1 = -1, int color2 = -1)
        {
            if (bootstrap == null)
            {
                Debug.LogError("[GameLauncher] GameBootstrap introuvable, lancement local annulé.");
                return;
            }

            var config = MatchConfig.CreateRandom();
            config.MaxPlayerCount = 2; // Toujours 2 joueurs en local
            if (!string.IsNullOrWhiteSpace(teamA)) config.TeamA.Name = teamA.Trim();
            if (!string.IsNullOrWhiteSpace(teamB)) config.TeamB.Name = teamB.Trim();

            if (PongGameManager.Instance != null)
                PongGameManager.Instance.SetActivePlayers(config.MaxPlayerCount);

            // HUD : noms d'équipe (remplacent « Équipe Bleue/Rouge ») + pseudos sous chaque nom.
            var score = FindFirstObjectByType<PongScore>();
            if (score != null)
            {
                score.SetTeamNames(config.TeamA.Name, config.TeamB.Name);
                score.SetTeamPlayers(pseudo1, pseudo2);
            }

            // Couleur de paddle choisie par chaque joueur (J1 = gauche, J2 = droite).
            foreach (var paddle in FindObjectsByType<PongPaddle>(FindObjectsSortMode.None))
            {
                if (paddle.Player == PongPlayer.PlayerLeft && color1 >= 0) paddle.SetColorId(color1);
                else if (paddle.Player == PongPlayer.PlayerRight && color2 >= 0) paddle.SetColorId(color2);
            }

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
