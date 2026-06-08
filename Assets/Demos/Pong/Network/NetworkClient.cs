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
        public int sendRate = 30;

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

        /// <summary>Identifiant attribué par le serveur, ou -1 tant que le WELCOME n'est pas reçu.</summary>
        public int PlayerId => myId;

        UdpTransport transport;
        IPEndPoint server;
        int myId = -1;
        float currentDir;
        float sendTimer;
        readonly SequenceGate stateGate = new SequenceGate();
        ReliableChannel serverChannel;

        void Start()
        {
            server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            transport = GetComponent<UdpTransport>();
            transport.OnData += OnData;
            transport.Open(listenPort);
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

        void OnData(byte[] data, IPEndPoint from)
        {
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

            float step = 1f / sendRate;
            sendTimer += Time.deltaTime;
            while (sendTimer >= step)
            {
                sendTimer -= step;
                SendInput(currentDir);
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
