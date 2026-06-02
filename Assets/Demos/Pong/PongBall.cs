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
      PongPaddle paddle = c.gameObject.GetComponent<PongPaddle>();
      if (paddle != null) {
          lastTouchedPlayer = paddle.Player;
          hasTouched = true;

          Renderer paddleRenderer = paddle.GetComponent<Renderer>();
          if (paddleRenderer != null) {
              balleRenderer.material.color = paddleRenderer.material.color;
          } else {
              // Fallback just in case
              if (paddle.Player == PongPlayer.PlayerLeft) balleRenderer.material.color = blue;
              else balleRenderer.material.color = red;
          }
          
          if (c.contacts.Length > 0) {
              Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
          } else {
              Direction = -Direction;
          }
          Speed += 0.5f;
      }
      else if (c.gameObject.name == "circle") {
          if (!hasTouched) {
              ResetBall();
              return;
          }

          if (lastTouchedPlayer == PongPlayer.PlayerRight) {
              _State = PongBallState.PlayerRightWin;
          } else if (lastTouchedPlayer == PongPlayer.PlayerLeft) {
              _State = PongBallState.PlayerLeftWin;
          } else {
              ResetBall();
          }
      }
    }
}
