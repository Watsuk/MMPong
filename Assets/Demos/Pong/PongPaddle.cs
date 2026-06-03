using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!DrivenExternally)
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
        }

        Renderer r = GetComponent<Renderer>();
        if (r != null) {
            if (Player == PongPlayer.PlayerLeft) {
                r.material.color = Color.blue;
            } else if (Player == PongPlayer.PlayerRight) {
                r.material.color = Color.red;
            }
        }

        Vector3 offset = transform.position - CenterPoint;
        radius = offset.magnitude;
        currentAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        baseAngle = currentAngle;
        baseRotation = transform.rotation;
    }

    public void SetCircle(int index, float newRadius, Vector3 center)
    {
        circleIndex = index;
        radius = newRadius;
        CenterPoint = center;

        Vector3 offset = transform.position - CenterPoint;
        currentAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        baseAngle = currentAngle;
        baseRotation = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
      float direction = DrivenExternally ? ExternalDirection : PlayerAction.ReadValue<float>();

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
        PongPaddle[] allPaddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);
        foreach (PongPaddle otherPaddle in allPaddles)
        {
            if (otherPaddle != this)
            {
                // Verify they are on the same circle by comparing their radius
                if (Mathf.Abs(this.radius - otherPaddle.radius) < 0.1f)
                {
                    float angleDiff = Mathf.DeltaAngle(desiredAngle, otherPaddle.currentAngle);
                    
                    // We use the full widths to be absolutely sure the collision box is large enough
                    float minDistance = PaddleWidth + otherPaddle.PaddleWidth;

                    if (Mathf.Abs(angleDiff) < minDistance)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    void OnDisable() {
      if (PlayerAction != null) PlayerAction.Disable();
    }
}