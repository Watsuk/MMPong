using System;
using System.Collections.Generic;
using MMPong.Network;
using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// Implémentation réseau réelle de <see cref="ILobbyService"/> : relie le hub UI à la couche
    /// <see cref="MMPong.Network"/>. Le host crée le serveur autoritatif + son client local ; le
    /// client se connecte au serveur distant. La configuration du salon et la liste des joueurs
    /// (pseudo/équipe/prêt) sont propagées par le serveur à <b>tous</b> les clients (messages
    /// CONFIG + LOBBY), de sorte que chaque écran affiche les mêmes informations.
    ///
    /// Drop-in du <see cref="FakeLobbyService"/> : mêmes events, aucun écran ne change — seule
    /// l'instance injectée par <c>HubBootstrap</c> diffère.
    /// </summary>
    public class NetworkLobbyService : MonoBehaviour, ILobbyService
    {
        private UISession session;
        private GameBootstrap bootstrap;
        private NetworkClient client;
        private NetworkServer server;
        private bool isHost;
        private bool pendingReady; // « prêt » demandé avant la réception du WELCOME (PlayerId pas encore attribué)

        private readonly List<PlayerInfo> players = new List<PlayerInfo>();

        public event Action<IReadOnlyList<PlayerInfo>> PlayersChanged;
        public event Action GameStarted;
        public event Action<JoinResult> JoinResult;

        public IReadOnlyList<PlayerInfo> Players => players;
        public bool IsHost => isHost;
        public int LocalPlayerId => client != null ? client.PlayerId : -1;

        /// <summary>Injection (appelée par <c>HubBootstrap</c>). Localise le <see cref="GameBootstrap"/> de la scène.</summary>
        public void Init(UISession session)
        {
            this.session = session;
            bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
                Debug.LogError("[NetworkLobbyService] Aucun GameBootstrap dans la scène : impossible d'ouvrir/rejoindre un salon.");
        }

        public void Host(MatchConfig config)
        {
            if (bootstrap == null) return;
            isHost = true;
            session.MatchConfig = config;

            string pseudo = string.IsNullOrEmpty(session.Pseudo) ? "Hôte" : session.Pseudo;
            bootstrap.mode = GameMode.Host;

            if (PongGameManager.Instance != null)
                PongGameManager.Instance.SetActivePlayers(config.MaxPlayerCount);

            client = bootstrap.SetupHost(pseudo, out server);
            server.Configure(ToSettings(config));

            Subscribe(client);
        }

        public void Join(string ip, string pseudo, int teamIndex)
        {
            if (bootstrap == null) return;
            isHost = false;

            bootstrap.mode = GameMode.Client;
            bootstrap.serverIp = ip;

            client = bootstrap.SetupClient(pseudo, teamIndex);
            Subscribe(client);
        }

        public void SetReady(bool ready)
        {
            // Le protocole ne gère que « prêt » (pas d'annulation) ; un READY suffit côté serveur.
            if (!ready) return;

            // SendReady() nécessite le PlayerId (WELCOME). Si pas encore reçu, on diffère.
            if (client != null && client.PlayerId >= 0)
                client.SendReady();
            else
                pendingReady = true;
        }

        public void StartGame()
        {
            // Démarrage autoritaire réservé au host (le serveur diffuse START à tous).
            if (isHost && server != null)
                server.ForceStart();
        }

        void OnDestroy()
        {
            if (client == null) return;
            client.OnWelcome -= OnWelcome;
            client.OnConfigReceived -= OnConfig;
            client.OnLobbyDetailed -= OnLobby;
            client.OnGameStarted -= OnStarted;
        }

        private void Subscribe(NetworkClient c)
        {
            if (c == null) return;
            c.OnWelcome += OnWelcome;
            c.OnConfigReceived += OnConfig;
            c.OnLobbyDetailed += OnLobby;
            c.OnGameStarted += OnStarted;
        }

        private void OnWelcome(int id)
        {
            JoinResult?.Invoke(MMPong.UI.JoinResult.Succeeded(id));

            // « prêt » demandé avant l'attribution de l'id : on l'envoie maintenant.
            if (pendingReady)
            {
                pendingReady = false;
                client?.SendReady();
            }
        }

        private void OnConfig(MatchSettings s)
        {
            // Le client découvre la config du salon (noms d'équipes, condition de victoire…).
            session.MatchConfig = FromSettings(s);

            if (PongGameManager.Instance != null)
                PongGameManager.Instance.SetActivePlayers(s.maxPlayers);

            PlayersChanged?.Invoke(players); // pousse un rafraîchissement de l'UI (labels d'équipes)
        }

        private void OnLobby(LobbyPlayerInfo[] arr)
        {
            players.Clear();
            foreach (var p in arr)
                players.Add(new PlayerInfo(p.id, p.pseudo, p.team, p.ready));
            PlayersChanged?.Invoke(players);
        }

        private void OnStarted() => GameStarted?.Invoke();

        // ── Mapping MatchConfig (UI) ↔ MatchSettings (réseau) ────────────────────

        private static MatchSettings ToSettings(MatchConfig c) => new MatchSettings
        {
            maxPlayers = c.MaxPlayerCount,
            winType = c.WinCondition.Type == WinConditionType.Timer ? 1 : 0,
            targetPoints = c.WinCondition.TargetPoints,
            duration = c.WinCondition.DurationSeconds,
            teamAName = c.TeamA.Name,
            teamASkin = c.TeamA.SkinId,
            teamBName = c.TeamB.Name,
            teamBSkin = c.TeamB.SkinId
        };

        private static MatchConfig FromSettings(MatchSettings s) => new MatchConfig
        {
            MaxPlayerCount = s.maxPlayers,
            WinCondition = new WinCondition
            {
                Type = s.winType == 1 ? WinConditionType.Timer : WinConditionType.Points,
                TargetPoints = s.targetPoints,
                DurationSeconds = s.duration
            },
            TeamA = new TeamConfig(s.teamAName, s.teamASkin),
            TeamB = new TeamConfig(s.teamBName, s.teamBSkin)
        };
    }
}
