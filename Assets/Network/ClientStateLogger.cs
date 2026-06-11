using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Abonné de test (jetable) à <see cref="NetworkClient.OnStateReceived"/>. Logge (en
    /// throttlé) la position du paddle du joueur local pour vérifier la boucle bout-en-bout
    /// sans rendu. Sert aussi d'exemple de consommation du contrat. À retirer une fois la
    /// vraie couche jeu branchée.
    /// </summary>
    [RequireComponent(typeof(NetworkClient))]
    public class ClientStateLogger : MonoBehaviour
    {
        NetworkClient client;
        float lastPaddleAngle;
        bool gotState;
        float logTimer;

        void Awake() => client = GetComponent<NetworkClient>();

        void OnEnable() => client.OnStateReceived += OnState;
        void OnDisable() => client.OnStateReceived -= OnState;

        void OnState(GameState state)
        {
            int id = client.PlayerId;
            if (id < 0 || id >= state.paddleAngle.Length) return;
            lastPaddleAngle = state.paddleAngle[id];
            gotState = true;
        }

        void Update()
        {
            logTimer += Time.deltaTime;
            if (gotState && logTimer >= 0.5f)
            {
                logTimer = 0f;
                Debug.Log($"[ClientStateLogger] mon paddle angle = {lastPaddleAngle:F1}");
            }
        }
    }
}
