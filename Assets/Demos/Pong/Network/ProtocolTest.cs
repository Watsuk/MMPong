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
                winner = 0
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
                && g2.winner == 0);

            // Join + nettoyage des séparateurs
            var join = Protocol.ParseJoin(Roundtrip(Protocol.BuildJoin("ali|ce,bob")));
            Check("Join (sanitize)", join == "alicebob");

            // Lobby
            var lobby = Protocol.ParseLobby(Roundtrip(Protocol.BuildLobby(new[] { "alice", "bob", "charlie" })));
            Check("Lobby", lobby.Length == 3 && lobby[2] == "charlie");

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
            chan.SendReliable(Protocol.BuildJoin("alice"));
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
