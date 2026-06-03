using System.Linq;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Affiche l'état reçu du serveur côté client : positionne les paddles (depuis leur angle)
    /// et la balle, <b>sans aucune simulation locale</b>. Branché sur
    /// <see cref="NetworkClient.OnStateReceived"/>. Suppose les paddles/balle en RemoteDisplay.
    /// </summary>
    [RequireComponent(typeof(NetworkClient))]
    public class ClientStateApplier : MonoBehaviour
    {
        NetworkClient client;
        PongPaddle[] paddles;
        PongBall ball;

        void Awake()
        {
            client = GetComponent<NetworkClient>();
            paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None)
                .OrderBy(p => (int)p.Player)
                .ToArray();
            ball = FindFirstObjectByType<PongBall>();
        }

        void OnEnable() => client.OnStateReceived += Apply;
        void OnDisable() => client.OnStateReceived -= Apply;

        void Apply(GameState s)
        {
            int n = Mathf.Min(paddles.Length, s.paddleAngle.Length);
            for (int i = 0; i < n; i++)
                paddles[i].ApplyNetworkAngle(s.paddleAngle[i]);

            if (ball != null)
                ball.transform.position = new Vector3(s.ballPos.x, s.ballPos.y, 0f);

            if (BonusManager.Instance != null)
                BonusManager.Instance.SyncNetworkState(s.hasBonus, s.bonusCircleIndex, s.bonusAngle);
        }
    }
}
