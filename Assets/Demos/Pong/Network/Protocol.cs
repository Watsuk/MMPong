using System;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>Types de messages échangés sur le réseau.</summary>
    public enum MessageType { Input, State, Join, Ready, Welcome, Lobby, Start, End, Ack }

    /// <summary>
    /// Enveloppe d'un message : en-tête (type, seq, reliable) + payload (fields).
    /// </summary>
    public struct Message
    {
        public MessageType type;
        public uint seq;
        public bool reliable;
        public string[] fields;
    }

    /// <summary>
    /// (Dé)sérialisation texte des messages réseau.
    /// Format du fil : <c>type|seq|reliable|champ0|champ1|...</c> (UTF-8).
    /// Le code de jeu passe par les helpers <c>Build*</c>/<c>Parse*</c> ;
    /// la couche réseau par <see cref="Encode"/>/<see cref="Decode"/>.
    /// </summary>
    public static class Protocol
    {
        const char FieldSep = '|';
        const char ListSep = ',';
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------- Enveloppe ----------

        /// <summary>Sérialise un message en octets UTF-8.</summary>
        public static byte[] Encode(Message m)
        {
            var sb = new StringBuilder();
            sb.Append(m.type.ToString());
            sb.Append(FieldSep).Append(m.seq.ToString(Inv));
            sb.Append(FieldSep).Append(m.reliable ? '1' : '0');
            if (m.fields != null)
                foreach (var f in m.fields)
                    sb.Append(FieldSep).Append(f);
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>Désérialise des octets UTF-8 en message.</summary>
        public static Message Decode(byte[] data)
        {
            string[] parts = Encoding.UTF8.GetString(data).Split(FieldSep);
            return new Message
            {
                type = (MessageType)Enum.Parse(typeof(MessageType), parts[0]),
                seq = uint.Parse(parts[1], Inv),
                reliable = parts[2] == "1",
                fields = parts.Skip(3).ToArray()
            };
        }

        // ---------- Helpers par message ----------

        /// <summary>Input d'un joueur : direction verticale (-1, 0, 1).</summary>
        public static Message BuildInput(int playerId, float dir) => new Message
        {
            type = MessageType.Input,
            reliable = false,
            fields = new[] { playerId.ToString(Inv), F(dir) }
        };

        public static (int playerId, float dir) ParseInput(Message m)
            => (int.Parse(m.fields[0], Inv), PF(m.fields[1]));

        /// <summary>Snapshot d'état (le <c>seq</c> du message reprend celui du GameState).</summary>
        public static Message BuildState(GameState s) => new Message
        {
            type = MessageType.State,
            seq = s.seq,
            reliable = false,
            fields = new[]
            {
                F(s.ballPos.x), F(s.ballPos.y),
                s.ballOwner.ToString(Inv),
                ((byte)s.phase).ToString(Inv),
                s.winner.ToString(Inv),
                string.Join(ListSep.ToString(), s.paddleAngle.Select(F)),
                string.Join(ListSep.ToString(), s.scores.Select(v => v.ToString(Inv))),
                s.hasBonus ? "1" : "0",
                s.bonusCircleIndex.ToString(Inv),
                F(s.bonusAngle)
            }
        };

        public static GameState ParseState(Message m) => new GameState
        {
            seq = m.seq,
            ballPos = new Vector2(PF(m.fields[0]), PF(m.fields[1])),
            ballOwner = int.Parse(m.fields[2], Inv),
            phase = (GamePhase)byte.Parse(m.fields[3], Inv),
            winner = int.Parse(m.fields[4], Inv),
            paddleAngle = m.fields[5].Length > 0
                ? m.fields[5].Split(ListSep).Select(PF).ToArray()
                : Array.Empty<float>(),
            scores = m.fields[6].Length > 0
                ? m.fields[6].Split(ListSep).Select(v => int.Parse(v, Inv)).ToArray()
                : Array.Empty<int>(),
            hasBonus = m.fields.Length > 7 && m.fields[7] == "1",
            bonusCircleIndex = m.fields.Length > 8 ? int.Parse(m.fields[8], Inv) : 0,
            bonusAngle = m.fields.Length > 9 ? PF(m.fields[9]) : 0f
        };

        /// <summary>Demande de connexion (pseudo nettoyé des séparateurs).</summary>
        public static Message BuildJoin(string pseudo) => new Message
        {
            type = MessageType.Join,
            reliable = true,
            fields = new[] { Sanitize(pseudo) }
        };

        public static string ParseJoin(Message m) => m.fields[0];

        public static Message BuildReady(int playerId) => new Message
        {
            type = MessageType.Ready,
            reliable = true,
            fields = new[] { playerId.ToString(Inv) }
        };

        public static int ParseReady(Message m) => int.Parse(m.fields[0], Inv);

        /// <summary>Attribution d'un identifiant joueur par le serveur.</summary>
        public static Message BuildWelcome(int playerId) => new Message
        {
            type = MessageType.Welcome,
            reliable = true,
            fields = new[] { playerId.ToString(Inv) }
        };

        public static int ParseWelcome(Message m) => int.Parse(m.fields[0], Inv);

        /// <summary>Liste des joueurs du lobby (séparée par des virgules).</summary>
        public static Message BuildLobby(string[] pseudos) => new Message
        {
            type = MessageType.Lobby,
            reliable = true,
            fields = new[]
            {
                pseudos.Length.ToString(Inv),
                string.Join(ListSep.ToString(), pseudos.Select(Sanitize))
            }
        };

        public static string[] ParseLobby(Message m)
            => m.fields.Length > 1 && m.fields[1].Length > 0
               ? m.fields[1].Split(ListSep)
               : Array.Empty<string>();

        /// <summary>Démarrage de la partie (aucun payload).</summary>
        public static Message BuildStart() => new Message
        {
            type = MessageType.Start,
            reliable = true,
            fields = Array.Empty<string>()
        };

        /// <summary>Fin de partie : identifiant du gagnant.</summary>
        public static Message BuildEnd(int winnerId) => new Message
        {
            type = MessageType.End,
            reliable = true,
            fields = new[] { winnerId.ToString(Inv) }
        };

        public static int ParseEnd(Message m) => int.Parse(m.fields[0], Inv);

        /// <summary>Accusé de réception d'un message fiable.</summary>
        public static Message BuildAck(uint ackedSeq) => new Message
        {
            type = MessageType.Ack,
            reliable = false,
            fields = new[] { ackedSeq.ToString(Inv) }
        };

        public static uint ParseAck(Message m) => uint.Parse(m.fields[0], Inv);

        // ---------- Utilitaires ----------

        static string F(float v) => v.ToString("R", Inv);
        static float PF(string s) => float.Parse(s, NumberStyles.Float, Inv);
        static string Sanitize(string s) => s.Replace(FieldSep.ToString(), "").Replace(ListSep.ToString(), "");
    }
}
