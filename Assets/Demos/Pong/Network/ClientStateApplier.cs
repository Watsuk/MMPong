using System.Linq;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Affiche l'état reçu du serveur côté client avec <b>interpolation linéaire</b>
    /// (Snapshot Interpolation) : stocke les deux derniers snapshots horodatés et
    /// interpole dans <c>Update()</c> pour un rendu fluide même si le serveur n'envoie
    /// que 30 ticks/s. Les valeurs discrètes (ballOwner, bonus) sont appliquées
    /// immédiatement à la réception, seules les positions continues (balle, paddles)
    /// sont lissées.
    ///
    /// <b>Algorithme :</b>
    /// À chaque réception d'un snapshot, l'ancien « cible » devient « précédent ».
    /// Dans Update(), on calcule <c>t = (now - tPrev) / (tTarget - tPrev)</c> borné
    /// à [0, 1], puis on applique <c>Vector2.Lerp</c> sur la balle et
    /// <c>Mathf.LerpAngle</c> sur chaque paddle. Quand <c>t</c> atteint 1 le client
    /// reste sur la dernière position connue sans extrapoler, ce qui évite les
    /// artefacts visuels en cas de perte de paquet.
    /// </summary>
    [RequireComponent(typeof(NetworkClient))]
    public class ClientStateApplier : MonoBehaviour
    {
        /// <summary>Snapshot horodaté pour l'interpolation entre deux états.</summary>
        struct TimestampedState
        {
            public GameState state;
            public float time;
        }

        NetworkClient client;
        PongPaddle[] paddles;
        PongBall ball;

        /// <summary>Avant-dernier snapshot reçu (point de départ de l'interpolation).</summary>
        TimestampedState previous;
        /// <summary>Dernier snapshot reçu (point d'arrivée de l'interpolation).</summary>
        TimestampedState target;
        bool hasTarget;
        bool hasPrevious;

        void Awake()
        {
            client = GetComponent<NetworkClient>();
            paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None)
                .OrderBy(p => (int)p.Player)
                .ToArray();
            ball = FindFirstObjectByType<PongBall>();
        }

        void OnEnable() => client.OnStateReceived += OnStateReceived;
        void OnDisable() => client.OnStateReceived -= OnStateReceived;

        /// <summary>
        /// Callback réseau : décale l'état cible vers le précédent et stocke le nouveau
        /// snapshot avec son horodatage. Les valeurs discrètes (couleur de la balle,
        /// état du bonus) sont appliquées immédiatement car elles ne se prêtent pas
        /// à l'interpolation.
        /// </summary>
        void OnStateReceived(GameState s)
        {
            // Décalage : l'ancien cible devient le précédent
            if (hasTarget)
            {
                previous = target;
                hasPrevious = true;
            }

            target = new TimestampedState { state = s, time = Time.time };
            hasTarget = true;

            // Valeurs discrètes : appliquées immédiatement (pas interpolables)
            if (ball != null)
                ball.ApplyVisualState(s.ballOwner);

            if (BonusManager.Instance != null)
                BonusManager.Instance.SyncNetworkState(s.hasBonus, s.bonusCircleIndex, s.bonusAngle);
        }

        /// <summary>
        /// Chaque frame Unity : interpole les positions continues entre l'avant-dernier
        /// et le dernier snapshot reçu. Le facteur t est borné à [0,1] pour ne jamais
        /// extrapoler au-delà du dernier état connu.
        /// </summary>
        void Update()
        {
            if (!hasTarget) return;

            // Premier snapshot : pas encore de paire → on applique directement
            if (!hasPrevious)
            {
                ApplyDirect(target.state);
                return;
            }

            // Durée entre les deux derniers snapshots (≈ 1/tickRate du serveur)
            float duration = target.time - previous.time;
            if (duration <= 0f)
            {
                ApplyDirect(target.state);
                return;
            }

            // Facteur d'interpolation : 0 = position précédente, 1 = position cible
            float elapsed = Time.time - previous.time;
            float t = Mathf.Clamp01(elapsed / duration);

            // Interpolation des angles des paddles (LerpAngle gère le wrap 0↔360)
            int paddleCountPrev = previous.state.paddleAngle != null ? previous.state.paddleAngle.Length : 0;
            int paddleCountTarget = target.state.paddleAngle != null ? target.state.paddleAngle.Length : 0;
            int n = Mathf.Min(paddles.Length, Mathf.Min(paddleCountPrev, paddleCountTarget));
            for (int i = 0; i < n; i++)
            {
                float angle = Mathf.LerpAngle(
                    previous.state.paddleAngle[i],
                    target.state.paddleAngle[i],
                    t);
                paddles[i].ApplyNetworkAngle(angle);
            }

            // Interpolation de la position de la balle (Lerp linéaire)
            if (ball != null)
            {
                Vector2 pos = Vector2.Lerp(previous.state.ballPos, target.state.ballPos, t);
                ball.transform.position = new Vector3(pos.x, pos.y, 0f);
            }
        }

        /// <summary>Applique un état directement sans interpolation (premier snapshot reçu).</summary>
        void ApplyDirect(GameState s)
        {
            int n = Mathf.Min(paddles.Length, s.paddleAngle != null ? s.paddleAngle.Length : 0);
            for (int i = 0; i < n; i++)
                paddles[i].ApplyNetworkAngle(s.paddleAngle[i]);

            if (ball != null)
                ball.transform.position = new Vector3(s.ballPos.x, s.ballPos.y, 0f);
        }
    }
}
