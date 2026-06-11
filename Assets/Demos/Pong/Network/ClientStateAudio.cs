using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Joue des SFX côté client à partir des snapshots serveur, ou de la simulation locale
    /// quand aucun NetworkClient n'est présent.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ClientStateAudio : MonoBehaviour
    {
        [Header("Clips")]
        public AudioClip leftHitClip;
        public AudioClip rightHitClip;
        public AudioClip hitClip;
        public AudioClip scoreClip;
        public AudioClip leftScoreClip;
        public AudioClip rightScoreClip;
        public AudioClip gameOverClip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Header("Camera Shake")]
        [Tooltip("Intensité du shake lors d'un impact paddle (même valeur pour tous les clients)")]
        public float paddleShakeIntensity = 0.12f;
        [Tooltip("Durée du shake lors d'un impact paddle")]
        public float paddleShakeDuration = 0.1f;
        [Tooltip("Intensité du shake lors d'un but")]
        public float scoreShakeIntensity = 0.25f;
        [Tooltip("Durée du shake lors d'un but")]
        public float scoreShakeDuration = 0.2f;

        NetworkClient client;
        PongBall localBall;
        AudioSource audioSource;

        bool hasState;
        bool receivedNetworkState;
        int lastBallOwner;
        int[] lastScores;
        GamePhase lastPhase;

        void Awake()
        {
            client = GetComponent<NetworkClient>();
            localBall = GetComponent<PongBall>();
            if (localBall == null)
                localBall = FindFirstObjectByType<PongBall>();
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        void OnEnable()
        {
            if (client != null)
            {
                client.OnStateReceived += OnState;
                client.OnGameEnded += OnGameEnded;
            }
        }

        void OnDisable()
        {
            if (client != null)
            {
                client.OnStateReceived -= OnState;
                client.OnGameEnded -= OnGameEnded;
            }
        }

        void Update()
        {
            if ((client == null || !receivedNetworkState) && localBall != null)
                PollLocalBall();
        }

        void PollLocalBall()
        {
            var s = new GameState
            {
                ballOwner = localBall.LastHitter,
                scores = new[] { localBall.scoreLeft, localBall.scoreRight },
                phase = MapLocalPhase(localBall.State)
            };

            if (!hasState)
            {
                CacheState(s);
                return;
            }

            if (s.ballOwner != lastBallOwner && s.ballOwner >= 0)
            {
                PlaySideHitClip(s.ballOwner);
                if (CameraShaker.Instance != null)
                    CameraShaker.Instance.Shake(paddleShakeIntensity, paddleShakeDuration);
            }

            if (ScoresChanged(s.scores, lastScores))
            {
                if (ScoreIncreased(s.scores, lastScores, 0))
                    PlayScoreClip(leftScoreClip);

                if (ScoreIncreased(s.scores, lastScores, 1))
                    PlayScoreClip(rightScoreClip);

                if (leftScoreClip == null && rightScoreClip == null)
                    PlayScoreClip(scoreClip);

                if (CameraShaker.Instance != null)
                    CameraShaker.Instance.Shake(scoreShakeIntensity, scoreShakeDuration);
            }

            if (gameOverClip != null && s.phase == GamePhase.GameOver && lastPhase != GamePhase.GameOver)
                audioSource.PlayOneShot(gameOverClip, volume);

            CacheState(s);
        }

        void OnState(GameState s)
        {
            receivedNetworkState = true;

            if (!hasState)
            {
                CacheState(s);
                return;
            }

            if (s.ballOwner != lastBallOwner && s.ballOwner >= 0)
            {
                PlaySideHitClip(s.ballOwner);
                if (CameraShaker.Instance != null)
                    CameraShaker.Instance.Shake(paddleShakeIntensity, paddleShakeDuration);
            }

            if (ScoresChanged(s.scores, lastScores))
            {
                if (ScoreIncreased(s.scores, lastScores, 0))
                    PlayScoreClip(leftScoreClip);

                if (ScoreIncreased(s.scores, lastScores, 1))
                    PlayScoreClip(rightScoreClip);

                if (leftScoreClip == null && rightScoreClip == null)
                    PlayScoreClip(scoreClip);

                if (CameraShaker.Instance != null)
                    CameraShaker.Instance.Shake(scoreShakeIntensity, scoreShakeDuration);
            }

            if (gameOverClip != null && s.phase == GamePhase.GameOver && lastPhase != GamePhase.GameOver)
                audioSource.PlayOneShot(gameOverClip, volume);

            CacheState(s);
        }

        void OnGameEnded(int winner)
        {
            if (gameOverClip != null && lastPhase != GamePhase.GameOver)
                audioSource.PlayOneShot(gameOverClip, volume);
            lastPhase = GamePhase.GameOver;
        }

        void CacheState(GameState s)
        {
            lastBallOwner = s.ballOwner;
            lastPhase = s.phase;
            CacheScores(s.scores);
            hasState = true;
        }

        static GamePhase MapLocalPhase(PongBallState state)
        {
            switch (state)
            {
                case PongBallState.PlayerLeftWin:
                case PongBallState.PlayerRightWin:
                    return GamePhase.GameOver;
                case PongBallState.Playing:
                    return GamePhase.Playing;
                case PongBallState.WaitingForServe:
                    return GamePhase.WaitingForServe;
                default:
                    return GamePhase.WaitingForServe;
            }
        }

        static bool ScoresChanged(int[] current, int[] previous)
        {
            if (current == null && previous == null) return false;
            if (current == null || previous == null) return true;
            int n = Mathf.Min(current.Length, previous.Length);
            for (int i = 0; i < n; i++)
                if (current[i] != previous[i]) return true;
            return current.Length != previous.Length;
        }

        static bool ScoreIncreased(int[] current, int[] previous, int index)
        {
            if (current == null || previous == null) return false;
            if (index < 0 || index >= current.Length || index >= previous.Length) return false;
            return current[index] > previous[index];
        }

        void PlaySideHitClip(int playerId)
        {
            AudioClip clip = null;

            if (playerId == 0)
                clip = leftHitClip;
            else if (playerId == 1)
                clip = rightHitClip;

            if (clip == null)
                clip = hitClip;

            if (clip != null)
                audioSource.PlayOneShot(clip, volume);
        }

        void PlayScoreClip(AudioClip clip)
        {
            if (clip == null)
                clip = scoreClip;

            if (clip != null)
                audioSource.PlayOneShot(clip, volume);
        }

        void CacheScores(int[] scores)
        {
            if (scores == null)
            {
                lastScores = null;
                return;
            }

            if (lastScores == null || lastScores.Length != scores.Length)
                lastScores = new int[scores.Length];

            for (int i = 0; i < scores.Length; i++)
                lastScores[i] = scores[i];
        }
    }
}
