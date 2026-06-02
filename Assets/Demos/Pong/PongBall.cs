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
    PongBallState _State = PongBallState.WaitingForServe;

    public int scoreLeft = 0;
    public int scoreRight = 0;
    public int winScore = 5;

    public int LastHitter = -1;

    public PongBallState State {
      get {
        return _State;
      }
    } 

    void Start() {
      BaseSpeed = Speed;
      balleRenderer = GetComponent<Renderer>();
      ResetBall();
    }

    public void ResetBall() {
      transform.position = Vector3.zero;
      Speed = BaseSpeed;
      balleRenderer.material.color = Color.white;
      _State = PongBallState.WaitingForServe;
      LastHitter = -1;

      Direction = new Vector3(
        Random.Range(0.5f, 1),
        Random.Range(-0.5f, 0.5f),
        0
      );
      Direction.x *= Mathf.Sign(Random.Range(-100, 100));
      Direction.Normalize();
    }

    void Update() {
      if (State == PongBallState.WaitingForServe) {
          if (Input.GetKeyDown(KeyCode.Space)) {
              _State = PongBallState.Playing;
          }
          return;
      }

      if (State != PongBallState.Playing) {
        return;
      }

      transform.position = transform.position + (Direction * Speed * Time.deltaTime);
    }

    void OnCollisionEnter(Collision c) {
      switch (c.collider.name) {
        case "PaddleLeft":
            balleRenderer.material.color = blue;
            Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
            Speed += 0.5f;
            LastHitter = 0;
            break;
        case "PaddleRight":
            balleRenderer.material.color = red;
            Direction = Vector3.Reflect(Direction, c.contacts[0].normal).normalized;
            Speed += 0.5f;
            LastHitter = 1;
                break;

        case "circle":
            if (balleRenderer.material.color == red) {
                // Red = Right
                scoreRight++;
                if (scoreRight >= winScore) {
                   _State = PongBallState.PlayerRightWin;
                } else {
                   ResetBall();
                }
            } else if (balleRenderer.material.color == blue) {
                // Blue = Left
                scoreLeft++;
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
