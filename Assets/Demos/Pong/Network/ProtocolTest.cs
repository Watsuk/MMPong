using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Test jetable des allers-retours du protocole (Parse(Build(x)) == x).
    /// Logge le bilan dans la console. À retirer une fois la couche réseau complète.
    /// </summary>
    public class ProtocolTest : MonoBehaviour
    {
        int ok, total;

        void Start()
        {
            // Input
            var (id, dir) = Protocol.ParseInput(Roundtrip(Protocol.BuildInput(1, -1f)));
            Check("Input", id == 1 && Mathf.Approximately(dir, -1f));

            // State (jeu circulaire, 2 joueurs)
            var g = new GameState
            {
                seq = 184,
                ballPos = new Vector2(2.31f, -0.5f),
                ballOwner = 1,
                paddleAngle = new[] { 37.5f, -128.0f },
                scores = new[] { 3, 1 },
                phase = GamePhase.GameOver,
                winner = 0,
                ballDir = new Vector2(0.707f, -0.707f),
                ballSpeed = 5.5f,
                bonusWinner = 2
            };
            var g2 = Protocol.ParseState(Roundtrip(Protocol.BuildState(g)));
            Check("State", g2.seq == 184
                && Mathf.Approximately(g2.ballPos.x, 2.31f)
                && Mathf.Approximately(g2.ballPos.y, -0.5f)
                && g2.ballOwner == 1
                && g2.paddleAngle.Length == 2
                && Mathf.Approximately(g2.paddleAngle[0], 37.5f)
                && Mathf.Approximately(g2.paddleAngle[1], -128.0f)
                && g2.scores[0] == 3 && g2.scores[1] == 1
                && g2.phase == GamePhase.GameOver
                && g2.winner == 0
                && g2.bonusWinner == 2
                && Mathf.Approximately(g2.ballDir.x, 0.707f)
                && Mathf.Approximately(g2.ballDir.y, -0.707f)
                && Mathf.Approximately(g2.ballSpeed, 5.5f));

            // Join + nettoyage des séparateurs + équipe
            var join = Protocol.ParseJoin(Roundtrip(Protocol.BuildJoin("ali|ce,bob", 1)));
            Check("Join (sanitize + team)", join.pseudo == "alicebob" && join.team == 1);

            // Lobby (pseudos + équipes + prêt positionnels)
            var lobbyMsg = Roundtrip(Protocol.BuildLobby(
                new[] { "alice", "bob", "charlie", "" },
                new[] { 0, 1, 0, 0 },
                new[] { true, false, true, false }));
            var lobby = Protocol.ParseLobby(lobbyMsg);
            Check("Lobby (pseudos)", lobby.Length == 4 && lobby[2] == "charlie");
            var lobbyDetail = Protocol.ParseLobbyDetailed(lobbyMsg);
            Check("Lobby (détaillé)", lobbyDetail.Length == 3
                && lobbyDetail[2].pseudo == "charlie" && lobbyDetail[2].team == 0 && lobbyDetail[2].ready
                && lobbyDetail[1].team == 1 && !lobbyDetail[1].ready);

            // Config (round-trip)
            var cfg = Protocol.ParseConfig(Roundtrip(Protocol.BuildConfig(new MatchSettings
            {
                maxPlayers = 4, winType = 1, targetPoints = 7, duration = 90f,
                teamAName = "Rouge", teamASkin = 0, teamBName = "Bleu", teamBSkin = 2
            })));
            Check("Config", cfg.maxPlayers == 4 && cfg.winType == 1 && cfg.targetPoints == 7
                && Mathf.Approximately(cfg.duration, 90f) && cfg.teamAName == "Rouge" && cfg.teamBSkin == 2);

            // Start (fiable, sans payload)
            var start = Roundtrip(Protocol.BuildStart());
            Check("Start", start.type == MessageType.Start && start.reliable);

            // SequenceGate (filtre anti-paquet-périmé)
            var gate = new SequenceGate();
            Check("Gate accepte premier seq", gate.Accept(1));
            Check("Gate rejette doublon", !gate.Accept(1));
            Check("Gate rejette périmé", !gate.Accept(0));
            Check("Gate accepte plus frais", gate.Accept(2));
            gate.Reset();
            Check("Gate accepte après Reset", gate.Accept(1));

            // DuplicateFilter (dédup exactly-once, indépendant de l'ordre)
            var dup = new DuplicateFilter();
            Check("Dup accepte premier", dup.IsNew(1));
            Check("Dup rejette doublon", !dup.IsNew(1));
            Check("Dup accepte autre seq", dup.IsNew(2));

            // ReliableChannel — émission : assigne un seq, ré-émet sans ACK, stoppe sur ACK
            var sent = new System.Collections.Generic.List<byte[]>();
            var chan = new ReliableChannel(b => sent.Add(b));
            chan.SendReliable(Protocol.BuildJoin("alice", 0));
            Check("Reliable émet une fois", sent.Count == 1);
            uint sentSeq = Protocol.Decode(sent[0]).seq;
            Check("Reliable assigne un seq", sentSeq >= 1);
            chan.Tick(0.25f);
            Check("Reliable ré-émet sans ACK", sent.Count == 2);
            chan.HandleAck(sentSeq);
            chan.Tick(0.25f);
            Check("Reliable stoppe après ACK", sent.Count == 2);

            // ReliableChannel — réception : renvoie un ACK puis déduplique
            var inbox = new System.Collections.Generic.List<byte[]>();
            var rx = new ReliableChannel(b => inbox.Add(b));
            var welcome = Protocol.BuildWelcome(2);
            welcome.seq = 7;
            bool fresh = rx.ReceiveReliable(welcome);
            Check("Reliable renvoie un ACK", inbox.Count == 1 && Protocol.Decode(inbox[0]).type == MessageType.Ack);
            Check("Reliable premier reçu = frais", fresh);
            Check("Reliable doublon ignoré", !rx.ReceiveReliable(welcome));

            // Ready (round-trip) + ReadyTracker (quota de joueurs prêts)
            var ready = Roundtrip(Protocol.BuildReady(2));
            Check("Ready", Protocol.ParseReady(ready) == 2 && ready.reliable);
            var tracker = new ReadyTracker();
            Check("ReadyTracker marque premier", tracker.MarkReady(0));
            Check("ReadyTracker ignore doublon", !tracker.MarkReady(0));
            Check("ReadyTracker marque autre", tracker.MarkReady(1));
            Check("ReadyTracker quota atteint", tracker.AllReady(2));
            Check("ReadyTracker quota non atteint", !tracker.AllReady(3));

            // ClientRegistry (bijection id↔endpoint↔pseudo + capacité)
            var epA = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 1);
            var epB = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 2);
            var reg = new ClientRegistry();
            Check("Registry attribue 0", reg.Register(epA, "a") == 0);
            Check("Registry attribue 1", reg.Register(epB, "b") == 1);
            Check("Registry retrouve l'id", reg.TryFindId(epB, out int foundB) && foundB == 1);
            string[] regPseudos = reg.Pseudos();
            Check("Registry pseudos positionnels",
                regPseudos.Length == ClientRegistry.MaxPlayers && regPseudos[0] == "a" && regPseudos[2] == "");
            var regFull = new ClientRegistry();
            for (int i = 0; i < ClientRegistry.MaxPlayers; i++)
                regFull.Register(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 100 + i), "p");
            Check("Registry refuse au-delà capacité",
                regFull.Register(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 200), "x") == -1);

            // MatchCoordinator (latch de démarrage unique)
            var coord = new MatchCoordinator(2);
            Check("Match pas démarré avant quota", !coord.TryStart(0));
            Check("Match démarre au quota", coord.TryStart(1));
            Check("Match ne redémarre pas", !coord.TryStart(2));
            Check("Match Started", coord.Started);
            var coordDup = new MatchCoordinator(2);
            coordDup.TryStart(0);
            Check("Match doublon n'atteint pas le quota", !coordDup.TryStart(0));

            // ReliableHub (fiabilité multi-pairs : dédup par pair, broadcast par endpoint)
            var ep1 = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11);
            var ep2 = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 12);
            var hubSent = new System.Collections.Generic.List<byte[]>();
            var hub = new ReliableHub((b, ep) => hubSent.Add(b));
            var w1 = Protocol.BuildWelcome(0); w1.seq = 5;
            var w2 = Protocol.BuildWelcome(1); w2.seq = 5;
            Check("Hub ep1 frais", hub.ReceiveReliable(ep1, w1));
            Check("Hub ep2 frais (même seq, autre pair)", hub.ReceiveReliable(ep2, w2));
            Check("Hub doublon même pair ignoré", !hub.ReceiveReliable(ep1, w1));
            hubSent.Clear();
            hub.BroadcastReliable(new[] { ep1, ep2 }, Protocol.BuildStart());
            Check("Hub broadcast = un envoi par pair", hubSent.Count == 2);

            if (ok == total) Debug.Log($"[ProtocolTest] {ok}/{total} OK");
            else Debug.LogError($"[ProtocolTest] {ok}/{total} OK — voir erreurs ci-dessus");
        }

        static Message Roundtrip(Message m) => Protocol.Decode(Protocol.Encode(m));

        void Check(string name, bool passed)
        {
            total++;
            if (passed) ok++;
            else Debug.LogError($"[ProtocolTest] {name} KO");
        }
    }
}
