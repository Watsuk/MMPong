using UnityEngine;

public enum PongBallState {
  Playing = 0,
  PlayerBlueWin = 1,
  PlayerRedWin = 2,
  WaitingForServe = 3
}

public class PongBall : MonoBehaviour
{
    private Renderer balleRenderer;
    public float Speed = 1;
    private float BaseSpeed;

    Vector3 Direction;
    PongBallState _State = PongBallState.Playing;

    public int scoreBlue = 0;
    public int scoreRed = 0;
    public int winScore = 5;

    public PongBallState State {
      get {
        return _State;
      }
    }

    public PongScore scoreDisplay;

    /// <summary>Dernier joueur ayant touché la balle (0 = Left, 1 = Right, etc., -1 = aucun). Lu par le serveur.</summary>
    public int LastHitter => hasTouched
        ? ((int)lastTouchedPlayer - 1)
        : -1;

    void Start() {
      BaseSpeed = Speed;
      balleRenderer = GetComponent<Renderer>();
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
      ResetBall();
    }

    private PongPlayer lastTouchedPlayer;
    private bool hasTouched = false;

    public void ResetBall() {
      transform.position = Vector3.zero;
      Speed = BaseSpeed;
      balleRenderer.material.color = Color.white;
      _State = PongBallState.Playing;
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
      if (State != PongBallState.Playing) {
        return;
      }

      transform.position = transform.position + (Direction * Speed * Time.deltaTime);
    }

    void OnCollisionEnter(Collision c) {
        PongPaddle paddle = c.collider.GetComponent<PongPaddle>();
        if (paddle != null)
        {
            Renderer paddleRenderer = paddle.GetComponent<Renderer>();
            if (paddleRenderer != null) {
                balleRenderer.material.color = paddleRenderer.material.color;
            } else {
                if ((int)paddle.Player % 2 == 1) balleRenderer.material.color = Color.blue;
                else balleRenderer.material.color = Color.red;
            }

            lastTouchedPlayer = paddle.Player;
            hasTouched = true;

            Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
            Speed += 0.5f;
            return;
        }

        switch (c.collider.name)
        {
            case "circle":
            if (hasTouched) {
                if ((int)lastTouchedPlayer % 2 == 1) {
                    // Impair = Bleu = Left/Blue team
                    scoreBlue++;
                    if (scoreDisplay != null) {
                        try {
                            scoreDisplay.BlueScorePoint();
                        } catch (System.Exception e) {
                            Debug.LogError($"[PongBall] Erreur lors du score bleu : {e.Message}");
                        }
                    }
                    if (scoreBlue >= winScore) {
                       _State = PongBallState.PlayerBlueWin;
                    } else {
                       ResetBall();
                    }
                } else {
                    // Pair = Rouge = Right/Red team
                    scoreRed++;
                    if (scoreDisplay != null) {
                        try {
                            scoreDisplay.RedScorePoint();
                        } catch (System.Exception e) {
                            Debug.LogError($"[PongBall] Erreur lors du score rouge : {e.Message}");
                        }
                    }
                    if (scoreRed >= winScore) {
                       _State = PongBallState.PlayerRedWin;
                    } else {
                       ResetBall();
                    }
                }
            } else {
                ResetBall();
            }
            break;
      }
    }
}
