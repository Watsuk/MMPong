using UnityEngine;
using UnityEngine.InputSystem;

public enum PongPlayer {
  PlayerLeft = 1,
  PlayerRight = 2
}

public class PongPaddle : MonoBehaviour
{ 
    public PongPlayer Player = PongPlayer.PlayerLeft;
    public float Speed = 1;
    public float PaddleWidth = 10f;
    public Vector3 CenterPoint = Vector3.zero;

    PongInput inputActions;
    InputAction PlayerAction;

    private float currentAngle;
    private float radius;
    private float baseAngle;
    private Quaternion baseRotation;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        inputActions = new PongInput();
        switch (Player) {
          case PongPlayer.PlayerLeft:
            PlayerAction = inputActions.Pong.Player1;
            break;
          case PongPlayer.PlayerRight:
            PlayerAction = inputActions.Pong.Player2;
            break;
        }

        PlayerAction.Enable();

        Vector3 offset = transform.position - CenterPoint;
        radius = offset.magnitude;
        currentAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        baseAngle = currentAngle;
        baseRotation = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
      float direction = PlayerAction.ReadValue<float>();

      // Convert linear speed to angular speed: v = r * omega
      float angularSpeedDeg = (Speed / radius) * Mathf.Rad2Deg;

      // Direction is always counter-clockwise: positive = counter-clockwise, negative = clockwise
      float newAngle = currentAngle - direction * angularSpeedDeg * Time.deltaTime;

      if (!CheckCollisionWithOtherPaddle(newAngle))
      {
          currentAngle = newAngle;
      }

      // Update position and rotation
      float rad = currentAngle * Mathf.Deg2Rad;
      transform.position = CenterPoint + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;

      float angleDiff = currentAngle - baseAngle;
      transform.rotation = baseRotation * Quaternion.Euler(0, 0, angleDiff);
    }

    bool CheckCollisionWithOtherPaddle(float desiredAngle)
    {
        PongPaddle otherPaddle = GetOtherPaddle();
        if (otherPaddle == null) return false;

        float halfPaddleWidth = PaddleWidth * 0.5f;
        float otherHalfPaddleWidth = otherPaddle.PaddleWidth * 0.5f;

        float angleDiff = Mathf.DeltaAngle(desiredAngle, otherPaddle.currentAngle);
        float minDistance = halfPaddleWidth + otherHalfPaddleWidth;

        return Mathf.Abs(angleDiff) < minDistance;
    }

    PongPaddle GetOtherPaddle()
    {
        PongPlayer otherPlayer = (Player == PongPlayer.PlayerLeft) ? PongPlayer.PlayerRight : PongPlayer.PlayerLeft;
        PongPaddle[] allPaddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);
        foreach (PongPaddle paddle in allPaddles)
        {
            if (paddle.Player == otherPlayer)
                return paddle;
        }
        return null;
    }

    void OnDisable() {
      PlayerAction.Disable();
    }
}