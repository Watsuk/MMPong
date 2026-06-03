using System.Linq;
using UnityEngine;

namespace MMPong.Network
{
    public enum GameMode { Local, Host, Client }

    /// <summary>
    /// Point d'entrée qui décide, au démarrage, si la scène tourne en local ou en réseau.
    /// <see cref="GameMode.Local"/> par défaut → jeu local inchangé (rien n'est instancié).
    /// En Host/Client, la couche réseau est créée <b>par code</b> (aucun objet réseau dans la
    /// scène, aucun prefab) : la scène locale reste intacte pour les coéquipiers.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public GameMode mode = GameMode.Local;
        public int listenPort = 25000;
        public string serverIp = "127.0.0.1";
        public int clientPort = 26000;

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
            client.serverIp = serverIp;
            client.serverPort = listenPort;
            client.listenPort = clientPort;
            client.pseudo = string.IsNullOrEmpty(pseudo) ? "host" : pseudo;
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
            clientGo.AddComponent<ClientStateLogger>();
            
            Debug.Log($"[GameBootstrap] Client démarré, connexion à {serverIp}:{listenPort}. Pseudo: {client.pseudo}");
        }
    }
}
