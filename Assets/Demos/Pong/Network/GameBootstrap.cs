using System.Linq;
using UnityEngine;

namespace MMPong.Network
{
    public enum GameMode { Local, Host, Client }

    /// <summary>
    /// Point d'entrée qui décide si la scène tourne en local ou en réseau.
    /// <see cref="GameMode.Local"/> par défaut → jeu local inchangé (rien n'est instancié).
    /// La couche réseau est créée <b>par code</b> (aucun objet réseau dans la scène, aucun prefab)
    /// via <see cref="StartGame"/>, appelée par le menu d'accueil <c>PongStartUI</c>. Le mode
    /// (Local / Host / Client) et l'IP du serveur à rejoindre se règlent dans l'inspecteur.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public GameMode mode = GameMode.Local;
        public int listenPort = 25000;   // port du serveur (host)
        // Obsolète : le client utilise désormais un port local éphémère (voir NetworkClient.listenPort = 0)
        // pour permettre plusieurs clients sur une même machine. Conservé pour compat inspecteur.
        public int clientPort = 26000;
        public string serverIp = "127.0.0.1";  // IP du serveur à rejoindre en mode Client

        void Start()
        {
            // Le démarrage réel attend que le menu (PongStartUI) appelle StartGame(pseudo).
            // En revanche, sous Multiplayer Play Mode (test multi-fenêtres sans build), on
            // résout ici le rôle de cette fenêtre depuis ses tags MPPM → « Play et c'est parti ».
#if UNITY_EDITOR
            ApplyMultiplayerPlayModeRole();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Force <see cref="mode"/> selon les tags Multiplayer Play Mode de la fenêtre courante :
        /// tag « Client » → Client, tag « Host » → Host. Sans tag pertinent, on garde le mode de
        /// l'inspecteur (usage normal / build inchangé). Accès par réflexion pour ne pas créer de
        /// dépendance dure au package MPPM (le code compile même s'il n'est pas installé).
        /// </summary>
        void ApplyMultiplayerPlayModeRole()
        {
            foreach (string tag in GetMultiplayerPlayModeTags())
            {
                if (string.Equals(tag, "Client", System.StringComparison.OrdinalIgnoreCase))
                {
                    mode = GameMode.Client;
                    Debug.Log("[GameBootstrap] MPPM : tag « Client » détecté → mode Client.");
                    return;
                }
                if (string.Equals(tag, "Host", System.StringComparison.OrdinalIgnoreCase))
                {
                    mode = GameMode.Host;
                    Debug.Log("[GameBootstrap] MPPM : tag « Host » détecté → mode Host.");
                    return;
                }
            }
        }

        static string[] GetMultiplayerPlayModeTags()
        {
            var type = System.Type.GetType("Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode");
            var method = type?.GetMethod("ReadOnlyTags",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return method?.Invoke(null, null) as string[] ?? System.Array.Empty<string>();
        }
#endif

        public void StartGame(string pseudo)
        {
            switch (mode)
            {
                case GameMode.Local:
                    // Jeu local : démarrage immédiat, pas de lobby réseau.
                    PongBall ball = FindFirstObjectByType<PongBall>();
                    if (ball != null) ball.StartGameFromMenu();
                    break;
                case GameMode.Host:
                    SetupHost(pseudo, out _);
                    break;
                case GameMode.Client:
                    SetupClient(pseudo);
                    break;
            }
        }

        /// <summary>
        /// Crée la couche réseau host (serveur autoritatif + client local) <b>sans démarrer le match</b>
        /// (le match démarre au START : quota de prêts ou <see cref="NetworkServer.ForceStart"/>).
        /// Retourne le client (pour s'abonner aux events lobby) et expose le serveur via <paramref name="server"/>.
        /// </summary>
        public NetworkClient SetupHost(string pseudo, out NetworkServer server)
        {
            PongPaddle[] paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None)
                .OrderBy(p => (int)p.Player)
                .ToArray();
            PongBall ball = FindFirstObjectByType<PongBall>();

            var serverGo = new GameObject("NetworkServer");
            serverGo.AddComponent<UdpTransport>();
            var bridge = serverGo.AddComponent<ServerGameBridge>();
            bridge.paddles = paddles;
            bridge.ball = ball;
            server = serverGo.AddComponent<NetworkServer>();
            server.bridge = bridge;
            server.listenPort = listenPort;
            server.expectedPlayers = PongGameManager.Instance != null ? PongGameManager.Instance.totalPlayers : 2;

            var clientGo = new GameObject("NetworkClient (host)");
            clientGo.AddComponent<UdpTransport>();
            var client = clientGo.AddComponent<NetworkClient>();
            client.serverIp = "127.0.0.1";   // le host rejoint son propre serveur en local
            client.serverPort = listenPort;
            // port client laissé éphémère (listenPort = 0) pour cohabiter avec d'autres clients
            // sur la même machine. Le host affiche la simulation réelle (pas de RemoteDisplay ici).
            client.pseudo = string.IsNullOrEmpty(pseudo) ? "host" : pseudo;
            client.OnLobbyReceived += OnLobbyReceived;
            client.OnConfigReceived += OnConfigReceived;
            client.OnGameStarted += OnGameStarted;
            clientGo.AddComponent<ClientStateLogger>();
            clientGo.AddComponent<DevReadyTrigger>().client = client;

            Debug.Log($"[GameBootstrap] Host démarré : {paddles.Length} paddle(s), serveur:{listenPort}, client:{clientPort}. Pseudo: {client.pseudo}");
            return client;
        }

        /// <summary>
        /// Crée le client réseau (afficheur pur) connecté à <see cref="serverIp"/>, avec l'équipe choisie.
        /// Retourne le client pour s'abonner aux events lobby.
        /// </summary>
        public NetworkClient SetupClient(string pseudo, int teamIndex = 0)
        {
            // Le client n'est qu'un afficheur : balle et paddles sont pilotés par l'état serveur,
            // jamais simulés localement. On bascule la scène en RemoteDisplay (miroir du host qui,
            // lui, pose DrivenExternally via ServerGameBridge).
            PongPaddle[] paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);
            foreach (var p in paddles)
                if (p != null) p.RemoteDisplay = true;
            PongBall ball = FindFirstObjectByType<PongBall>();
            if (ball != null) ball.RemoteDisplay = true;

            var clientGo = new GameObject("NetworkClient");
            clientGo.AddComponent<UdpTransport>();
            var client = clientGo.AddComponent<NetworkClient>();
            client.serverIp = serverIp;
            client.serverPort = listenPort;
            // port client laissé éphémère (listenPort = 0) pour cohabiter sur la même machine.
            client.pseudo = string.IsNullOrEmpty(pseudo) ? "player" : pseudo;
            client.teamIndex = teamIndex;
            client.OnLobbyReceived += OnLobbyReceived;
            client.OnConfigReceived += OnConfigReceived;
            client.OnGameStarted += OnGameStarted;
            clientGo.AddComponent<ClientStateApplier>();   // applique les STATE reçus à la scène
            clientGo.AddComponent<ClientStateLogger>();
            clientGo.AddComponent<DevReadyTrigger>().client = client;

            Debug.Log($"[GameBootstrap] Client démarré, connexion à {serverIp}:{listenPort}. Pseudo: {client.pseudo}");
            return client;
        }

        void OnGameStarted()
        {
            // Seam pour UI-2 : masquer le lobby / afficher le HUD. Le rendu réseau suit déjà l'état serveur.
            Debug.Log("[GameBootstrap] Partie démarrée (START reçu).");
        }

        /// <summary>
        /// Config de match reçue du serveur (noms d'équipe définis par le host) : on alimente
        /// le HUD de score avec les vrais noms d'équipe (remplace les faux noms par défaut).
        /// </summary>
        void OnConfigReceived(MatchSettings settings)
        {
            PongScore score = FindFirstObjectByType<PongScore>();
            if (score != null) score.SetTeamNames(settings.teamAName, settings.teamBName);
        }

        void OnLobbyReceived(string[] pseudos)
        {
            int localId = -1;
            NetworkClient client = FindFirstObjectByType<NetworkClient>();
            if (client != null) localId = client.PlayerId;

            PongPaddle[] paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None)
                .OrderBy(p => (int)p.Player)
                .ToArray();
                
            for (int i = 0; i < paddles.Length && i < pseudos.Length; i++)
            {
                if (paddles[i] != null)
                {
                    paddles[i].SetPseudo(pseudos[i]);
                    if (i == localId) {
                        paddles[i].SetAsLocalPlayer();
                    }
                }
            }
        }
    }
}
