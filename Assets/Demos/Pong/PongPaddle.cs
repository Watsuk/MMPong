using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum PongPlayer {
  Player1 = 1,
  Player2 = 2,
  Player3 = 3,
  Player4 = 4,
  Player5 = 5,
  Player6 = 6
}

public class PongPaddle : MonoBehaviour
{
    public PongPlayer Player = PongPlayer.Player1;
    public float Speed = 1;
    public float PaddleWidth = 10f;
    public Vector3 CenterPoint = Vector3.zero;

    // Pilotage externe (mode réseau) : quand true, la direction vient de ExternalDirection
    // au lieu du clavier. Inerte par défaut → le jeu local n'est pas affecté.
    public bool DrivenExternally = false;
    public float ExternalDirection = 0f;

    /// <summary>Angle courant du paddle sur le cercle (lu par le serveur).</summary>
    public float CurrentAngle => currentAngle;

    PongInput inputActions;
    InputAction PlayerAction;

    private float currentAngle;
    private float radius;
    private float baseAngle;
    private Quaternion baseRotation;
    private int circleIndex;
    private bool circleInitialized = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!DrivenExternally)
        {
            inputActions = new PongInput();
            switch (Player) {
              case PongPlayer.Player1:
                PlayerAction = inputActions.Pong.Player1;
                break;
              case PongPlayer.Player2:
                PlayerAction = inputActions.Pong.Player2;
                break;
              default:
                // Pour Player3, Player4, etc., pour l'instant on ignore l'input
                PlayerAction = null;
                break;
            }

            if (PlayerAction != null) {
                PlayerAction.Enable();
            }
        }

        Renderer r = GetComponent<Renderer>();
        if (r != null) {
            // Les joueurs impairs (1, 3, etc.) sont Bleus, les joueurs pairs (2, 4, etc.) sont Rouges
            if ((int)Player % 2 == 1) {
                r.material.color = Color.blue;
            } else {
                r.material.color = Color.red;
            }
        }

        if (!circleInitialized)
        {
            Vector3 offset = transform.position - CenterPoint;
            radius = offset.magnitude;
            currentAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            baseAngle = currentAngle;
            baseRotation = transform.rotation;
            circleInitialized = true;
        }
    }

    public void SetCircle(int index, float newRadius, Vector3 center)
    {
        circleIndex = index;
        radius = newRadius;
        CenterPoint = center;
        circleInitialized = true;

        Vector3 offset = transform.position - CenterPoint;
        currentAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        baseAngle = currentAngle;
        baseRotation = transform.rotation;
    }

        // Update is called once per frame
    void Update()
    {
      float direction = 0f;
      if (DrivenExternally) {
          direction = ExternalDirection;
      } else if (PlayerAction != null) {
          direction = PlayerAction.ReadValue<float>();
      }

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
        if (PongGameManager.Instance == null) return false;

        List<PongPaddle> sameCirclePaddles = PongGameManager.Instance.GetPaddlesOnSameCircle(this);

        foreach (PongPaddle otherPaddle in sameCirclePaddles)
        {
            if (otherPaddle.Player != this.Player)
            {
                float halfPaddleWidth = PaddleWidth * 0.5f;
                float otherHalfPaddleWidth = otherPaddle.PaddleWidth * 0.5f;

                float angleDiff = Mathf.DeltaAngle(desiredAngle, otherPaddle.currentAngle);
                float minDistance = halfPaddleWidth + otherHalfPaddleWidth;

                if (Mathf.Abs(angleDiff) < minDistance)
                {
                    return true;
                }
            }
        }
        return false;
    }

    void OnDisable() {
      if (PlayerAction != null) PlayerAction.Disable();
    }
}