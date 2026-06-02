using System.Net;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Doublure jetable d'un joueur, pour tester <see cref="NetworkServer"/> seul en
    /// loopback. Envoie un JOIN puis des INPUT alternés, et logge (en throttlé) la
    /// position de son paddle reçue dans les STATE. À retirer une fois le vrai client prêt.
    /// </summary>
    [RequireComponent(typeof(UdpTransport))]
    public class FakeClient : MonoBehaviour
    {
        public string serverIp = "127.0.0.1";
        public int serverPort = 25000;
        public int listenPort = 26000;
        public string pseudo = "tester";
        public float inputPeriod = 0.5f;

        UdpTransport transport;
        IPEndPoint server;
        int myId = -1;
        float dir = 1f;
        float inputTimer;
        float logTimer;
        float lastPaddleY;
        bool gotState;

        void Start()
        {
            server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            transport = GetComponent<UdpTransport>();
            transport.OnData += OnData;
            transport.Open(listenPort);
            transport.Send(Protocol.Encode(Protocol.BuildJoin(pseudo)), server);
        }

        void OnData(byte[] data, IPEndPoint from)
        {
            Message m = Protocol.Decode(data);
            if (m.type == MessageType.Welcome)
            {
                myId = Protocol.ParseWelcome(m);
                Debug.Log($"[FakeClient] WELCOME id={myId}");
            }
            else if (m.type == MessageType.State && myId >= 0)
            {
                lastPaddleY = Protocol.ParseState(m).paddleY[myId];
                gotState = true;
            }
        }

        void Update()
        {
            if (myId < 0) return;

            inputTimer += Time.deltaTime;
            if (inputTimer >= inputPeriod)
            {
                inputTimer -= inputPeriod;
                dir = -dir;
                transport.Send(Protocol.Encode(Protocol.BuildInput(myId, dir)), server);
            }

            logTimer += Time.deltaTime;
            if (gotState && logTimer >= 0.5f)
            {
                logTimer = 0f;
                Debug.Log($"[FakeClient] mon paddle Y = {lastPaddleY:F2}");
            }
        }

        void OnDisable()
        {
            if (transport != null) transport.OnData -= OnData;
        }
    }
}
