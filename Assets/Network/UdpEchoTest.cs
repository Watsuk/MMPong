using System.Net;
using System.Text;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Pilier 1 — Test "echo loopback" (JETABLE, à retirer une fois validé).
    ///
    /// But : prouver que le tuyau UdpTransport fonctionne, dans l'environnement le plus
    /// contrôlé possible : une seule instance qui s'envoie un message À ELLE-MÊME via
    /// l'adresse loopback 127.0.0.1.
    ///
    /// Si "reçu : hello" s'affiche dans la console → le pilier 1 est validé.
    ///
    /// Mise en place :
    ///   1. Créer un GameObject vide "Network".
    ///   2. Lui ajouter le composant UdpTransport ET ce composant UdpEchoTest.
    ///   3. Glisser le UdpTransport dans le champ "transport" ci-dessous.
    ///   4. Appuyer sur Play.
    /// </summary>
    [RequireComponent(typeof(UdpTransport))]
    public class UdpEchoTest : MonoBehaviour
    {
        [Tooltip("Le wrapper UDP à tester. Auto-rempli si laissé vide.")]
        public UdpTransport transport;

        [Tooltip("Port d'écoute ET de destination (loopback vers soi-même).")]
        public int port = 25000;

        [Tooltip("Message à s'envoyer à soi-même.")]
        public string message = "Salut !";

        void Start()
        {
            // Si on a oublié de glisser la référence, on la récupère sur le même GameObject.
            if (transport == null)
                transport = GetComponent<UdpTransport>();

            // 1) On écoute les messages entrants.
            transport.OnData += OnDataReceived;

            // 2) On ouvre le socket sur notre port.
            transport.Open(port);

            // 3) On s'envoie "hello" à NOUS-MÊME.
            //    IPAddress.Loopback = 127.0.0.1 = "cette machine". Le datagramme ne
            //    sort jamais sur le vrai réseau : il revient directement à notre socket.
            var destination = new IPEndPoint(IPAddress.Loopback, port);
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            transport.Send(bytes, destination);

            Debug.Log($"[UdpEchoTest] Message envoyé : \"{message}\" → 127.0.0.1:{port}");
        }

        // Appelé par UdpTransport à chaque datagramme reçu.
        void OnDataReceived(byte[] data, IPEndPoint from)
        {
            string text = Encoding.UTF8.GetString(data);
            Debug.Log($"reçu : {text}   (de {from})");
        }

        void OnDisable()
        {
            // Bonne hygiène : on se désabonne pour éviter les fuites d'événement.
            if (transport != null)
                transport.OnData -= OnDataReceived;
        }
    }
}
