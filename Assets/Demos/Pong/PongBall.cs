using UnityEngine;

public enum PongBallState {
  Playing = 0,
  PlayerLeftWin = 1,
  PlayerRightWin = 2,
}

public class PongBall : MonoBehaviour
{
    public Color blue = Color.blue;
    public Color red = Color.red;


    private Renderer balleRenderer;
    public float Speed = 1;

    Vector3 Direction;
    PongBallState _State = PongBallState.Playing;

    public PongBallState State {
      get {
        return _State;
      }
    } 

    void Start() {
      Direction = new Vector3(
        Random.Range(0.5f, 1),
        Random.Range(-0.5f, 0.5f),
        0
      );
      Direction.x *= Mathf.Sign(Random.Range(-100, 100));
      Direction.Normalize();
      balleRenderer = GetComponent<Renderer>();
    }

    void Update() {
      if (State != PongBallState.Playing) {
        return;
      }

      transform.position = transform.position + (Direction * Speed * Time.deltaTime);
    }

    void OnCollisionEnter(Collision c) {
      switch (c.collider.name) {
        case "BoundTop":
        case "BoundBottom":
                Direction.y = -Direction.y;
          break;

        case "PaddleLeft":
            balleRenderer.material.color = blue;
            Direction.x = -Direction.x;
          break;
        case "PaddleRight":
            balleRenderer.material.color = red;
            Direction.x = -Direction.x;
          break;

        case "circle":
            if (balleRenderer.material.color == red) {
                _State = PongBallState.PlayerRightWin;
            } else if (balleRenderer.material.color == blue) {
                _State = PongBallState.PlayerLeftWin;
            }
          break;

      }
    }

}
