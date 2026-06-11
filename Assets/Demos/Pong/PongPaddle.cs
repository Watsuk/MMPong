using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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

    // Couleur choisie par le joueur (index dans Palette). -1 = couleur par défaut selon l'équipe.
    public int ColorId = -1;
    public int TeamIndex = -1;

    /// <summary>
    /// Palette de couleurs de paddle, partagée entre l'UI (sélecteur), le réseau (on ne
    /// transmet qu'un index) et le rendu. Plus tard remplacée par une sélection de skins.
    /// </summary>
    public static readonly Color[] Palette =
    {
        new Color(0.20f, 0.45f, 0.95f), // 0 Bleu
        new Color(0.90f, 0.25f, 0.25f), // 1 Rouge
        new Color(0.30f, 0.80f, 0.35f), // 2 Vert
        new Color(0.95f, 0.80f, 0.25f), // 3 Jaune
        new Color(0.70f, 0.35f, 0.85f), // 4 Violet
        new Color(0.95f, 0.55f, 0.20f), // 5 Orange
    };

    /// <summary>Noms lisibles des couleurs de <see cref="Palette"/> (même ordre).</summary>
    public static readonly string[] PaletteNames =
        { "Bleu", "Rouge", "Vert", "Jaune", "Violet", "Orange" };

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

    /// <summary>Pseudo du joueur (stocké pour l'écran de victoire ; plus affiché au-dessus du paddle).</summary>
    public string Pseudo { get; private set; } = "";


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
            default:
                PlayerAction = null;
                break;
            }

            if (PlayerAction != null)
            {
                PlayerAction.Enable();
            }
        }

        if (TeamIndex == -1)
        {
            TeamIndex = ((int)Player % 2 == 1) ? 0 : 1;
        }
        ApplyColor();

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
    }

    public void SetPseudo(string pseudo)
    {
        Pseudo = pseudo;
    }

    /// <summary>
    /// Définit la couleur du paddle via son index dans <see cref="Palette"/>.
    /// Un index négatif rétablit la couleur par défaut (selon l'équipe gauche/droite).
    /// </summary>
    public void SetColorId(int colorId)
    {
        ColorId = colorId;
        ApplyColor();
    }

    public void ApplyColor()
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null) return;

        if (ColorId >= 0 && ColorId < Palette.Length)
            r.material.color = Palette[ColorId];
        else
            r.material.color = (TeamIndex == 0) ? Color.blue : Color.red; // repli équipe
    }

    private bool isLocalPlayerSet = false;
    public void SetAsLocalPlayer()
    {
        if (isLocalPlayerSet) return;
        isLocalPlayerSet = true;
        RemoteDisplay = false;

        // Prédiction client : le paddle local se déplace tout de suite à partir de ExternalDirection
        // (alimentée par ClientStateApplier avec l'input réseau), et non depuis l'InputAction clavier.
        // Indispensable pour les joueurs 3-6 qui n'ont aucune InputAction dédiée (sinon : zéro
        // prédiction → tout le lag d'un aller-retour serveur).
        DrivenExternally = true;

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
            // Bordure jaune : repère le paddle contrôlé par le joueur local
            // (les deux paddles d'une même équipe partageant le même skin).
            outRen.material.color = Color.yellow;
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

    /// <summary>
    /// Flash visuel temporaire du paddle (feedback de race condition sur le bonus).
    /// Le paddle clignote dans la couleur donnée puis revient à sa couleur d'équipe.
    /// </summary>
    public void FlashColor(Color flashColor, float duration = 0.5f, int blinks = 3)
    {
        StartCoroutine(FlashCoroutine(flashColor, duration, blinks));
    }

    private System.Collections.IEnumerator FlashCoroutine(Color flashColor, float duration, int blinks)
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null) yield break;

        Color original = r.material.color;
        float blinkTime = duration / (blinks * 2f);

        for (int i = 0; i < blinks; i++)
        {
            r.material.color = flashColor;
            yield return new WaitForSeconds(blinkTime);
            r.material.color = original;
            yield return new WaitForSeconds(blinkTime);
        }

        // Garantie : restaure la couleur d'origine
        r.material.color = original;
    }

    void OnDisable() {
      if (PlayerAction != null) PlayerAction.Disable();
    }
}