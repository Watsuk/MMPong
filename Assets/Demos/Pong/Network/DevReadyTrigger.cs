using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Déclencheur de lobby <b>temporaire</b> : envoie <c>Ready</c> au serveur sur appui d'une
    /// touche. Remplace le futur bouton « Prêt » de l'écran lobby (UI-2) le temps que celui-ci
    /// n'existe pas — à retirer une fois UI-2 livré (même statut que l'ex-DevNetLauncher).
    /// </summary>
    public class DevReadyTrigger : MonoBehaviour
    {
        public NetworkClient client;
        public KeyCode readyKey = KeyCode.Space;

        bool sent;

        void Update()
        {
            if (sent || client == null) return;
            if (Input.GetKeyDown(readyKey))
            {
                client.SendReady();
                sent = true;
                Debug.Log("[DevReadyTrigger] READY envoyé.");
            }
        }
    }
}
