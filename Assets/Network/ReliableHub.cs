using System;
using System.Collections.Generic;
using System.Net;

namespace MMPong.Network
{
    /// <summary>
    /// Orchestration de la fiabilité sur N pairs : tient un <see cref="ReliableChannel"/> par
    /// endpoint et expose les opérations fiables agrégées (émission, diffusion, ACK, réception,
    /// ré-émission). Analogue multi-pairs du canal unique. Dépend d'un délégué d'envoi injecté
    /// (DIP) plutôt que de <see cref="UdpTransport"/> → testable sans socket.
    /// </summary>
    public sealed class ReliableHub
    {
        readonly Action<byte[], IPEndPoint> sendRaw;
        readonly Dictionary<IPEndPoint, ReliableChannel> channels = new Dictionary<IPEndPoint, ReliableChannel>();

        public ReliableHub(Action<byte[], IPEndPoint> sendRaw)
        {
            this.sendRaw = sendRaw;
        }

        /// <summary>Émet un message fiable vers un pair (ré-émis jusqu'à l'ACK).</summary>
        public void SendReliable(IPEndPoint to, Message m) => ChannelFor(to).SendReliable(m);

        /// <summary>Émet un message fiable vers plusieurs pairs (chacun avec son propre seq).</summary>
        public void BroadcastReliable(IEnumerable<IPEndPoint> tos, Message m)
        {
            foreach (var ep in tos) ChannelFor(ep).SendReliable(m);
        }

        /// <summary>Acquitte un message émis vers un pair (stoppe sa ré-émission).</summary>
        public void HandleAck(IPEndPoint from, uint seq) => ChannelFor(from).HandleAck(seq);

        /// <summary>Traite un message fiable entrant : renvoie l'ACK et indique s'il est nouveau (à traiter).</summary>
        public bool ReceiveReliable(IPEndPoint from, Message m) => ChannelFor(from).ReceiveReliable(m);

        /// <summary>Fait avancer la ré-émission de tous les canaux.</summary>
        public void TickAll(float dt)
        {
            foreach (var ch in channels.Values) ch.Tick(dt);
        }

        ReliableChannel ChannelFor(IPEndPoint ep)
        {
            if (!channels.TryGetValue(ep, out var ch))
            {
                ch = new ReliableChannel(bytes => sendRaw(bytes, ep));
                channels[ep] = ch;
            }
            return ch;
        }
    }
}
