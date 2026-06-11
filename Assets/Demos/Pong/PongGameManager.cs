using UnityEngine;
using System.Collections.Generic;

public class PongGameManager : MonoBehaviour
{
    public GameObject paddlePrefab;
    public List<Transform> spawnPoints;
    [Range(2, 6)]
    public int totalPlayers = 2;
    private List<GameObject> activePaddles = new List<GameObject>();

    [SerializeField] public float[] CircleRadii = { 5f, 7f, 9f };
    [SerializeField] public int NumberOfPlayers = 2;
    public Vector3 CenterPoint = Vector3.zero;

    private Dictionary<PongPaddle, int> paddleToCircleIndex = new Dictionary<PongPaddle, int>();
    private List<PongPaddle> allPaddles = new List<PongPaddle>();
    private List<LineRenderer> circleLineRenderers = new List<LineRenderer>();
    private static PongGameManager instance;

    public static PongGameManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        gameObject.AddComponent<MMPong.BonusManager>();

        // Attache le CameraShaker à la caméra principale (auto-setup, pas besoin de le faire dans l'éditeur)
        if (Camera.main != null && Camera.main.GetComponent<CameraShaker>() == null)
            Camera.main.gameObject.AddComponent<CameraShaker>();
    }

    void Start()
    {
        GameObject outerCircle = GameObject.Find("circle");
        if (outerCircle != null)
        {
            Collider c = outerCircle.GetComponent<Collider>();
            if (c != null) {
                float maxRadius = c.bounds.extents.x;
                CircleRadii = new float[] {
                    maxRadius * 0.4f,
                    maxRadius * 0.7f,
                    maxRadius * 0.95f
                };
            }
        }

        SpawnPlayers();
        InitializeGame();
        CreateCircleVisuals();
    }

    void InitializeGame()
    {
        allPaddles.Clear();
        paddleToCircleIndex.Clear();

        PongPaddle[] existingPaddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);
        foreach (PongPaddle paddle in existingPaddles)
        {
            allPaddles.Add(paddle);
        }

        foreach (GameObject paddleObj in activePaddles)
        {
            PongPaddle p = paddleObj.GetComponent<PongPaddle>();
            if (p != null && !allPaddles.Contains(p))
            {
                allPaddles.Add(p);
            }
        }

        AssignPaddlesToCircles();
    }

    void CreateCircleVisuals()
    {
        Color[] colors = { Color.red, Color.green, Color.blue };
        
        for (int i = 0; i < CircleRadii.Length; i++)
        {
            GameObject circleGO = new GameObject($"Circle_{CircleRadii[i]}");
            circleGO.transform.parent = transform;
            circleGO.transform.localPosition = CenterPoint;

            LineRenderer lineRenderer = circleGO.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.startColor = colors[i % colors.Length];
            lineRenderer.endColor = colors[i % colors.Length];

            int segments = 64;
            lineRenderer.positionCount = segments + 1;

            Vector3[] positions = new Vector3[segments + 1];
            for (int j = 0; j <= segments; j++)
            {
                float angle = (j / (float)segments) * Mathf.PI * 2f;
                positions[j] = new Vector3(Mathf.Cos(angle) * CircleRadii[i], Mathf.Sin(angle) * CircleRadii[i], 0);
            }
            lineRenderer.SetPositions(positions);

            circleLineRenderers.Add(lineRenderer);
        }
    }

    public void AssignPaddlesToCircles()
    {
        // On évite d'avoir plus d'un joueur de la même équipe sur un même cercle.
        // L'ordre des cercles est bleu, vert, rouge (2, 1, 0).
        int[] circleOrder = { 2, 1, 0 };

        List<PongPaddle> team0Paddles = new List<PongPaddle>();
        List<PongPaddle> team1Paddles = new List<PongPaddle>();

        // Trier les paddles de manière déterministe par leur index de joueur (Player)
        List<PongPaddle> sortedPaddles = new List<PongPaddle>(allPaddles);
        sortedPaddles.Sort((a, b) => ((int)a.Player).CompareTo((int)b.Player));

        foreach (PongPaddle p in sortedPaddles)
        {
            if (p == null) continue;
            // Ne placer sur un cercle que les paddles actifs dans la hiérarchie.
            if (!p.gameObject.activeInHierarchy) continue;

            int team = (p.TeamIndex != -1) ? p.TeamIndex : (((int)p.Player % 2 == 1) ? 0 : 1);
            if (team == 0)
                team0Paddles.Add(p);
            else
                team1Paddles.Add(p);
        }

        // Attribution pour l'équipe 0 (un joueur max par cercle)
        for (int i = 0; i < team0Paddles.Count; i++)
        {
            PongPaddle p = team0Paddles[i];
            int circleIndex = circleOrder[i % circleOrder.Length] % CircleRadii.Length;
            float radius = CircleRadii[circleIndex];

            paddleToCircleIndex[p] = circleIndex;
            p.SetCircle(circleIndex, radius, CenterPoint);
        }

        // Attribution pour l'équipe 1 (un joueur max par cercle)
        for (int i = 0; i < team1Paddles.Count; i++)
        {
            PongPaddle p = team1Paddles[i];
            int circleIndex = circleOrder[i % circleOrder.Length] % CircleRadii.Length;
            float radius = CircleRadii[circleIndex];

            paddleToCircleIndex[p] = circleIndex;
            p.SetCircle(circleIndex, radius, CenterPoint);
        }
    }

    public int GetCircleIndex(PongPaddle paddle)
    {
        if (paddleToCircleIndex.TryGetValue(paddle, out int index))
            return index;
        return -1;
    }

    public float GetCircleRadius(PongPaddle paddle)
    {
        int index = GetCircleIndex(paddle);
        if (index >= 0 && index < CircleRadii.Length)
            return CircleRadii[index];
        return 0;
    }

    public float GetOutermostRadius()
    {
        return CircleRadii[0];
    }

    public List<PongPaddle> GetPaddlesOnSameCircle(PongPaddle paddle)
    {
        List<PongPaddle> result = new List<PongPaddle>();
        int circleIndex = GetCircleIndex(paddle);

        foreach (PongPaddle p in allPaddles)
        {
            if (p != paddle && GetCircleIndex(p) == circleIndex)
                result.Add(p);
        }

        return result;
    }

    void SpawnPlayers()
    {
        int existingCount = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None).Length;

        // Des paddles sont déjà placées dans la scène (Player 1 et 2). On ne spawne que le
        // complément pour atteindre totalPlayers — sinon on cumule (pré-placées + totalPlayers)
        // et on obtient trop de paddles (ex. 2 pré-placées + 2 = 4).
        int playersToSpawn = Mathf.Clamp(totalPlayers - existingCount, 0, spawnPoints.Count);

        for (int i = 0; i < playersToSpawn; i++)
        {
            GameObject newPaddle = Instantiate(paddlePrefab, spawnPoints[i].position, spawnPoints[i].rotation);

            activePaddles.Add(newPaddle);

            PongPaddle paddleScript = newPaddle.GetComponent<PongPaddle>();
            if (paddleScript != null)
            {
                paddleScript.Player = (PongPlayer)(existingCount + i + 1);

                bool isControllablePlayer = (existingCount + i < 2);
                paddleScript.DrivenExternally = !isControllablePlayer;
            }
        }
    }

    public void SetActivePlayers(int count)
    {
        totalPlayers = count;

        // 1. Find all paddles currently in the scene (including inactive ones)
        List<PongPaddle> paddlesInScene = new List<PongPaddle>(
            FindObjectsByType<PongPaddle>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        );
        paddlesInScene.Sort((a, b) => ((int)a.Player).CompareTo((int)b.Player));

        // 2. If we need more than currently exist in the scene, spawn the missing ones
        int existingCount = paddlesInScene.Count;
        int playersToSpawn = Mathf.Clamp(totalPlayers - existingCount, 0, spawnPoints.Count);
        for (int i = 0; i < playersToSpawn; i++)
        {
            int spawnIndex = existingCount - 2 + i;
            if (spawnIndex >= 0 && spawnIndex < spawnPoints.Count)
            {
                GameObject newPaddle = Instantiate(paddlePrefab, spawnPoints[spawnIndex].position, spawnPoints[spawnIndex].rotation);
                activePaddles.Add(newPaddle);
                PongPaddle paddleScript = newPaddle.GetComponent<PongPaddle>();
                if (paddleScript != null)
                {
                    paddleScript.Player = (PongPlayer)(existingCount + i + 1);
                    bool isControllablePlayer = (existingCount + i < 2);
                    paddleScript.DrivenExternally = !isControllablePlayer;
                }
                paddlesInScene.Add(paddleScript);
            }
        }

        // 3. Set active state based on player index and totalPlayers
        foreach (var p in paddlesInScene)
        {
            if (p != null)
            {
                bool active = (int)p.Player <= totalPlayers;
                p.gameObject.SetActive(active);
            }
        }

        // 4. Re-initialize game mappings
        InitializeGame();

        // 5. Notify active ClientStateApplier to refresh its cached paddles list
        var applier = FindFirstObjectByType<MMPong.Network.ClientStateApplier>();
        if (applier != null)
        {
            applier.RefreshPaddles();
        }
    }
}
