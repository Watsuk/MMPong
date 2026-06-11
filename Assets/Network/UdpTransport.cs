using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Wrapper UDP réutilisable au-dessus de <see cref="UdpClient"/>.
    /// Modèle proche d'un WebSocket : <see cref="Send"/> (fire-and-forget) pour émettre,
    /// et l'événement <see cref="OnData"/> pour la réception. Transporte des octets bruts ;
    /// le sens des messages relève de Protocol.cs.
    /// </summary>
    public class UdpTransport : MonoBehaviour
    {
        UdpClient udp;
        IPEndPoint sourceEndPoint;

        /// <summary>Déclenché à chaque datagramme reçu : (données, adresse de l'expéditeur).</summary>
        public event Action<byte[], IPEndPoint> OnData;

        /// <summary>Vrai si le socket est ouvert.</summary>
        public bool IsOpen => udp != null;

        /// <summary>Ouvre le socket et réserve le port d'écoute (Bind).</summary>
        /// <param name="listenPort">Port UDP sur lequel recevoir.</param>
        public void Open(int listenPort)
        {
            if (udp != null)
            {
                Debug.LogWarning("[UdpTransport] Déjà ouvert. Close() d'abord.");
                return;
            }

            try
            {
                udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.ExclusiveAddressUse = false;
                DisableUdpConnReset(udp);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));
                sourceEndPoint = new IPEndPoint(IPAddress.Any, 0);
                Debug.Log($"[UdpTransport] Ouvert et à l'écoute sur le port {listenPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UdpTransport] Échec de l'ouverture sur {listenPort} : {e.Message}");
                Close();
            }
        }

        /// <summary>Envoie des octets vers une destination (fire-and-forget, sans accusé).</summary>
        /// <param name="data">Octets à envoyer.</param>
        /// <param name="destination">Adresse IP + port du destinataire.</param>
        public void Send(byte[] data, IPEndPoint destination)
        {
            if (udp == null)
            {
                Debug.LogWarning("[UdpTransport] Send() appelé alors que le socket est fermé.");
                return;
            }

            try
            {
                udp.Send(data, data.Length, destination);
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[UdpTransport] Erreur d'envoi : {e.Message}");
            }
        }

        void Update()
        {
            if (udp == null) return;

            while (udp.Available > 0)
            {
                try
                {
                    byte[] data = udp.Receive(ref sourceEndPoint);
                    OnData?.Invoke(data, sourceEndPoint);
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // ICMP « port unreachable » d'un pair injoignable/parti (Windows WSAECONNRESET).
                    // Sans conséquence en UDP : on ignore et on continue d'écouter.
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UdpTransport] Erreur de réception : {e.Message}");
                }
            }
        }

        /// <summary>
        /// Désactive SIO_UDP_CONNRESET (Windows) : sans ça, un ICMP « port unreachable » d'un pair
        /// parti fait lever WSAECONNRESET sur la réception suivante et casse la boucle. No-op ailleurs.
        /// </summary>
        static void DisableUdpConnReset(UdpClient client)
        {
            const int SIO_UDP_CONNRESET = -1744830452; // 0x9800000C
            try
            {
                client.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception)
            {
                // IOControl non supporté (macOS/Linux) : le comportement par défaut y est déjà correct.
            }
        }

        /// <summary>Ferme le socket et libère le port.</summary>
        public void Close()
        {
            if (udp != null)
            {
                udp.Close();
                udp = null;
                Debug.Log("[UdpTransport] Fermé.");
            }
        }

        void OnDisable() => Close();
    }
}
