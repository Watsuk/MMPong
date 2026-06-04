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
            switch (mode)
            {
                case GameMode.Local: break;            // jeu local normal : ne rien faire
                case GameMode.Host: SetupHost(); break;
                case GameMode.Client:
                    Debug.Log("[GameBootstrap] mode Client : affichage pur (à implémenter en 2c).");
                    break;
            }
        }

        void SetupHost()
        {
            PongPaddle[] paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None)
                .OrderBy(p => (int)p.Player)          // PlayerBlue(1) -> index 0, PlayerRed(2) -> index 1
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

            // Joueur host : un client local en loopback (envoie son clavier, reçoit l'état).
            var clientGo = new GameObject("NetworkClient (host)");
            clientGo.AddComponent<UdpTransport>();
            var client = clientGo.AddComponent<NetworkClient>();
            client.serverIp = serverIp;
            client.serverPort = listenPort;
            client.listenPort = clientPort;
            clientGo.AddComponent<ClientStateLogger>();

            Debug.Log($"[GameBootstrap] Host démarré : {paddles.Length} paddle(s), serveur:{listenPort}, client:{clientPort}.");
        }
    }
}
