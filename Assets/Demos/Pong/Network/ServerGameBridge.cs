using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Colle entre le serveur réseau et le jeu. Applique l'input réseau aux vrais paddles
    /// et lit la simulation (<see cref="PongPaddle"/>/<see cref="PongBall"/>) pour produire le
    /// <see cref="GameState"/> autoritatif. <see cref="NetworkServer"/> reste générique : toute
    /// la connaissance du Pong vit ici.
    /// </summary>
    public class ServerGameBridge : MonoBehaviour
    {
        public PongPaddle[] paddles;   // index = playerId (0 = Left, 1 = Right)
        public PongBall ball;

        void Start()
        {
            if (paddles != null)
                foreach (var p in paddles)
                    if (p != null) p.DrivenExternally = true;
        }

        void Update()
        {
            if (BonusManager.Instance != null)
                BonusManager.Instance.ServerUpdate();
        }

        /// <summary>Applique la dernière intention reçue de chaque joueur à son paddle.</summary>
        public void ApplyInput(float[] pendingInput)
        {
            if (paddles == null) return;
            int n = Mathf.Min(paddles.Length, pendingInput.Length);
            for (int i = 0; i < n; i++)
                if (paddles[i] != null) paddles[i].ExternalDirection = pendingInput[i];
        }

        /// <summary>Construit l'état autoritatif courant à partir de la vraie simulation.</summary>
        public GameState BuildState(uint seq)
        {
            int n = paddles != null ? paddles.Length : 0;
            var angles = new float[n];
            for (int i = 0; i < n; i++)
                angles[i] = paddles[i] != null ? paddles[i].CurrentAngle : 0f;

            var (phase, winner) = MapPhase(ball != null ? ball.State : PongBallState.Playing);

            return new GameState
            {
                seq = seq,
                paddleAngle = angles,
                ballPos = ball != null ? (Vector2)ball.transform.position : Vector2.zero,
                ballOwner = ball != null ? ball.LastHitter : -1,
                scores = new[] { ball != null ? ball.scoreLeft : 0, ball != null ? ball.scoreRight : 0 },
                phase = phase,
                winner = winner,
                hasBonus = BonusManager.Instance != null && BonusManager.Instance.HasBonus,
                bonusCircleIndex = BonusManager.Instance != null ? BonusManager.Instance.BonusCircleIndex : 0,
                bonusAngle = BonusManager.Instance != null ? BonusManager.Instance.BonusAngle : 0f
            };
        }

        static (GamePhase phase, int winner) MapPhase(PongBallState s)
        {
            switch (s)
            {
                case PongBallState.WaitingForServe: return (GamePhase.WaitingForServe, -1);
                case PongBallState.Playing:         return (GamePhase.Playing, -1);
                case PongBallState.PlayerLeftWin:   return (GamePhase.GameOver, 0);
                case PongBallState.PlayerRightWin:  return (GamePhase.GameOver, 1);
                default:                            return (GamePhase.Playing, -1);
            }
        }
    }
}
