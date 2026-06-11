using System;
using System.Collections.Generic;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Couche de fiabilité (ACK + ré-émission bornée + déduplication) d'un flux de messages
    /// <c>reliable</c> vers <b>un</b> pair distant, au-dessus du transport UDP best-effort.
    /// Symétrique : le même canal sert à émettre (avec ré-émission jusqu'à l'ACK) et à
    /// recevoir (renvoi d'ACK + dédup). Aucune dépendance à <see cref="UdpTransport"/> ni au
    /// reste d'Unity : la fonction d'envoi est injectée et l'horloge est passée via
    /// <see cref="Tick"/> → testable sans socket.
    /// </summary>
    public sealed class ReliableChannel
    {
        const float RetryInterval = 0.2f;
        const int MaxAttempts = 15;

        struct Pending
        {
            public byte[] bytes;
            public float timer;
            public int attempts;
        }

        readonly Action<byte[]> sendRaw;
        readonly Dictionary<uint, Pending> pending = new Dictionary<uint, Pending>();
        readonly DuplicateFilter inbound = new DuplicateFilter();
        readonly List<uint> scratch = new List<uint>();
        uint seqOut;

        public ReliableChannel(Action<byte[]> sendRaw)
        {
            this.sendRaw = sendRaw;
        }

        /// <summary>Émet un message fiable : lui assigne un seq, le mémorise et l'envoie une première fois.</summary>
        public void SendReliable(Message m)
        {
            m.seq = ++seqOut;
            byte[] bytes = Protocol.Encode(m);
            pending[m.seq] = new Pending { bytes = bytes, timer = 0f, attempts = 1 };
            sendRaw(bytes);
        }

        /// <summary>
        /// Traite un message fiable entrant : renvoie systématiquement un ACK (même sur doublon,
        /// l'émetteur en a besoin), puis indique si le message doit être traité (true) ou ignoré
        /// car déjà vu (false).
        /// </summary>
        public bool ReceiveReliable(Message m)
        {
            sendRaw(Protocol.Encode(Protocol.BuildAck(m.seq)));
            return inbound.IsNew(m.seq);
        }

        /// <summary>Acquitte un message émis : stoppe sa ré-émission.</summary>
        public void HandleAck(uint seq) => pending.Remove(seq);

        /// <summary>Ré-émet les messages non acquittés dont le délai est écoulé ; abandonne après la borne.</summary>
        public void Tick(float dt)
        {
            if (pending.Count == 0) return;

            scratch.Clear();
            scratch.AddRange(pending.Keys);
            foreach (uint seq in scratch)
            {
                Pending p = pending[seq];
                p.timer += dt;
                if (p.timer < RetryInterval) { pending[seq] = p; continue; }

                if (p.attempts >= MaxAttempts)
                {
                    pending.Remove(seq);
                    Debug.LogWarning($"[ReliableChannel] Abandon du message seq={seq} après {MaxAttempts} tentatives.");
                    continue;
                }

                p.timer = 0f;
                p.attempts++;
                pending[seq] = p;
                sendRaw(p.bytes);
            }
        }
    }
}
