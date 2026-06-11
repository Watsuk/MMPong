using System;
using System.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MMPong.Network
{
    /// <summary>
    /// Client réseau (miroir de <see cref="NetworkServer"/>). Capte l'intention du joueur
    /// (clavier) et l'envoie au serveur en <c>INPUT</c> à cadence fixe, reçoit les
    /// <c>STATE</c> autoritatifs et les expose via <see cref="OnStateReceived"/>. Ne
    /// contient aucune logique de jeu ni rendu : la couche jeu s'abonne à l'événement.
    /// </summary>
    [RequireComponent(typeof(UdpTransport))]
    public class NetworkClient : MonoBehaviour
    {
        public string serverIp = "127.0.0.1";
        public int serverPort = 25000;
        // 0 = port local éphémère attribué par l'OS. Indispensable pour que plusieurs clients
        // cohabitent sur une même machine : un port fixe ferait apparaître tous les clients au
        // serveur sous le même endpoint (127.0.0.1:port) et les fusionnerait sur un seul joueur.
        // Le serveur répond toujours à l'endpoint source observé → aucun port fixe n'est requis.
        public int listenPort = 0;
        public string pseudo = "player";
        public int teamIndex = 0;   // équipe choisie, envoyée au serveur au JOIN
        public int sendRate = 60;
        /// <summary>Délai (s) sans aucun paquet du serveur au-delà duquel on considère l'hôte perdu.</summary>
        public float serverTimeout = 3f;

        /// <summary>Émis à chaque snapshot reçu du serveur. Point de couture de la couche jeu.</summary>
        public event Action<GameState> OnStateReceived;

        /// <summary>Émis lorsque la liste des joueurs (Lobby) est mise à jour par le serveur (pseudos seuls).</summary>
        public event Action<string[]> OnLobbyReceived;

        /// <summary>Émis avec l'état lobby détaillé (pseudo + équipe + prêt) pour l'UI de salle d'attente.</summary>
        public event Action<LobbyPlayerInfo[]> OnLobbyDetailed;

        /// <summary>Émis à la réception de la configuration de match diffusée par le host.</summary>
        public event Action<MatchSettings> OnConfigReceived;

        /// <summary>Émis quand le serveur attribue l'identifiant local (WELCOME).</summary>
        public event Action<int> OnWelcome;

        /// <summary>Émis à la réception du START : la partie démarre (point de couture UI/jeu).</summary>
        public event Action OnGameStarted;

        /// <summary>Émis à la réception du END : la partie se termine avec l'id du gagnant.</summary>
        public event Action<int> OnGameEnded;

        /// <summary>
        /// Émis une seule fois quand le serveur (host) ne donne plus signe de vie au-delà de
        /// <see cref="serverTimeout"/> : l'hôte est considéré déconnecté (détection par timeout,
        /// couvre les pertes brutales : crash, fermeture sans Shutdown).
        /// </summary>
        public event Action OnServerLost;

        /// <summary>Émis quand le serveur signale explicitement une déconnexion/fermeture du salon (message DISCONNECT).</summary>
        public event Action OnDisconnected;

        /// <summary>Identifiant attribué par le serveur, ou -1 tant que le WELCOME n'est pas reçu.</summary>
        public int PlayerId => myId;

        /// <summary>
        /// Dernière direction d'input lue localement (−1/0/+1). Source unique partagée avec la
        /// prédiction du paddle local (<see cref="ClientStateApplier"/>) : le paddle prédit avec
        /// exactement l'input envoyé au serveur, donc sans divergence prédiction/serveur.
        /// </summary>
        public float CurrentDirection => currentDir;

        UdpTransport transport;
        IPEndPoint server;
        int myId = -1;
        float currentDir;
        float lastSentDir = float.NaN;
        float heartbeatTimer = 0f;          // cadence du keepalive INPUT (renvoie l'input même inchangé)
        const float HeartbeatInterval = 1f;
        float sendTimer;
        float lastServerPacketTime;         // dernier paquet reçu du serveur (détection de perte de l'hôte)
        bool serverLostFired;
        readonly SequenceGate stateGate = new SequenceGate();
        ReliableChannel serverChannel;

        void Start()
        {
            server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            transport = GetComponent<UdpTransport>();
            transport.OnData += OnData;
            transport.Open(listenPort);
            lastServerPacketTime = Time.time;
            serverChannel = new ReliableChannel(bytes => transport.Send(bytes, server));
            serverChannel.SendReliable(Protocol.BuildJoin(pseudo, teamIndex));
        }

        /// <summary>Envoie l'intention de déplacement courante au serveur (réseau pur).</summary>
        public void SendInput(float dir)
        {
            if (myId < 0) return;
            transport.Send(Protocol.Encode(Protocol.BuildInput(myId, dir)), server);
        }

        /// <summary>Signale au serveur que ce joueur est prêt (lobby). Envoyé de façon fiable.</summary>
        public void SendReady()
        {
            if (myId < 0) return;
            serverChannel.SendReliable(Protocol.BuildReady(myId));
        }

        /// <summary>Envoie un message de déconnexion au serveur (réseau pur).</summary>
        public void Disconnect()
        {
            if (transport != null && transport.IsOpen && server != null)
            {
                transport.Send(Protocol.Encode(Protocol.BuildDisconnect()), server);
            }
        }

        void OnData(byte[] data, IPEndPoint from)
        {
            // Tout datagramme reçu (STATE, LOBBY, ACK, ping serveur…) prouve que l'hôte est vivant.
            lastServerPacketTime = Time.time;

            Message m = Protocol.Decode(data);

            if (m.type == MessageType.Ack) { serverChannel.HandleAck(Protocol.ParseAck(m)); return; }
            if (m.reliable && !serverChannel.ReceiveReliable(m)) return;

            switch (m.type)
            {
                case MessageType.Welcome:
                    stateGate.Reset();
                    myId = Protocol.ParseWelcome(m);
                    Debug.Log($"[NetworkClient] WELCOME id={myId}");
                    OnWelcome?.Invoke(myId);
                    break;
                case MessageType.Config:
                    OnConfigReceived?.Invoke(Protocol.ParseConfig(m));
                    break;
                case MessageType.Lobby:
                    OnLobbyReceived?.Invoke(Protocol.ParseLobby(m));
                    OnLobbyDetailed?.Invoke(Protocol.ParseLobbyDetailed(m));
                    break;
                case MessageType.Start:
                    OnGameStarted?.Invoke();
                    break;
                case MessageType.End:
                    int winnerId = Protocol.ParseEnd(m);
                    Debug.Log($"[NetworkClient] Reçu END gagnant={winnerId}");
                    OnGameEnded?.Invoke(winnerId);
                    break;
                case MessageType.Disconnect:
                    Debug.Log("[NetworkClient] Reçu DISCONNECT du serveur.");
                    OnDisconnected?.Invoke();
                    break;
                case MessageType.State:
                    GameState s = Protocol.ParseState(m);
                    if (!stateGate.Accept(s.seq)) break;
                    OnStateReceived?.Invoke(s);
                    break;
            }
        }

        void Update()
        {
            serverChannel?.Tick(Time.deltaTime);

            // Contrôle réseau unifié : flèches ↑/↓ uniquement, pour TOUS les joueurs (↑ = +1, ↓ = -1).
            currentDir = ReadArrowDirection();

            heartbeatTimer += Time.deltaTime;
            bool changed = currentDir != lastSentDir;
            bool heartbeat = heartbeatTimer >= HeartbeatInterval;

            float step = 1f / sendRate;
            sendTimer += Time.deltaTime;
            while (sendTimer >= step)
            {
                sendTimer -= step;
                if (changed || heartbeat)
                {
                    SendInput(currentDir);
                    lastSentDir = currentDir;
                    if (heartbeat) heartbeatTimer = 0f;
                }
            }

            CheckServerAlive();
        }

        /// <summary>
        /// Déclenche <see cref="OnServerLost"/> une seule fois si aucun paquet du serveur n'est
        /// arrivé depuis <see cref="serverTimeout"/> (uniquement une fois identifié, pour ne pas
        /// se déclencher pendant la connexion initiale).
        /// </summary>
        void CheckServerAlive()
        {
            if (myId < 0 || serverLostFired) return;
            if (Time.time - lastServerPacketTime > serverTimeout)
            {
                serverLostFired = true;
                Debug.LogWarning("[NetworkClient] Hôte injoignable (timeout serveur) → OnServerLost.");
                OnServerLost?.Invoke();
            }
        }

        /// <summary>Direction verticale à partir des seules flèches ↑/↓ (↑ = +1, ↓ = -1, sinon 0).</summary>
        static float ReadArrowDirection()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0f;
            float dir = 0f;
            if (kb.upArrowKey.isPressed) dir += 1f;
            if (kb.downArrowKey.isPressed) dir -= 1f;
            return dir;
        }

        void OnDisable()
        {
            if (transport != null) transport.OnData -= OnData;
        }
    }
}
