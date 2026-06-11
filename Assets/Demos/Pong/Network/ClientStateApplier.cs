using System.Linq;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Affiche l'état reçu du serveur côté client avec <b>interpolation linéaire</b>
    /// (Snapshot Interpolation) et <b>prédiction client</b> (Client-Side Prediction).
    ///
    /// <b>Interpolation :</b>
    /// Stocke les deux derniers snapshots horodatés et interpole dans Update()
    /// pour un rendu fluide même si le serveur n'envoie que 30 ticks/s.
    /// Les valeurs discrètes (ballOwner, bonus) sont appliquées immédiatement.
    ///
    /// <b>Prédiction client :</b>
    /// Le paddle du joueur local est exclu de l'interpolation réseau : il se
    /// déplace immédiatement en réponse aux inputs clavier dans son propre
    /// <c>PongPaddle.Update()</c>, sans attendre la confirmation du serveur.
    /// Cela supprime l'input lag (≈ 1 RTT, soit 50-150ms sur Internet).
    ///
    /// <b>Réconciliation serveur :</b>
    /// Le serveur reste autoritatif. Si l'angle prédit localement diverge de
    /// plus de <see cref="ReconcileThreshold"/> degrés par rapport à l'état
    /// serveur, le paddle est doucement corrigé via un Lerp à vitesse
    /// <see cref="ReconcileSpeed"/>, évitant un snap visuel brutal.
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

        /// <summary>
        /// Seuil de tolérance (degrés) au-delà duquel le serveur corrige la prédiction
        /// locale. En dessous, la prédiction est considérée suffisamment juste.
        /// </summary>
        const float ReconcileThreshold = 5f;

        /// <summary>
        /// Vitesse de correction douce par frame (0 = pas de correction, 1 = snap immédiat).
        /// Une valeur basse (0.15) donne une correction progressive et invisible.
        /// </summary>
        const float ReconcileSpeed = 0.15f;

        [Header("Ball Prediction")]
        [Tooltip("Active la prédiction client de la trajectoire de la balle (Extrapolating/Dead Reckoning) à la place de l'interpolation simple.")]
        public bool predictBall = true;

        [Tooltip("Vitesse de lissage de la position prédite de la balle.")]
        public float ballPredictLerpSpeed = 15f;

        [Tooltip("Seuil de distance au-delà duquel la balle se téléporte directement à la position prédite.")]
        public float ballSnapThreshold = 2f;

        NetworkClient client;
        PongPaddle[] paddles;
        PongBall ball;
        PongScore scoreDisplay;

        /// <summary>Avant-dernier snapshot reçu (point de départ de l'interpolation).</summary>
        TimestampedState previous;
        /// <summary>Dernier snapshot reçu (point d'arrivée de l'interpolation).</summary>
        TimestampedState target;
        bool hasTarget;
        bool hasPrevious;

        /// <summary>Dernier bonusWinner vu, pour détecter les transitions (pickup events).</summary>
        int lastSeenBonusWinner = -1;

        void Awake()
        {
            client = GetComponent<NetworkClient>();
            paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None)
                .OrderBy(p => (int)p.Player)
                .ToArray();
            ball = FindFirstObjectByType<PongBall>();
            scoreDisplay = FindFirstObjectByType<PongScore>();
        }

        public void RefreshPaddles()
        {
            paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None)
                .OrderBy(p => (int)p.Player)
                .ToArray();
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
            {
                ball.ApplyVisualState(s.ballOwner);
                ball.BallDirection = s.ballDir;
                ball.BallSpeed = s.ballSpeed;
            }

            // Synchronise les scores affichés depuis l'état serveur
            if (scoreDisplay != null && s.scores != null && s.scores.Length >= 2)
                scoreDisplay.SetScores(s.scores[0], s.scores[1]);

            if (BonusManager.Instance != null)
                BonusManager.Instance.SyncNetworkState(s.hasBonus, s.bonusCircleIndex, s.bonusAngle);

            // Feedback visuel de résolution de course (race condition) sur le bonus.
            // Détecte la transition bonusWinner : -1 → playerId = un pickup vient d'avoir lieu.
            if (s.bonusWinner >= 0 && s.bonusWinner != lastSeenBonusWinner)
            {
                // Flash vert sur le gagnant
                if (s.bonusWinner < paddles.Length && paddles[s.bonusWinner] != null)
                    paddles[s.bonusWinner].FlashColor(Color.green, 0.6f, 3);

                // Flash rouge sur les autres paddles (les "perdants potentiels")
                // Tous les joueurs voient qui a gagné le bonus
                for (int i = 0; i < paddles.Length; i++)
                {
                    if (i != s.bonusWinner && paddles[i] != null)
                        paddles[i].FlashColor(Color.red, 0.4f, 2);
                }

                Debug.Log($"[ClientStateApplier] Bonus ramassé par Player {s.bonusWinner + 1} (feedback visuel)");
            }
            lastSeenBonusWinner = s.bonusWinner;
        }

        /// <summary>
        /// Chaque frame Unity : interpole les positions continues des entités distantes
        /// et réconcilie le paddle local si nécessaire.
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

            int localId = client.PlayerId;

            // Interpolation des angles des paddles (LerpAngle gère le wrap 0↔360)
            int paddleCountPrev = previous.state.paddleAngle != null ? previous.state.paddleAngle.Length : 0;
            int paddleCountTarget = target.state.paddleAngle != null ? target.state.paddleAngle.Length : 0;
            int n = Mathf.Min(paddles.Length, Mathf.Min(paddleCountPrev, paddleCountTarget));
            for (int i = 0; i < n; i++)
            {
                // Prédiction client : le paddle local se déplace seul via PongPaddle.Update(),
                // on ne l'écrase pas → suppression de l'input lag.
                if (i == localId)
                {
                    Reconcile(i);
                    continue;
                }

                float angle = Mathf.LerpAngle(
                    previous.state.paddleAngle[i],
                    target.state.paddleAngle[i],
                    t);
                paddles[i].ApplyNetworkAngle(angle);
            }

            // Interpolation ou prédiction de la position de la balle
            if (ball != null)
            {
                if (predictBall)
                {
                    // Temps écoulé depuis la réception du dernier snapshot
                    float elapsedSinceTarget = Time.time - target.time;

                    // Extrapoler la position à partir du dernier état connu
                    Vector2 predictedPos = target.state.ballPos + target.state.ballDir * target.state.ballSpeed * elapsedSinceTarget;

                    // Lisser le rendu visuel
                    float currentDist = Vector3.Distance(ball.transform.position, predictedPos);
                    if (currentDist > ballSnapThreshold)
                    {
                        // Snap direct si l'erreur est trop grande (ex: téléportation au score)
                        ball.transform.position = new Vector3(predictedPos.x, predictedPos.y, 0f);
                    }
                    else
                    {
                        // Interpolation douce vers la position prédite
                        ball.transform.position = Vector3.Lerp(
                            ball.transform.position,
                            new Vector3(predictedPos.x, predictedPos.y, 0f),
                            ballPredictLerpSpeed * Time.deltaTime);
                    }
                }
                else
                {
                    // Rendu classique par interpolation linéaire entre previous et target
                    Vector2 pos = Vector2.Lerp(previous.state.ballPos, target.state.ballPos, t);
                    ball.transform.position = new Vector3(pos.x, pos.y, 0f);
                }
            }
        }

        /// <summary>
        /// Réconciliation serveur : si la position prédite du paddle local diverge de
        /// plus de <see cref="ReconcileThreshold"/> degrés par rapport à l'état autoritatif
        /// du serveur, on corrige doucement via un LerpAngle. En dessous du seuil, la
        /// prédiction est considérée juste et le paddle n'est pas touché.
        /// </summary>
        void Reconcile(int localIndex)
        {
            if (target.state.paddleAngle == null || localIndex >= target.state.paddleAngle.Length) return;
            if (localIndex >= paddles.Length || paddles[localIndex] == null) return;

            float serverAngle = target.state.paddleAngle[localIndex];
            float localAngle = paddles[localIndex].CurrentAngle;
            float diff = Mathf.Abs(Mathf.DeltaAngle(localAngle, serverAngle));

            if (diff > ReconcileThreshold)
            {
                // Correction progressive : on ramène doucement le paddle vers le serveur
                float corrected = Mathf.LerpAngle(localAngle, serverAngle, ReconcileSpeed);
                paddles[localIndex].ApplyNetworkAngle(corrected);
            }
        }

        /// <summary>Applique un état directement sans interpolation (premier snapshot reçu).</summary>
        void ApplyDirect(GameState s)
        {
            int localId = client.PlayerId;
            int n = Mathf.Min(paddles.Length, s.paddleAngle != null ? s.paddleAngle.Length : 0);
            for (int i = 0; i < n; i++)
            {
                // Même en mode direct, on ne touche pas au paddle local (prédiction)
                if (i == localId) continue;
                paddles[i].ApplyNetworkAngle(s.paddleAngle[i]);
            }

            if (ball != null)
                ball.transform.position = new Vector3(s.ballPos.x, s.ballPos.y, 0f);
        }
    }
}
