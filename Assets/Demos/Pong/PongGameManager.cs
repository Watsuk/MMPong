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

    void AssignPaddlesToCircles()
    {
        // Séparer les paddles par couleur (impair = bleu/gauche, pair = rouge/droit)
        List<PongPaddle> bluePaddles = new List<PongPaddle>();
        List<PongPaddle> redPaddles = new List<PongPaddle>();

        foreach (PongPaddle p in allPaddles)
        {
            if ((int)p.Player % 2 == 1)
                bluePaddles.Add(p);
            else
                redPaddles.Add(p);
        }

        // Distribuer les paddles bleus sur des cercles différents
        AssignGroupToCircles(bluePaddles);

        // Distribuer les paddles rouges sur des cercles différents
        AssignGroupToCircles(redPaddles);
    }

    void AssignGroupToCircles(List<PongPaddle> group)
    {
        // Créer une liste d'indices de cercles disponibles
        List<int> availableCircles = new List<int>();
        for (int i = 0; i < CircleRadii.Length; i++)
        {
            availableCircles.Add(i);
        }

        // Mélanger les cercles pour garder un côté aléatoire
        // Fisher-Yates shuffle
        for (int i = availableCircles.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            int tmp = availableCircles[i];
            availableCircles[i] = availableCircles[r];
            availableCircles[r] = tmp;
        }

        for (int i = 0; i < group.Count; i++)
        {
            // Si on a plus de paddles que de cercles, on boucle sur les cercles disponibles
            int circleIndex = availableCircles[i % CircleRadii.Length];
            float radius = CircleRadii[circleIndex];

            paddleToCircleIndex[group[i]] = circleIndex;
            group[i].SetCircle(circleIndex, radius, CenterPoint);
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

        int playersToSpawn = Mathf.Min(totalPlayers, spawnPoints.Count);

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
}
