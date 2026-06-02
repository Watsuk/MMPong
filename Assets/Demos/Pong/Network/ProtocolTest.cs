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

            // State
            var g = new GameState
            {
                seq = 184,
                ballPos = new Vector2(2.31f, -0.5f),
                paddleY = new[] { 1.2f, -3.0f, 0f, 2.5f },
                scores = new[] { 3, 1, 0, 2 }
            };
            var g2 = Protocol.ParseState(Roundtrip(Protocol.BuildState(g)));
            Check("State", g2.seq == 184
                && Mathf.Approximately(g2.ballPos.x, 2.31f)
                && Mathf.Approximately(g2.ballPos.y, -0.5f)
                && Mathf.Approximately(g2.paddleY[1], -3.0f)
                && g2.scores[0] == 3 && g2.scores[3] == 2);

            // Join + nettoyage des séparateurs
            var join = Protocol.ParseJoin(Roundtrip(Protocol.BuildJoin("ali|ce,bob")));
            Check("Join (sanitize)", join == "alicebob");

            // Lobby
            var lobby = Protocol.ParseLobby(Roundtrip(Protocol.BuildLobby(new[] { "alice", "bob", "charlie" })));
            Check("Lobby", lobby.Length == 3 && lobby[2] == "charlie");

            // Start (fiable, sans payload)
            var start = Roundtrip(Protocol.BuildStart());
            Check("Start", start.type == MessageType.Start && start.reliable);

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
