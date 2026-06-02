using UnityEngine;

public enum PongBallState {
  Playing = 0,
  PlayerLeftWin = 1,
  PlayerRightWin = 2,
  WaitingForServe = 3
}

public class PongBall : MonoBehaviour
{
    public Color blue = Color.blue;
    public Color red = Color.red;

    private Renderer balleRenderer;
    public float Speed = 1;
    private float BaseSpeed;

    Vector3 Direction;
    PongBallState _State = PongBallState.Playing;

    public int scoreLeft = 0;
    public int scoreRight = 0;
    public int winScore = 5;

    public PongBallState State {
      get {
        return _State;
      }
    }

    public PongScore scoreDisplay;

    /// <summary>Dernier joueur ayant touché la balle (0 = Left, 1 = Right, -1 = aucun). Lu par le serveur.</summary>
    public int LastHitter => hasTouched
        ? (lastTouchedPlayer == PongPlayer.PlayerLeft ? 0 : 1)
        : -1;

    void Start() {
      BaseSpeed = Speed;
      balleRenderer = GetComponent<Renderer>();
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
        switch (c.collider.name)
        {
            case "PaddleLeft":
                balleRenderer.material.color = blue;
                Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
                Speed += 0.5f;
                break;
            case "PaddleRight":
                balleRenderer.material.color = red;
                Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
                Speed += 0.5f;
                break;

            case "circle":
            if (balleRenderer.material.color == red) {
                // Red = Right
                scoreRight++;
                if(scoreDisplay != null) scoreDisplay.MarquerPointDroit();
                if (scoreRight >= winScore) {
                   _State = PongBallState.PlayerRightWin;
                } else {
                   ResetBall();
                }
            } else if (balleRenderer.material.color == blue) {
                // Blue = Left
                scoreLeft++;
                if(scoreDisplay != null) scoreDisplay.MarquerPointGauche();
                if (scoreLeft >= winScore) {
                   _State = PongBallState.PlayerLeftWin;
                } else {
                   ResetBall();
                }
            } else
                {
                    ResetBall();
                }
                break;
      }
    }
}
