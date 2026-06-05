using System;
using System.Net;
using UnityEngine;

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
        public int listenPort = 26000;
        public string pseudo = "player";
        public int sendRate = 30;

        /// <summary>Émis à chaque snapshot reçu du serveur. Point de couture de la couche jeu.</summary>
        public event Action<GameState> OnStateReceived;

        /// <summary>Émis lorsque la liste des joueurs (Lobby) est mise à jour par le serveur.</summary>
        public event Action<string[]> OnLobbyReceived;

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
            serverChannel.SendReliable(Protocol.BuildJoin(pseudo));
        }

        /// <summary>Envoie l'intention de déplacement courante au serveur (réseau pur).</summary>
        public void SendInput(float dir)
        {
            if (myId < 0) return;
            transport.Send(Protocol.Encode(Protocol.BuildInput(myId, dir)), server);
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
                    break;
                case MessageType.Lobby:
                    OnLobbyReceived?.Invoke(Protocol.ParseLobby(m));
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

            currentDir = Input.GetAxisRaw("Vertical");

            float step = 1f / sendRate;
            sendTimer += Time.deltaTime;
            while (sendTimer >= step)
            {
                sendTimer -= step;
                SendInput(currentDir);
            }
        }

        void OnDisable()
        {
            if (transport != null) transport.OnData -= OnData;
        }
    }
}
