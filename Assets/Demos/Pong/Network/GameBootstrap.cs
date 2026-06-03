using System.Linq;
using UnityEngine;

namespace MMPong.Network
{
    public enum GameMode { Local, Host, Client }

    /// <summary>
    /// Point d'entrée qui décide si la scène tourne en local ou en réseau.
    /// <see cref="GameMode.Local"/> par défaut → jeu local inchangé (rien n'est instancié).
    /// La couche réseau est créée <b>par code</b> (aucun objet réseau dans la scène, aucun prefab)
    /// via <see cref="StartHost"/> / <see cref="StartClient"/> — appelables par l'inspecteur (mode),
    /// par le futur menu, ou par le lanceur de dev <see cref="DevNetLauncher"/>.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public GameMode mode = GameMode.Local;
        public int listenPort = 25000;
        public string serverIp = "127.0.0.1";

        void Start()
        {
            switch (mode)
            {
                case GameMode.Local: break;                 // jeu local normal : ne rien faire
                case GameMode.Host: StartHost(); break;
                case GameMode.Client: StartClient(serverIp); break;
            }
        }

        /// <summary>Démarre le serveur autoritatif + un client local (joueur host).</summary>
        public void StartHost()
        {
            PongPaddle[] paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None)
                .OrderBy(p => (int)p.Player)            // PlayerLeft(1) -> index 0, PlayerRight(2) -> index 1
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

            SpawnClient(serverIp, applyState: false);   // joueur host : envoie l'input, affiche sa propre sim
            Debug.Log($"[GameBootstrap] Host démarré : {paddles.Length} paddle(s), serveur:{listenPort}.");
        }

        /// <summary>Rejoint un serveur : passe la scène en affichage pur et crée le client.</summary>
        public void StartClient(string ip)
        {
            foreach (var p in FindObjectsByType<PongPaddle>(FindObjectsSortMode.None))
                p.RemoteDisplay = true;
            var ball = FindFirstObjectByType<PongBall>();
            if (ball != null) ball.RemoteDisplay = true;

            SpawnClient(ip, applyState: true);
            Debug.Log($"[GameBootstrap] Client démarré → serveur {ip}:{listenPort}.");
        }

        void SpawnClient(string ip, bool applyState)
        {
            var clientGo = new GameObject("NetworkClient");
            clientGo.AddComponent<UdpTransport>();
            var client = clientGo.AddComponent<NetworkClient>();
            client.serverIp = ip;
            client.serverPort = listenPort;
            client.listenPort = 0;   // port éphémère : aucun conflit si 2 instances sur la même machine
            if (applyState) clientGo.AddComponent<ClientStateApplier>();
        }
    }
}
