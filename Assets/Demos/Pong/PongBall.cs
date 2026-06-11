using UnityEngine;

public enum PongBallState {
  Playing = 0,
  PlayerLeftWin = 1,
  PlayerRightWin = 2,
  WaitingForServe = 3,
  StartMenu = 4
}

public class PongBall : MonoBehaviour
{
    public Color blue = Color.blue;
    public Color red = Color.red;

    public Texture waterTexture;
    public Texture fireTexture;

    [Header("SFX")]
    public AudioClip wallBounceClip;

    private Renderer balleRenderer;
    private TrailRenderer trail;
    private AudioSource audioSource;
    public float Speed = 1;
    private float BaseSpeed;

    Vector3 Direction;
    PongBallState _State = PongBallState.StartMenu;

    public int scoreLeft = 0;
    public int scoreRight = 0;
    public int winScore = 5;

    // Affichage distant (client) : quand true, la balle ne simule pas (position fixée par le
    // serveur). Inerte par défaut → jeu local non affecté.
    public bool RemoteDisplay = false;

    public Vector3 BallDirection {
        get => Direction;
        set => Direction = value.normalized;
    }

    public float BallSpeed {
        get => Speed;
        set => Speed = value;
    }

    public PongBallState State {
      get {
        return _State;
      }
    }

    public PongScore scoreDisplay;

    /// <summary>Dernier joueur ayant touché la balle (0 = Left, 1 = Right, -1 = aucun). Lu par le serveur.</summary>
    public int LastHitter => hasTouched
        ? lastTouchedTeam
        : -1;

    void Start() {
      BaseSpeed = Speed;
      balleRenderer = GetComponent<Renderer>();

      // Audio
      audioSource = GetComponent<AudioSource>();
      if (audioSource == null)
          audioSource = gameObject.AddComponent<AudioSource>();
      audioSource.playOnAwake = false;

      // Génère un bip procédural par défaut pour le rebond mur si aucun clip n'est assigné
      if (wallBounceClip == null)
          wallBounceClip = GenerateBeep(220f, 0.08f);  // bip grave et court

      // Récupère un TrailRenderer existant ou en crée un automatiquement
      trail = GetComponent<TrailRenderer>();
      if (trail == null)
          trail = gameObject.AddComponent<TrailRenderer>();

      // Configuration du Trail Renderer par code (pas besoin de le faire manuellement dans Unity)
      trail.time = 0.3f;          // Durée de vie de la traînée en secondes
      trail.widthMultiplier = 0.6f; // Largeur de base de la traînée

      // Courbe de largeur : large à la tête, zéro à la queue
      AnimationCurve widthCurve = new AnimationCurve(
          new Keyframe(0f, 1f),   // début (tête) : largeur pleine
          new Keyframe(1f, 0f)    // fin (queue) : s'efface
      );
      trail.widthCurve = widthCurve;

      // Matériau compatible avec tous les pipelines de rendu (URP, HDRP, Built-in)
      trail.material = new Material(Shader.Find("Sprites/Default"));

      // Couleur initiale : blanc → transparent
      trail.startColor = Color.white;
      trail.endColor = new Color(1f, 1f, 1f, 0f);
      trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

      if (scoreDisplay == null)
      {
          GameObject scoreManager = GameObject.Find("ScoreManager");
          if (scoreManager != null)
          {
              scoreDisplay = scoreManager.GetComponent<PongScore>();
          }
          if (scoreDisplay == null)
          {
              scoreDisplay = FindFirstObjectByType<PongScore>();
          }
      }
      ResetBall(false); // Do not launch immediately
    }

    /// <summary>Génère un AudioClip mono synthétique (onde sinusoïdale).</summary>
    static AudioClip GenerateBeep(float frequency, float duration, float volume = 0.4f)
    {
        int sampleRate = 44100;
        int numSamples = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[numSamples];
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duration); // fondu linéaire
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
        }
        AudioClip clip = AudioClip.Create("beep_" + frequency, numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private PongPlayer lastTouchedPlayer;
    private int lastTouchedTeam = -1;
    private bool hasTouched = false;

    public void SetStateToMenu() {
        _State = PongBallState.StartMenu;
    }

    public void StartGameFromMenu() {
        _State = PongBallState.Playing;
    }

    /// <summary>Met à jour l'état visuel (texture et couleur de la traînée) selon la couleur de l'équipe (0 = Left/Bleu, 1 = Right/Rouge, -1 = aucun).</summary>
    public void ApplyVisualState(int team)
    {
        if (balleRenderer == null)
            balleRenderer = GetComponent<Renderer>();
        if (trail == null)
            trail = GetComponent<TrailRenderer>();

        int colorId = -1;
        if (team >= 0)
        {
            PongPaddle[] paddles = FindObjectsByType<PongPaddle>();
            foreach (var p in paddles)
            {
                if (p != null && p.TeamIndex == team)
                {
                    colorId = p.ColorId;
                    break;
                }
            }

            if (colorId == -1)
            {
                colorId = team;
            }
        }

        if (colorId == 0) // Blue / Water
        {
            if (waterTexture != null) balleRenderer.material.mainTexture = waterTexture;
            if (balleRenderer != null) balleRenderer.material.color = Color.white;
            if (trail != null)
            {
                trail.startColor = new Color(0f, 0.7f, 1f); // Un beau bleu ciel/eau
                trail.endColor = new Color(0f, 0.7f, 1f, 0f);
            }
        }
        else if (colorId == 1) // Red / Fire
        {
            if (fireTexture != null) balleRenderer.material.mainTexture = fireTexture;
            if (balleRenderer != null) balleRenderer.material.color = Color.white;
            if (trail != null)
            {
                trail.startColor = new Color(1f, 0.3f, 0f); // Un bel orange/rouge feu
                trail.endColor = new Color(1f, 0.3f, 0f, 0f);
            }
        }
        else if (colorId >= 2 && colorId < PongPaddle.Palette.Length) // Autre couleur de la palette
        {
            if (balleRenderer != null)
            {
                balleRenderer.material.mainTexture = null;
                balleRenderer.material.color = PongPaddle.Palette[colorId];
            }
            if (trail != null)
            {
                Color c = PongPaddle.Palette[colorId];
                trail.startColor = c;
                trail.endColor = new Color(c.r, c.g, c.b, 0f);
            }
        }
        else // Aucun (-1)
        {
            if (balleRenderer != null)
            {
                balleRenderer.material.mainTexture = null;
                balleRenderer.material.color = Color.white;
            }
            if (trail != null)
            {
                trail.startColor = Color.white;
                trail.endColor = new Color(1f, 1f, 1f, 0f);
            }
        }
    }

    public void ResetBall(bool autoLaunch = true) {
      transform.position = Vector3.zero;
      Speed = BaseSpeed;
      ApplyVisualState(-1);
      if (trail != null)
      {
          trail.Clear(); // Supprime la traînée de téléportation
      }
      if (autoLaunch) {
          _State = PongBallState.Playing;
      }
      hasTouched = false;

      Direction = new Vector3(
        Random.Range(0.5f, 1),
        Random.Range(-0.5f, 0.5f),
        0
      );
      Direction.x *= Mathf.Sign(Random.Range(-100, 100));
      Direction.Normalize();
    }

    void Update() {
      if (RemoteDisplay) return;   // client : position fixée par le serveur (pas de simulation)

      if (State != PongBallState.Playing) {
        return;
      }

      transform.position = transform.position + (Direction * Speed * Time.deltaTime);
    }

    void OnCollisionEnter(Collision c) {
        PongPaddle paddle = c.collider.GetComponent<PongPaddle>();
        if (paddle != null)
        {
            if (RemoteDisplay) return; // Le client ne gère pas la physique des paddles ni l'owner
            hasTouched = true;
            lastTouchedPlayer = paddle.Player;
            lastTouchedTeam = paddle.TeamIndex != -1 ? paddle.TeamIndex : (((int)paddle.Player % 2 == 1) ? 0 : 1);
            ApplyVisualState(lastTouchedTeam);
            Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
            Speed += 0.5f;
            return;
        }

        if (c.collider.name == "circle")
        {
            // Son de rebond sur le mur extérieur (joué systématiquement, avant la logique de score)
            if (wallBounceClip != null && audioSource != null)
                audioSource.PlayOneShot(wallBounceClip, 0.5f);

            if (RemoteDisplay) return; // Le client ne gère ni les scores ni le reset de la balle localement

            if (hasTouched)
            {
                int teamIndex = -1;
                PongPaddle[] allPaddles = FindObjectsByType<PongPaddle>();
                foreach (var p in allPaddles)
                {
                    if (p != null && p.Player == lastTouchedPlayer)
                    {
                        teamIndex = p.TeamIndex;
                        break;
                    }
                }

                if (teamIndex == -1)
                {
                    teamIndex = ((int)lastTouchedPlayer % 2 == 1) ? 0 : 1;
                }

                if (teamIndex == 0)
                {                // Left (Blue / Water)
                    scoreLeft++;
                    if (scoreDisplay != null) scoreDisplay.MarquerPointGauche();
                    if (scoreLeft >= winScore)
                    {
                        _State = PongBallState.PlayerLeftWin;
                    }
                    else
                    {
                        ResetBall();
                    }
                }
                else
                {
                    // Right (Red / Fire)
                    scoreRight++;
                    if (scoreDisplay != null) scoreDisplay.MarquerPointDroit();
                    if (scoreRight >= winScore)
                    {
                        _State = PongBallState.PlayerRightWin;
                    }
                    else
                    {
                        ResetBall();
                    }
                }
            }
            else
            {
                ResetBall();
            }
        }
    }
}
