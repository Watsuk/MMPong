using UnityEngine;
using System.Collections;

namespace MMPong
{
    public class BonusManager : MonoBehaviour
    {
        public static BonusManager Instance { get; private set; }

        public bool HasBonus { get; private set; }
        public int BonusCircleIndex { get; private set; }
        public float BonusAngle { get; private set; }

        private float spawnTimer = 2f; // Réduit à 2 secondes pour tester plus vite !
        private float despawnTimer = 5f;
        private GameObject bonusVisual;

        void Awake()
        {
            if (Instance != null && Instance != this) Destroy(gameObject);
            else {
                Instance = this;
                Debug.Log("🟢 [BonusManager] Composant correctement attaché et actif ! Apparition du bonus dans 2 secondes...");
            }
        }

        // Called by ClientStateApplier (Client)
        public void SyncNetworkState(bool hasBonus, int circleIndex, float angle)
        {
            HasBonus = hasBonus;
            BonusCircleIndex = circleIndex;
            BonusAngle = angle;
            UpdateVisuals();
        }

        // Automatically runs every frame
        void Update()
        {
            // Ne pas exécuter la logique si on est un Client pur (pas de serveur mais un client présent).
            // Si on est en mode Local (ni serveur ni client) ou Host (serveur présent), on l'exécute !
            bool isClientOnly = FindAnyObjectByType<MMPong.Network.NetworkServer>() == null && 
                                FindAnyObjectByType<MMPong.Network.NetworkClient>() != null;
            
            if (isClientOnly)
                return;

            if (!HasBonus)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    HasBonus = true;
                    if (PongGameManager.Instance != null && PongGameManager.Instance.CircleRadii != null)
                    {
                        BonusCircleIndex = Random.Range(0, PongGameManager.Instance.CircleRadii.Length);
                    }
                    BonusAngle = Random.Range(0f, 360f);
                    despawnTimer = 5f;
                    Debug.Log($"🟢 [BonusManager] SPAWN BONUS! Cercle: {BonusCircleIndex}, Angle: {BonusAngle}");
                }
            }
            else
            {
                despawnTimer -= Time.deltaTime;
                if (despawnTimer <= 0f)
                {
                    HasBonus = false;
                    spawnTimer = 2f; // Keep at 2s for fast testing
                    Debug.Log("🔴 [BonusManager] Le bonus a disparu (timeout).");
                }
                else
                {
                    CheckCollisions();
                }
            }
            UpdateVisuals(); // Server needs visuals too if it's Host
        }

        void CheckCollisions()
        {
            PongPaddle[] paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);
            foreach (var paddle in paddles)
            {
                if (PongGameManager.Instance.GetCircleIndex(paddle) == BonusCircleIndex)
                {
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(paddle.CurrentAngle, BonusAngle));
                    if (angleDiff < 15f) // Collision threshold (15 degrees is wide enough for a fast paddle)
                    {
                        // Take bonus
                        HasBonus = false;
                        spawnTimer = 10f;
                        StartCoroutine(ApplySpeedAdvantage(paddle));
                        break;
                    }
                }
            }
        }

        IEnumerator ApplySpeedAdvantage(PongPaddle paddle)
        {
            float originalSpeed = paddle.Speed;
            paddle.Speed = originalSpeed * 4f;
            yield return new WaitForSeconds(7f);
            if (paddle != null)
            {
                paddle.Speed = originalSpeed; // Restore speed
            }
        }

        void UpdateVisuals()
        {
            if (HasBonus)
            {
                if (bonusVisual == null)
                {
                    bonusVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bonusVisual.name = "BonusSphere";
                    Destroy(bonusVisual.GetComponent<Collider>()); // No physical collision
                    bonusVisual.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                    Renderer r = bonusVisual.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.material = new Material(Shader.Find("Unlit/Color"));
                        r.material.color = Color.green;
                    }
                }
                bonusVisual.SetActive(true);

                if (PongGameManager.Instance != null && PongGameManager.Instance.CircleRadii != null && BonusCircleIndex < PongGameManager.Instance.CircleRadii.Length)
                {
                    float radius = PongGameManager.Instance.CircleRadii[BonusCircleIndex];
                    float rad = BonusAngle * Mathf.Deg2Rad;
                    bonusVisual.transform.position = PongGameManager.Instance.CenterPoint + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
                }
            }
            else
            {
                if (bonusVisual != null)
                {
                    bonusVisual.SetActive(false);
                }
            }
        }
    }
}
