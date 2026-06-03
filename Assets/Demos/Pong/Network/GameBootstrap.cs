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
        public int clientPort = 26000;   // port local du client (≠ serveur pour cohabiter sur une même machine)
        public string serverIp = "127.0.0.1";  // IP du serveur à rejoindre en mode Client

        void Start()
        {
            // Do nothing on Start. Wait for the Start Menu UI to call StartGame(pseudo).
        }

        public void StartGame(string pseudo)
        {
            PongBall ball = FindFirstObjectByType<PongBall>();
            if (ball != null) ball.StartGameFromMenu();

            switch (mode)
            {
                case GameMode.Local:
                    break;
                case GameMode.Host:
                    SetupHost(pseudo);
                    break;
                case GameMode.Client:
                    SetupClient(pseudo);
                    break;
            }
        }

        void SetupHost(string pseudo)
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
            var server = serverGo.AddComponent<NetworkServer>();
            server.bridge = bridge;
            server.listenPort = listenPort;

            var clientGo = new GameObject("NetworkClient (host)");
            clientGo.AddComponent<UdpTransport>();
            var client = clientGo.AddComponent<NetworkClient>();
            client.serverIp = "127.0.0.1";   // le host rejoint son propre serveur en local
            client.serverPort = listenPort;
            client.listenPort = clientPort;
            client.pseudo = string.IsNullOrEmpty(pseudo) ? "host" : pseudo;
            client.OnLobbyReceived += OnLobbyReceived;
            clientGo.AddComponent<ClientStateLogger>();

            Debug.Log($"[GameBootstrap] Host démarré : {paddles.Length} paddle(s), serveur:{listenPort}, client:{clientPort}. Pseudo: {client.pseudo}");
        }

        void SetupClient(string pseudo)
        {
            var clientGo = new GameObject("NetworkClient");
            clientGo.AddComponent<UdpTransport>();
            var client = clientGo.AddComponent<NetworkClient>();
            client.serverIp = serverIp;
            client.serverPort = listenPort;
            client.listenPort = clientPort;
            client.pseudo = string.IsNullOrEmpty(pseudo) ? "player" : pseudo;
            client.OnLobbyReceived += OnLobbyReceived;
            clientGo.AddComponent<ClientStateLogger>();

            Debug.Log($"[GameBootstrap] Client démarré, connexion à {serverIp}:{listenPort}. Pseudo: {client.pseudo}");
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
