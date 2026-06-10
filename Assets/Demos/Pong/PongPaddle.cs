using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

public enum PongPlayer {
  PlayerLeft = 1,
  PlayerRight = 2,
  Player3 = 3,
  Player4 = 4,
  Player5 = 5,
  Player6 = 6
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

    // Affichage distant (client) : quand true, le paddle ne se déplace pas seul ; sa position
    // vient du serveur via ApplyNetworkAngle. Inerte par défaut → jeu local non affecté.
    public bool RemoteDisplay = false;

    /// <summary>Angle courant du paddle sur le cercle (lu par le serveur).</summary>
    public float CurrentAngle => currentAngle;

    /// <summary>Place le paddle à l'angle reçu du serveur (affichage client).</summary>
    public void ApplyNetworkAngle(float deg)
    {
        currentAngle = deg;
        ApplyTransform();
    }

    PongInput inputActions;
    InputAction PlayerAction;

    private float currentAngle;
    private float radius;
    private float baseAngle;
    private Quaternion baseRotation;
    private int circleIndex;
    private bool circleInitialized = false;

    private TextMeshPro nameText;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject textObj = new GameObject("PseudoText");
        textObj.transform.SetParent(this.transform);
        textObj.transform.localPosition = new Vector3(0, 1.5f, 0);
        nameText = textObj.AddComponent<TextMeshPro>();
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontSize = 8;
        nameText.color = Color.white;
        nameText.text = "";

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
            default:
                PlayerAction = null;
                break;
            }

            if (PlayerAction != null)
            {
                PlayerAction.Enable();
            }
        }

        Renderer r = GetComponent<Renderer>();
        if (r != null) {
            if ((int)Player % 2 == 1)
            {
                r.material.color = Color.blue;
            }
            else
            {
                r.material.color = Color.red;
            }
        }

        if (radius == 0f)
        {
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
      if (RemoteDisplay) return;   // client : position pilotée par ApplyNetworkAngle

        float direction = 0f;
        if (DrivenExternally)
        {
            direction = ExternalDirection;
        }
        else if (PlayerAction != null)
        {
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

      ApplyTransform();
    }

    void ApplyTransform()
    {
      float rad = currentAngle * Mathf.Deg2Rad;
      transform.position = CenterPoint + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;

      float angleDiff = currentAngle - baseAngle;
      transform.rotation = baseRotation * Quaternion.Euler(0, 0, angleDiff);

      if (nameText != null)
      {
          nameText.transform.rotation = Quaternion.identity;
      }
    }

    public void SetPseudo(string pseudo)
    {
        if (nameText != null)
        {
            nameText.text = pseudo;
        }
    }

    private bool isLocalPlayerSet = false;
    public void SetAsLocalPlayer()
    {
        if (isLocalPlayerSet) return;
        isLocalPlayerSet = true;
        RemoteDisplay = false;

        GameObject outline = new GameObject("Outline");
        outline.transform.SetParent(this.transform);
        outline.transform.localPosition = new Vector3(0, 0, 0.5f);
        outline.transform.localRotation = Quaternion.identity;
        outline.transform.localScale = new Vector3(1.15f, 1.15f, 1.0f);

        MeshFilter myMesh = GetComponent<MeshFilter>();
        if (myMesh != null) {
            MeshFilter outMesh = outline.AddComponent<MeshFilter>();
            outMesh.sharedMesh = myMesh.sharedMesh;
        }

        Renderer myRen = GetComponent<Renderer>();
        if (myRen != null) {
            MeshRenderer outRen = outline.AddComponent<MeshRenderer>();
            outRen.material = new Material(Shader.Find("Unlit/Color"));
            outRen.material.color = Color.white;
        }
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