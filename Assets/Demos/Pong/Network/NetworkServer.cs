using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Serveur autoritatif. Tient le registre des clients, reçoit les INPUT, et à cadence fixe
    /// délègue à <see cref="ServerGameBridge"/> : applique l'input à la vraie simulation, lit le
    /// <see cref="GameState"/> résultant et le diffuse à tous.
    /// </summary>
    [RequireComponent(typeof(UdpTransport))]
    public class NetworkServer : MonoBehaviour
    {
        public int listenPort = 25000;
        public int tickRate = 30;

        /// <summary>Colle vers la simulation (posée par GameBootstrap). Sans elle, le serveur ne simule rien.</summary>
        public ServerGameBridge bridge;

        const int MaxPlayers = 4;

        UdpTransport transport;
        readonly Dictionary<int, IPEndPoint> clients = new Dictionary<int, IPEndPoint>();
        readonly float[] pendingInput = new float[MaxPlayers];
        GameState state;
        uint tickSeq;
        float tickTimer;

        void Start()
        {
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
                Tick();
            }
        }

        void Tick()
        {
            if (bridge == null) return;
            bridge.ApplyInput(pendingInput);
            state = bridge.BuildState(++tickSeq);
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
