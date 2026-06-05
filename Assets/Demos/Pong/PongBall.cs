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

    private Renderer balleRenderer;
    private TrailRenderer trail;
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

    public PongBallState State {
      get {
        return _State;
      }
    }

    public PongScore scoreDisplay;

    /// <summary>Dernier joueur ayant touché la balle (0 = Left, 1 = Right, -1 = aucun). Lu par le serveur.</summary>
    public int LastHitter => hasTouched
        ? ((int)lastTouchedPlayer - 1)
        : -1;

    void Start() {
      BaseSpeed = Speed;
      balleRenderer = GetComponent<Renderer>();
      trail = GetComponent<TrailRenderer>();
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

    private PongPlayer lastTouchedPlayer;
    private bool hasTouched = false;

    public void SetStateToMenu() {
        _State = PongBallState.StartMenu;
    }

    public void StartGameFromMenu() {
        _State = PongBallState.Playing;
    }

    /// <summary>Met à jour l'état visuel (texture et couleur de la traînée) selon le dernier joueur ayant touché la balle.</summary>
    public void ApplyVisualState(int ownerId)
    {
        if (balleRenderer == null)
            balleRenderer = GetComponent<Renderer>();
        if (trail == null)
            trail = GetComponent<TrailRenderer>();

        if (ownerId == 0) // Left Player (Water)
        {
            if (waterTexture != null) balleRenderer.material.mainTexture = waterTexture;
            if (balleRenderer != null) balleRenderer.material.color = Color.white;
            if (trail != null)
            {
                trail.startColor = new Color(0f, 0.7f, 1f); // Un beau bleu ciel/eau
                trail.endColor = new Color(0f, 0.7f, 1f, 0f);
            }
        }
        else if (ownerId == 1) // Right Player (Fire)
        {
            if (fireTexture != null) balleRenderer.material.mainTexture = fireTexture;
            if (balleRenderer != null) balleRenderer.material.color = Color.white;
            if (trail != null)
            {
                trail.startColor = new Color(1f, 0.3f, 0f); // Un bel orange/rouge feu
                trail.endColor = new Color(1f, 0.3f, 0f, 0f);
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
        switch (c.collider.name)
        {
            case "PaddleLeft":
                hasTouched = true;
                lastTouchedPlayer = PongPlayer.PlayerLeft;
                ApplyVisualState(0);
                Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
                Speed += 0.5f;
                break;
            case "PaddleRight":
                hasTouched = true;
                lastTouchedPlayer = PongPlayer.PlayerRight;
                ApplyVisualState(1);
                Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
                Speed += 0.5f;
                break;

            case "circle":
                if (hasTouched)
                {
                    if ((int)lastTouchedPlayer % 2 == 1)
                    {                // Right
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
                    else
                    {
                        // Left
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
                }
                else
                {
                    ResetBall();
                }
                break;
        }
    }
}
