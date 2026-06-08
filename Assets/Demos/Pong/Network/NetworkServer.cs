using System.Net;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Serveur autoritatif (orchestrateur). Compose les collaborateurs réseau — registre des
    /// clients (<see cref="ClientRegistry"/>), fiabilité multi-pairs (<see cref="ReliableHub"/>),
    /// coordination du start (<see cref="MatchCoordinator"/>) — et pilote la boucle de simulation
    /// via <see cref="ServerGameBridge"/>. Chaque responsabilité métier vit dans sa classe dédiée ;
    /// ce composant ne fait qu'aiguiller et orchestrer le cycle Unity.
    /// </summary>
    [RequireComponent(typeof(UdpTransport))]
    public class NetworkServer : MonoBehaviour
    {
        public int listenPort = 25000;
        public int tickRate = 30;
        public int expectedPlayers = 2;

        /// <summary>Colle vers la simulation (posée par GameBootstrap). Sans elle, le serveur ne simule rien.</summary>
        public ServerGameBridge bridge;

        UdpTransport transport;
        ClientRegistry registry;
        ReliableHub hub;
        MatchCoordinator match;
        float[] pendingInput;
        GameState state;
        uint tickSeq;
        float tickTimer;

        void Start()
        {
            transport = GetComponent<UdpTransport>();
            registry = new ClientRegistry();
            hub = new ReliableHub((bytes, ep) => transport.Send(bytes, ep));
            match = new MatchCoordinator(expectedPlayers);
            pendingInput = new float[ClientRegistry.MaxPlayers];

            transport.OnData += OnData;
            transport.Open(listenPort);
        }

        void OnData(byte[] data, IPEndPoint from)
        {
            Message m = Protocol.Decode(data);

            if (m.type == MessageType.Ack) { hub.HandleAck(from, Protocol.ParseAck(m)); return; }
            if (m.reliable && !hub.ReceiveReliable(from, m)) return;

            switch (m.type)
            {
                case MessageType.Join: HandleJoin(m, from); break;
                case MessageType.Input: HandleInput(m); break;
                case MessageType.Ready: HandleReady(m); break;
            }
        }

        void HandleJoin(Message m, IPEndPoint from)
        {
            string pseudo = Protocol.ParseJoin(m);
            if (!registry.TryFindId(from, out int id)) id = registry.Register(from, pseudo);
            if (id < 0)
            {
                Debug.LogWarning("[NetworkServer] Partie pleine, JOIN refusé.");
                return;
            }

            hub.SendReliable(from, Protocol.BuildWelcome(id));
            Debug.Log($"[NetworkServer] client joined id={id} pseudo={pseudo} from {from}");

            hub.BroadcastReliable(registry.Endpoints, Protocol.BuildLobby(registry.Pseudos()));
        }

        void HandleInput(Message m)
        {
            var (id, dir) = Protocol.ParseInput(m);
            if (id >= 0 && id < ClientRegistry.MaxPlayers)
                pendingInput[id] = Mathf.Clamp(dir, -1f, 1f);
        }

        void HandleReady(Message m)
        {
            if (!match.TryStart(Protocol.ParseReady(m))) return;

            bridge?.StartMatch();
            Debug.Log($"[NetworkServer] START ({match.ReadyCount}/{expectedPlayers} prêts).");
            hub.BroadcastReliable(registry.Endpoints, Protocol.BuildStart());
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

            hub.TickAll(Time.deltaTime);
        }

        void Tick()
        {
            if (!match.Started || bridge == null) return;
            bridge.ApplyInput(pendingInput);
            state = bridge.BuildState(++tickSeq);

            byte[] bytes = Protocol.Encode(Protocol.BuildState(state));
            foreach (var ep in registry.Endpoints)
                transport.Send(bytes, ep);
        }

        void OnDisable()
        {
            if (transport != null) transport.OnData -= OnData;
        }
    }
}
