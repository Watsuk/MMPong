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

        /// <summary>Identifiant attribué par le serveur, ou -1 tant que le WELCOME n'est pas reçu.</summary>
        public int PlayerId => myId;

        UdpTransport transport;
        IPEndPoint server;
        int myId = -1;
        float currentDir;
        float sendTimer;

        void Start()
        {
            server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            transport = GetComponent<UdpTransport>();
            transport.OnData += OnData;
            transport.Open(listenPort);
            transport.Send(Protocol.Encode(Protocol.BuildJoin(pseudo)), server);
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
            switch (m.type)
            {
                case MessageType.Welcome:
                    myId = Protocol.ParseWelcome(m);
                    Debug.Log($"[NetworkClient] WELCOME id={myId}");
                    break;
                case MessageType.State:
                    OnStateReceived?.Invoke(Protocol.ParseState(m));
                    break;
            }
        }

        void Update()
        {
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
