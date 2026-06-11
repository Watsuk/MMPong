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

        MatchSettings settings;
        readonly int[] teamById = new int[ClientRegistry.MaxPlayers];
        readonly bool[] readyById = new bool[ClientRegistry.MaxPlayers];
        readonly int[] colorById = new int[ClientRegistry.MaxPlayers];

        /// <summary>
        /// Définit la configuration du match (appelée par le lobby host avant <see cref="Start"/>).
        /// Borne <see cref="expectedPlayers"/> à la capacité réseau réelle.
        /// </summary>
        public void Configure(MatchSettings s)
        {
            settings = s;
            expectedPlayers = Mathf.Clamp(s.maxPlayers, 1, ClientRegistry.MaxPlayers);
        }

        void Start()
        {
            transport = GetComponent<UdpTransport>();
            registry = new ClientRegistry();
            hub = new ReliableHub((bytes, ep) => transport.Send(bytes, ep));
            match = new MatchCoordinator(expectedPlayers);
            pendingInput = new float[ClientRegistry.MaxPlayers];
            for (int i = 0; i < colorById.Length; i++) colorById[i] = -1; // -1 = couleur d'équipe par défaut

            // Valeurs par défaut si aucun Configure() (compat : démarrage hors hub).
            if (settings.maxPlayers == 0)
                settings = new MatchSettings
                {
                    maxPlayers = expectedPlayers, winType = 0, targetPoints = 5, duration = 120f,
                    teamAName = "Rouge", teamASkin = 0, teamBName = "Bleu", teamBSkin = 1
                };

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
            var (pseudo, team, color) = Protocol.ParseJoin(m);
            if (!registry.TryFindId(from, out int id)) id = registry.Register(from, pseudo);
            if (id < 0)
            {
                Debug.LogWarning("[NetworkServer] Partie pleine, JOIN refusé.");
                return;
            }

            if (id >= 0 && id < teamById.Length) teamById[id] = team;
            if (id >= 0 && id < colorById.Length) colorById[id] = color;

            hub.SendReliable(from, Protocol.BuildWelcome(id));
            hub.SendReliable(from, Protocol.BuildConfig(settings)); // le client reçoit la config du salon
            Debug.Log($"[NetworkServer] client joined id={id} pseudo={pseudo} team={team} color={color} from {from}");

            BroadcastLobby();
        }

        void BroadcastLobby()
            => hub.BroadcastReliable(registry.Endpoints,
                Protocol.BuildLobby(registry.Pseudos(), teamById, readyById, colorById));

        /// <summary>Démarrage autoritaire forcé par le host (bouton « Démarrer »).</summary>
        public void ForceStart()
        {
            if (!match.ForceStart()) return;
            bridge?.StartMatch();
            Debug.Log("[NetworkServer] START forcé par le host.");
            hub.BroadcastReliable(registry.Endpoints, Protocol.BuildStart());
        }

        void HandleInput(Message m)
        {
            var (id, dir) = Protocol.ParseInput(m);
            if (id >= 0 && id < ClientRegistry.MaxPlayers)
                pendingInput[id] = Mathf.Clamp(dir, -1f, 1f);
        }

        void HandleReady(Message m)
        {
            int id = Protocol.ParseReady(m);
            if (id >= 0 && id < readyById.Length) readyById[id] = true;

            BroadcastLobby(); // tout le monde voit l'état « prêt » mis à jour

            if (!match.TryStart(id)) return;

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

            if (state.phase == GamePhase.GameOver && !match.IsRematchWaiting)
            {
                match.PrepareRematch();
                for (int i = 0; i < readyById.Length; i++) readyById[i] = false;
                BroadcastLobby();
            }
        }

        void OnDisable()
        {
            if (transport != null) transport.OnData -= OnData;
        }
    }
}
