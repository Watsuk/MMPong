using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Serveur autoritatif (version réseau minimale). Tient le registre des clients,
    /// reçoit les INPUT, fait avancer un <see cref="GameState"/> à cadence fixe et le
    /// diffuse à tous. La physique de la balle n'est pas encore branchée (placeholder).
    /// </summary>
    [RequireComponent(typeof(UdpTransport))]
    public class NetworkServer : MonoBehaviour
    {
        public int listenPort = 25000;
        public int tickRate = 30;
        public float paddleSpeed = 5f;
        public float minY = -4f;
        public float maxY = 4f;

        const int MaxPlayers = 4;

        UdpTransport transport;
        readonly Dictionary<int, IPEndPoint> clients = new Dictionary<int, IPEndPoint>();
        readonly float[] pendingInput = new float[MaxPlayers];
        GameState state;
        uint tickSeq;
        float tickTimer;

        void Start()
        {
            state = new GameState
            {
                ballPos = Vector2.zero,
                paddleY = new float[MaxPlayers],
                scores = new int[MaxPlayers]
            };

            transport = GetComponent<UdpTransport>();
            transport.OnData += OnData;
            transport.Open(listenPort);
        }

        void OnData(byte[] data, IPEndPoint from)
        {
            Message m = Protocol.Decode(data);
            switch (m.type)
            {
                case MessageType.Join: HandleJoin(from); break;
                case MessageType.Input: HandleInput(m); break;
            }
        }

        void HandleJoin(IPEndPoint from)
        {
            int id = FindClientId(from);
            if (id < 0) id = AssignId(from);
            if (id < 0)
            {
                Debug.LogWarning("[NetworkServer] Partie pleine, JOIN refusé.");
                return;
            }

            transport.Send(Protocol.Encode(Protocol.BuildWelcome(id)), from);
            Debug.Log($"[NetworkServer] client joined id={id} from {from}");
        }

        void HandleInput(Message m)
        {
            var (id, dir) = Protocol.ParseInput(m);
            if (id >= 0 && id < MaxPlayers)
                pendingInput[id] = Mathf.Clamp(dir, -1f, 1f);
        }

        void Update()
        {
            float step = 1f / tickRate;
            tickTimer += Time.deltaTime;
            while (tickTimer >= step)
            {
                tickTimer -= step;
                Tick(step);
            }
        }

        void Tick(float dt)
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                state.paddleY[i] = Mathf.Clamp(
                    state.paddleY[i] + pendingInput[i] * paddleSpeed * dt, minY, maxY);
            }

            state.seq = ++tickSeq;
            Broadcast(Protocol.BuildState(state));
        }

        void Broadcast(Message m)
        {
            byte[] bytes = Protocol.Encode(m);
            foreach (var ep in clients.Values)
                transport.Send(bytes, ep);
        }

        int FindClientId(IPEndPoint ep)
        {
            foreach (var kv in clients)
                if (kv.Value.Equals(ep)) return kv.Key;
            return -1;
        }

        int AssignId(IPEndPoint ep)
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (!clients.ContainsKey(i))
                {
                    clients[i] = ep;
                    return i;
                }
            }
            return -1;
        }

        void OnDisable()
        {
            if (transport != null) transport.OnData -= OnData;
        }
    }
}
