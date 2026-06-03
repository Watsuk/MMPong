using UnityEngine;
using System.Collections.Generic;

public class PongGameManager : MonoBehaviour
{
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
        List<int> availableCircles = new List<int>();
        for (int i = 0; i < CircleRadii.Length; i++)
        {
            availableCircles.Add(i);
        }

        // Mélange aléatoire des cercles
        for (int i = 0; i < availableCircles.Count; i++)
        {
            int temp = availableCircles[i];
            int randomIndex = Random.Range(i, availableCircles.Count);
            availableCircles[i] = availableCircles[randomIndex];
            availableCircles[randomIndex] = temp;
        }

        List<int> circleIndices = new List<int>();
        for (int i = 0; i < NumberOfPlayers; i++)
        {
            // Assigner un cercle différent à chaque joueur (si possible)
            int circle = availableCircles[i % availableCircles.Count];
            circleIndices.Add(circle);
        }

        for (int i = 0; i < allPaddles.Count && i < circleIndices.Count; i++)
        {
            int circleIndex = circleIndices[i];
            float radius = CircleRadii[circleIndex];

            paddleToCircleIndex[allPaddles[i]] = circleIndex;
            allPaddles[i].SetCircle(circleIndex, radius, CenterPoint);
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
}
