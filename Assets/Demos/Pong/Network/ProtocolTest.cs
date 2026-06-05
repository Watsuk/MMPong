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
