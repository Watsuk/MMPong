using UnityEngine;
using System.Collections;

/// <summary>
/// Composant à attacher sur la caméra principale. Fournit un effet de secousse
/// (camera shake) déclenché par les événements de jeu (collision rapide, point marqué).
/// S'attache automatiquement à la caméra principale si aucune n'est assignée.
/// Utilisable en mode Local, Host et Client (effet purement visuel/local).
/// </summary>
public class CameraShaker : MonoBehaviour
{
    public static CameraShaker Instance { get; private set; }

    [Header("Paramètres de secousse")]
    [Tooltip("Intensité maximale du déplacement (en unités Unity)")]
    public float defaultIntensity = 0.15f;

    [Tooltip("Durée par défaut de la secousse (en secondes)")]
    public float defaultDuration = 0.15f;

    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        originalPosition = transform.localPosition;
    }

    /// <summary>Déclenche une secousse avec les paramètres par défaut.</summary>
    public void Shake()
    {
        Shake(defaultIntensity, defaultDuration);
    }

    /// <summary>Déclenche une secousse avec une intensité personnalisée.</summary>
    public void Shake(float intensity, float duration)
    {
        // Si une secousse est déjà en cours, on la relance (pas de cumul)
        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
    }

    IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Déplacement aléatoire avec fondu progressif (diminue vers la fin)
            float remaining = 1f - (elapsed / duration);
            float offsetX = Random.Range(-1f, 1f) * intensity * remaining;
            float offsetY = Random.Range(-1f, 1f) * intensity * remaining;

            transform.localPosition = originalPosition + new Vector3(offsetX, offsetY, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Remet la caméra exactement à sa position d'origine
        transform.localPosition = originalPosition;
        shakeCoroutine = null;
    }
}
