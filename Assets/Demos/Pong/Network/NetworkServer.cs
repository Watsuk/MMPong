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

        /// <summary>Délai (s) sans battement de cœur au-delà duquel un joueur est marqué déconnecté.</summary>
        public float heartbeatTimeout = 3f;

        /// <summary>
        /// Fréquence (Hz) des pings serveur→clients. Donne aux clients un signal de vie régulier de
        /// l'hôte hors phase de jeu (lobby, écran de fin) ; pendant le match, le flux STATE suffit déjà.
        /// </summary>
        public float clientPingRate = 1f;

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

        // État de connexion par battements de cœur : date du dernier heartbeat reçu et statut courant.
        readonly float[] lastSeenById = new float[ClientRegistry.MaxPlayers];
        readonly bool[] connectedById = new bool[ClientRegistry.MaxPlayers];
        float clientPingTimer;

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
                case MessageType.Input: HandleInput(m, from); break;
                case MessageType.Ready: HandleReady(m); break;
                case MessageType.Disconnect: HandleDisconnect(from); break;
            }
        }

        /// <summary>
        /// Rafraîchit le signe de vie d'un joueur (résolu par son endpoint). S'il était marqué
        /// déconnecté, il revient « connecté » et on rediffuse le lobby pour repasser sa pastille au
        /// vert chez les autres. Appelé à chaque INPUT, qui sert de keepalive (renvoyé ≥1×/s par le
        /// client même quand l'input ne change pas).
        /// </summary>
        void MarkSeen(IPEndPoint from)
        {
            if (!registry.TryFindId(from, out int id) || id < 0 || id >= ClientRegistry.MaxPlayers) return;

            lastSeenById[id] = Time.time;
            if (!connectedById[id])
            {
                connectedById[id] = true;
                Debug.Log($"[NetworkServer] joueur id={id} reconnecté.");
                BroadcastLobby();
            }
        }

        void HandleJoin(Message m, IPEndPoint from)
        {
            var (pseudo, team) = Protocol.ParseJoin(m);

            // Nouveau client : refuser si le match est complet, sinon enregistrer + répartir l'équipe.
            // Re-JOIN (endpoint déjà connu) : idempotent, on conserve l'équipe déjà attribuée.
            bool isNew = !registry.TryFindId(from, out int id);
            if (isNew)
            {
                if (registry.Count >= expectedPlayers)
                {
                    Debug.LogWarning($"[NetworkServer] Match complet ({expectedPlayers} joueurs), JOIN refusé.");
                    return;
                }
                id = registry.Register(from, pseudo);
                if (id < 0)
                {
                    Debug.LogWarning("[NetworkServer] Partie pleine, JOIN refusé.");
                    return;
                }
                if (id < teamById.Length) teamById[id] = AssignTeam(id, team);
                if (id < connectedById.Length)
                {
                    connectedById[id] = true;
                    lastSeenById[id] = Time.time;
                }
            }

            if (id >= 0 && id < teamById.Length)
            {
                if (bridge != null && bridge.paddles != null && id < bridge.paddles.Length && bridge.paddles[id] != null)
                {
                    bridge.paddles[id].TeamIndex = teamById[id];
                    int colorId = teamById[id] == 0 ? settings.teamASkin : settings.teamBSkin;
                    bridge.paddles[id].SetColorId(colorId);
                }
            }

            if (PongGameManager.Instance != null)
            {
                PongGameManager.Instance.AssignPaddlesToCircles();
            }

            hub.SendReliable(from, Protocol.BuildWelcome(id));
            hub.SendReliable(from, Protocol.BuildConfig(settings)); // le client reçoit la config du salon
            Debug.Log($"[NetworkServer] client joined id={id} pseudo={pseudo} team={teamById[id]} from {from}");

            BroadcastLobby();
        }

        /// <summary>
        /// Répartit un nouveau joueur entre les deux équipes : on honore l'équipe demandée si elle a
        /// de la place, sinon on le bascule dans l'équipe libre. Plafond par équipe = ceil(expectedPlayers/2)
        /// (2 joueurs → 1 par équipe, 4 → 2, etc.).
        /// </summary>
        int AssignTeam(int newId, int requested)
        {
            int wanted = requested == 1 ? 1 : 0;
            int other = 1 - wanted;
            int cap = (expectedPlayers + 1) / 2; // ceil(expectedPlayers / 2)

            // Compte les membres déjà attribués dans chaque équipe (le nouveau joueur est déjà
            // enregistré mais son équipe n'est pas encore fixée → on l'exclut).
            int countWanted = 0, countOther = 0;
            foreach (int id in registry.Ids)
            {
                if (id == newId) continue;
                if (teamById[id] == wanted) countWanted++;
                else countOther++;
            }

            // On honore l'équipe demandée si elle a de la place, sinon on bascule dans l'autre.
            if (countWanted < cap) return wanted;
            if (countOther < cap) return other;

            // Repli (ne devrait pas arriver, total borné à expectedPlayers) : l'équipe la moins remplie.
            return countWanted <= countOther ? wanted : other;
        }

        void BroadcastLobby()
            => hub.BroadcastReliable(registry.Endpoints,
                Protocol.BuildLobby(registry.Pseudos(), teamById, readyById, connectedById));

        /// <summary>Démarrage autoritaire forcé par le host (bouton « Démarrer »).</summary>
        public void ForceStart()
        {
            if (!match.ForceStart()) return;
            bridge?.StartMatch();
            Debug.Log("[NetworkServer] START forcé par le host.");
            hub.BroadcastReliable(registry.Endpoints, Protocol.BuildStart());
        }

        void HandleInput(Message m, IPEndPoint from)
        {
            MarkSeen(from); // l'INPUT régulier tient le joueur « connecté » (liveness des pastilles)
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

        void HandleDisconnect(IPEndPoint from)
        {
            if (registry != null && registry.TryFindId(from, out int id))
            {
                registry.Unregister(from);
                if (id >= 0 && id < readyById.Length)
                {
                    readyById[id] = false;
                    teamById[id] = 0;
                }

                if (bridge != null && bridge.paddles != null && id < bridge.paddles.Length && bridge.paddles[id] != null)
                {
                    bridge.paddles[id].SetPseudo("");
                    bridge.paddles[id].SetColorId(-1);
                    bridge.paddles[id].TeamIndex = -1;
                }

                if (PongGameManager.Instance != null)
                {
                    PongGameManager.Instance.AssignPaddlesToCircles();
                }

                Debug.Log($"[NetworkServer] client disconnected id={id} from {from}");
                BroadcastLobby();
            }
        }

        public void Shutdown()
        {
            if (transport != null && transport.IsOpen && registry != null)
            {
                byte[] disconnectMsg = Protocol.Encode(Protocol.BuildDisconnect());
                foreach (var ep in registry.Endpoints)
                {
                    if (ep != null)
                    {
                        transport.Send(disconnectMsg, ep);
                    }
                }
            }
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
            CheckHeartbeats();
            PingClients(Time.deltaTime);
        }

        /// <summary>
        /// Diffuse un ping best-effort à tous les clients à <see cref="clientPingRate"/> Hz : un signe
        /// de vie régulier de l'hôte que le client utilise pour détecter sa déconnexion (playerId -1,
        /// ignoré côté client : seul l'arrivée du paquet compte).
        /// </summary>
        void PingClients(float dt)
        {
            if (clientPingRate <= 0f) return;
            clientPingTimer += dt;
            float step = 1f / clientPingRate;
            if (clientPingTimer < step) return;
            clientPingTimer = 0f;

            byte[] ping = Protocol.Encode(Protocol.BuildHeartbeat(-1));
            foreach (var ep in registry.Endpoints)
                transport.Send(ping, ep);
        }

        /// <summary>
        /// Marque déconnecté tout joueur enregistré dont le dernier signe de vie (INPUT) dépasse le
        /// timeout, puis rediffuse le lobby une seule fois si au moins un statut a changé (la pastille
        /// passe au rouge chez les autres). Reconnexion gérée par <see cref="MarkSeen"/>.
        /// </summary>
        void CheckHeartbeats()
        {
            bool changed = false;
            foreach (int id in registry.Ids)
            {
                if (id < 0 || id >= ClientRegistry.MaxPlayers) continue;
                if (connectedById[id] && Time.time - lastSeenById[id] > heartbeatTimeout)
                {
                    connectedById[id] = false;
                    changed = true;
                    Debug.Log($"[NetworkServer] joueur id={id} déconnecté (aucun battement depuis {heartbeatTimeout}s).");
                }
            }
            if (changed) BroadcastLobby();
        }

        void Tick()
        {
            if (!match.Started || bridge == null) return;
            bridge.ApplyInput(pendingInput);
            state = bridge.BuildState(++tickSeq);

            // Tick rate adaptatif
            if (state.phase != GamePhase.Playing)
            {
                int reducedRate = state.phase == GamePhase.GameOver ? 1 : 5;
                if (tickSeq % (tickRate / reducedRate) != 0) return;
            }

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
