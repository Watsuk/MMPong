using UnityEngine;
using System.Collections;

namespace MMPong
{
    public class BonusManager : MonoBehaviour
    {
        public static BonusManager Instance { get; private set; }

        public bool HasBonus { get; private set; }
        public int BonusCircleIndex { get; private set; }
        public float BonusAngle { get; private set; }

        /// <summary>
        /// playerId (index dans le tableau paddles) du dernier joueur à avoir ramassé un bonus.
        /// -1 = aucun pickup récent. Réinitialisé au spawn du bonus suivant.
        /// Sert à la synchronisation réseau (GameState.bonusWinner) et au feedback client.
        /// </summary>
        public int LastBonusWinner { get; private set; } = -1;

        /// <summary>
        /// playerIds des joueurs qui étaient dans la zone de collision mais ont perdu la course.
        /// Vide si aucun concurrent. Utilisé pour afficher un feedback "Trop tard !" côté client.
        /// </summary>
        public int[] LastBonusLosers { get; private set; } = System.Array.Empty<int>();

        [Header("SFX")]
        public AudioClip bonusSpawnClip;
        public AudioClip bonusPickupClip;

        private float spawnTimer = 2f; // Réduit à 2 secondes pour tester plus vite !
        private float despawnTimer = 5f;
        private GameObject bonusVisual;
        private AudioSource audioSource;

        // ========== Animation sprite (Poisonous Smoke) ==========
        /// <summary>Frames PNG chargées depuis Resources/BonusSmoke/ au démarrage.</summary>
        private Texture2D[] smokeFrames;
        /// <summary>Index de la frame courante dans le cycle d'animation.</summary>
        private int currentFrame;
        /// <summary>Timer interne pour cadencer le défilement des frames.</summary>
        private float frameTimer;
        /// <summary>Vitesse de l'animation en frames par seconde.</summary>
        private const float FramesPerSecond = 12f;
        /// <summary>Renderer du Quad portant le sprite (pour changer la texture).</summary>
        private Renderer bonusRenderer;

        void Awake()
        {
            if (Instance != null && Instance != this) Destroy(gameObject);
            else {
                Instance = this;
                Debug.Log("🟢 [BonusManager] Composant correctement attaché et actif ! Apparition du bonus dans 2 secondes...");
            }

            // Audio : crée un AudioSource s'il n'en existe pas
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // Génère des sons procéduraux par défaut si aucun clip n'est assigné
            if (bonusSpawnClip == null)
                bonusSpawnClip = GenerateBeep(660f, 0.12f, 0.3f);   // bip aigu et court
            if (bonusPickupClip == null)
                bonusPickupClip = GeneratePowerUp(0.25f, 0.4f);      // son ascendant de power-up

            // Charge les frames du sprite animé depuis Resources/BonusSmoke/
            LoadSmokeFrames();
        }

        /// <summary>Génère un bip sinusoïdal simple.</summary>
        static AudioClip GenerateBeep(float frequency, float duration, float volume = 0.4f)
        {
            int sr = 44100;
            int n = Mathf.CeilToInt(sr * duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = 1f - (t / duration);
                s[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * env;
            }
            AudioClip c = AudioClip.Create("bonus_beep", n, 1, sr, false);
            c.SetData(s, 0);
            return c;
        }

        /// <summary>Génère un son ascendant de power-up (fréquence qui monte).</summary>
        static AudioClip GeneratePowerUp(float duration, float volume = 0.4f)
        {
            int sr = 44100;
            int n = Mathf.CeilToInt(sr * duration);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float freq = Mathf.Lerp(400f, 1200f, t / duration); // monte de 400 Hz à 1200 Hz
                float env = 1f - (t / duration) * 0.5f; // fondu léger
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * volume * env;
            }
            AudioClip c = AudioClip.Create("bonus_powerup", n, 1, sr, false);
            c.SetData(s, 0);
            return c;
        }

        /// <summary>
        /// Charge les 12 frames du sprite « Poisonous Smoke » depuis le dossier
        /// Assets/Resources/BonusSmoke/ (smoke_01..smoke_12). Les fichiers doivent
        /// être des PNG importés comme Texture2D dans Unity (paramètre par défaut).
        /// </summary>
        void LoadSmokeFrames()
        {
            var frames = new System.Collections.Generic.List<Texture2D>();
            for (int i = 1; i <= 12; i++)
            {
                string path = $"BonusSmoke/smoke_{i:D2}";
                Texture2D tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    frames.Add(tex);
                else
                    Debug.LogWarning($"[BonusManager] Frame manquante : Resources/{path}");
            }

            smokeFrames = frames.ToArray();
            if (smokeFrames.Length > 0)
                Debug.Log($"🟢 [BonusManager] {smokeFrames.Length} frames smoke chargées.");
            else
                Debug.LogWarning("[BonusManager] Aucune frame smoke trouvée ! Le bonus sera invisible.");
        }

        // Called by ClientStateApplier (Client)
        public void SyncNetworkState(bool hasBonus, int circleIndex, float angle)
        {
            // Détecte les changements d'état pour jouer les sons côté client
            bool wasActive = HasBonus;
            HasBonus = hasBonus;
            BonusCircleIndex = circleIndex;
            BonusAngle = angle;

            if (!wasActive && hasBonus)
                PlayClip(bonusSpawnClip);    // le bonus vient d'apparaître
            else if (wasActive && !hasBonus)
                PlayClip(bonusPickupClip);   // le bonus vient d'être ramassé ou a disparu

            UpdateVisuals();
        }

        // Automatically runs every frame
        void Update()
        {
            // Ne pas exécuter la logique si on est un Client pur (pas de serveur mais un client présent).
            // Si on est en mode Local (ni serveur ni client) ou Host (serveur présent), on l'exécute !
            bool isClientOnly = FindAnyObjectByType<MMPong.Network.NetworkServer>() == null && 
                                FindAnyObjectByType<MMPong.Network.NetworkClient>() != null;
            
            if (isClientOnly)
            {
                // Le client ne gère pas la logique de spawn/collision,
                // mais affiche et anime quand même le bonus (via SyncNetworkState)
            }
            else
            {
                if (!HasBonus)
                {
                    spawnTimer -= Time.deltaTime;
                    if (spawnTimer <= 0f)
                    {
                        HasBonus = true;
                        LastBonusWinner = -1;   // reset : pas de pickup en cours
                        LastBonusLosers = System.Array.Empty<int>();
                        if (PongGameManager.Instance != null && PongGameManager.Instance.CircleRadii != null)
                        {
                            System.Collections.Generic.List<int> activeCircleIndices = new System.Collections.Generic.List<int>();
                            PongPaddle[] activePaddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);
                            foreach (var paddle in activePaddles)
                            {
                                int idx = PongGameManager.Instance.GetCircleIndex(paddle);
                                if (idx >= 0 && idx < PongGameManager.Instance.CircleRadii.Length && !activeCircleIndices.Contains(idx))
                                {
                                    activeCircleIndices.Add(idx);
                                }
                            }

                            if (activeCircleIndices.Count > 0)
                            {
                                BonusCircleIndex = activeCircleIndices[Random.Range(0, activeCircleIndices.Count)];
                            }
                            else
                            {
                                BonusCircleIndex = Random.Range(0, PongGameManager.Instance.CircleRadii.Length);
                            }
                        }
                        BonusAngle = Random.Range(0f, 360f);
                        despawnTimer = 5f;
                        PlayClip(bonusSpawnClip);
                        Debug.Log($"🟢 [BonusManager] SPAWN BONUS! Cercle: {BonusCircleIndex}, Angle: {BonusAngle}");
                    }
                }
                else
                {
                    despawnTimer -= Time.deltaTime;
                    if (despawnTimer <= 0f)
                    {
                        HasBonus = false;
                        spawnTimer = 2f;
                        Debug.Log("🔴 [BonusManager] Le bonus a disparu (timeout).");
                    }
                    else
                    {
                        CheckCollisions();
                    }
                }
            }

            // Visuels et animation pour tout le monde (client, host, local)
            UpdateVisuals();
            AnimateVisuals();
        }

        /// <summary>
        /// Animation du sprite : défile les frames à FramesPerSecond et fait tourner
        /// le Quad face à la caméra (billboard) pour qu'il soit toujours visible.
        /// </summary>
        void AnimateVisuals()
        {
            if (!HasBonus || bonusVisual == null || smokeFrames == null || smokeFrames.Length == 0)
                return;

            // Défilement des frames
            frameTimer += Time.deltaTime;
            float frameDuration = 1f / FramesPerSecond;
            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                currentFrame = (currentFrame + 1) % smokeFrames.Length;
                if (bonusRenderer != null)
                    bonusRenderer.material.mainTexture = smokeFrames[currentFrame];
            }

            // Billboard : le Quad fait toujours face à la caméra
            if (Camera.main != null)
            {
                bonusVisual.transform.rotation = Camera.main.transform.rotation;
            }

            // Pulsation douce de la taille
            float pulse = 3f + Mathf.Sin(Time.time * 3f) * 0.3f;
            bonusVisual.transform.localScale = new Vector3(pulse, pulse, pulse);
        }

        /// <summary>
        /// Détection de collision avec résolution de course (race condition) :
        /// au lieu de prendre le premier paddle trouvé dans la zone, on collecte
        /// TOUS les candidats et on choisit le plus proche (angleDiff minimal).
        /// Algorithme déterministe : même frame, même résultat, quel que soit
        /// l'ordre de FindObjectsByType.
        /// </summary>
        void CheckCollisions()
        {
            PongPaddle[] paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);

            // Phase 1 : collecter tous les paddles dans la zone de collision
            PongPaddle bestPaddle = null;
            float bestDiff = float.MaxValue;
            var candidates = new System.Collections.Generic.List<(PongPaddle paddle, float diff, int id)>();

            for (int i = 0; i < paddles.Length; i++)
            {
                var paddle = paddles[i];
                if (PongGameManager.Instance.GetCircleIndex(paddle) != BonusCircleIndex)
                    continue;

                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(paddle.CurrentAngle, BonusAngle));
                if (angleDiff < 15f) // seuil de collision (15° assez large pour un paddle rapide)
                {
                    int playerId = (int)paddle.Player - 1; // PongPlayer est 1-indexed
                    candidates.Add((paddle, angleDiff, playerId));

                    if (angleDiff < bestDiff)
                    {
                        bestDiff = angleDiff;
                        bestPaddle = paddle;
                    }
                }
            }

            // Phase 2 : résolution — le plus proche gagne
            if (bestPaddle != null)
            {
                int winnerId = (int)bestPaddle.Player - 1;
                HasBonus = false;
                spawnTimer = 10f;
                LastBonusWinner = winnerId;
                PlayClip(bonusPickupClip);
                StartCoroutine(ApplySpeedAdvantage(bestPaddle));

                // Identifier les perdants (dans la zone mais pas le gagnant)
                var losers = new System.Collections.Generic.List<int>();
                foreach (var (paddle, diff, id) in candidates)
                {
                    if (id != winnerId)
                        losers.Add(id);
                }
                LastBonusLosers = losers.ToArray();

                // Log détaillé de la résolution de la course
                if (candidates.Count > 1)
                {
                    string details = "";
                    foreach (var (paddle, diff, id) in candidates)
                    {
                        string tag = (id == winnerId) ? " ★ GAGNANT" : " ✗ perdant";
                        details += $"\n    Player {id + 1} (angleDiff={diff:F2}°){tag}";
                    }
                    Debug.Log($"⚡ [BonusManager] RACE CONDITION DÉTECTÉE — {candidates.Count} joueurs dans la zone !" +
                              $"\n  Résolution : le plus proche gagne (Player {winnerId + 1}, diff={bestDiff:F2}°)" +
                              details);
                }
                else
                {
                    Debug.Log($"🟡 [BonusManager] Bonus ramassé par Player {winnerId + 1} (angleDiff={bestDiff:F2}°, aucune contestation)");
                }
            }
        }

        IEnumerator ApplySpeedAdvantage(PongPaddle paddle)
        {
            float originalSpeed = paddle.Speed;
            paddle.Speed = originalSpeed * 4f;
            yield return new WaitForSeconds(7f);
            if (paddle != null)
            {
                paddle.Speed = originalSpeed; // Restore speed
            }
        }

        void UpdateVisuals()
        {
            if (HasBonus)
            {
                if (bonusVisual == null)
                {
                    // Crée un Quad (plan 2D) au lieu d'une sphère 3D
                    bonusVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    bonusVisual.name = "BonusSmoke";
                    Destroy(bonusVisual.GetComponent<Collider>()); // pas de collision physique
                    bonusVisual.transform.localScale = new Vector3(3f, 3f, 3f);

                    // Matériau transparent (sprites avec alpha)
                    bonusRenderer = bonusVisual.GetComponent<Renderer>();
                    if (bonusRenderer != null)
                    {
                        // Utilise un shader transparent compatible avec tous les pipelines
                        Material mat = new Material(Shader.Find("Sprites/Default"));
                        mat.color = Color.white;
                        bonusRenderer.material = mat;

                        // Applique la première frame si disponible
                        if (smokeFrames != null && smokeFrames.Length > 0)
                        {
                            currentFrame = 0;
                            frameTimer = 0f;
                            mat.mainTexture = smokeFrames[0];
                        }
                    }
                }
                bonusVisual.SetActive(true);

                if (PongGameManager.Instance != null && PongGameManager.Instance.CircleRadii != null && BonusCircleIndex < PongGameManager.Instance.CircleRadii.Length)
                {
                    float radius = PongGameManager.Instance.CircleRadii[BonusCircleIndex];
                    float rad = BonusAngle * Mathf.Deg2Rad;
                    bonusVisual.transform.position = PongGameManager.Instance.CenterPoint + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
                }
            }
            else
            {
                if (bonusVisual != null)
                {
                    bonusVisual.SetActive(false);
                }
            }
        }

        bool IsPaddleActivePlayer(PongPaddle paddle)
        {
            if (paddle == null) return false;

            // 1. En mode réseau (Host/Server présent)
            var server = FindAnyObjectByType<MMPong.Network.NetworkServer>();
            if (server != null)
            {
                return (int)paddle.Player <= server.expectedPlayers;
            }

            // 2. En mode local (pas de serveur)
            if (PongGameManager.Instance != null)
            {
                return (int)paddle.Player <= PongGameManager.Instance.totalPlayers;
            }

            return true;
        }

        void PlayClip(AudioClip clip)
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip);
        }
    }
}
