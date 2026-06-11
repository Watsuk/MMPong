using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>Types de messages échangés sur le réseau.</summary>
    public enum MessageType : byte { Input, State, Join, Ready, Welcome, Lobby, Start, End, Ack, Config, Heartbeat, Disconnect }

    /// <summary>
    /// Configuration de match définie par le host, propagée à tous les clients (message CONFIG).
    /// Champs primitifs pour garder la couche réseau indépendante de la couche UI.
    /// </summary>
    public struct MatchSettings
    {
        public int maxPlayers;
        public int winType;       // 0 = Points, 1 = Timer
        public int targetPoints;
        public float duration;
        public string teamAName;
        public int teamASkin;
        public string teamBName;
        public int teamBSkin;
    }

    /// <summary>État d'un joueur du lobby tel que diffusé par le serveur (message LOBBY enrichi).</summary>
    public struct LobbyPlayerInfo
    {
        public int id;
        public string pseudo;
        public int team;
        public bool ready;
        /// <summary>Vrai tant que le serveur reçoit les battements de cœur du joueur ; faux après timeout.</summary>
        public bool connected;
    }

    /// <summary>
    /// Enveloppe d'un message : en-tête (type, seq, reliable) + payload binaire.
    /// Le payload est opaque : seul Protocol sait lire/écrire chaque type.
    /// </summary>
    public struct Message
    {
        public MessageType type;
        public uint seq;
        public bool reliable;
        /// <summary>Payload binaire spécifique au type de message.</summary>
        public byte[] payload;
    }

    /// <summary>
    /// (Dé)sérialisation <b>binaire</b> des messages réseau.
    /// Format du fil (little-endian) :
    /// <c>[type:1][seq:4][reliable:1][payload:N]</c>
    /// Le code de jeu passe par les helpers <c>Build*</c>/<c>Parse*</c> ;
    /// la couche réseau par <see cref="Encode"/>/<see cref="Decode"/>.
    ///
    /// <b>Algorithme de sérialisation</b> :
    /// Chaque champ est écrit dans le flux binaire à une taille fixe (byte, int, float)
    /// via BinaryWriter (little-endian). Les tableaux de taille variable sont précédés
    /// d'un compteur (byte) puis de leurs éléments. Les chaînes sont préfixées par leur
    /// longueur en octets (ushort) suivie du contenu UTF-8 brut.
    /// Avantage : zéro parsing de texte, taille réduite (un float = 4 octets fixes au
    /// lieu de ~8-12 caractères ASCII), pas d'allocation de string[] intermédiaire.
    /// </summary>
    public static class Protocol
    {
        const char FieldSep = '|';
        const char ListSep = ',';

        // ========== En-tête fixe : 3 octets ==========
        // [0]     type:4 | reliable:1 | reserved:3 (1 octet)
        // [1..2]  seq         (2 octets, ushort LE)

        const int HeaderSize = 3;

        // ---------- Enveloppe ----------

        /// <summary>Sérialise un message en octets binaires (en-tête + payload).</summary>
        public static byte[] Encode(Message m)
        {
            int payloadLen = m.payload != null ? m.payload.Length : 0;
            byte[] buf = new byte[HeaderSize + payloadLen];

            // En-tête
            byte typeAndReliable = (byte)((byte)m.type & 0x0F);
            if (m.reliable) typeAndReliable |= 0x10;
            buf[0] = typeAndReliable;
            ushort shortSeq = (ushort)m.seq;
            buf[1] = (byte)(shortSeq);
            buf[2] = (byte)(shortSeq >> 8);

            // Payload
            if (payloadLen > 0)
                Buffer.BlockCopy(m.payload, 0, buf, HeaderSize, payloadLen);

            return buf;
        }

        /// <summary>Désérialise des octets binaires en message.</summary>
        public static Message Decode(byte[] data)
        {
            if (data.Length < HeaderSize)
                throw new ArgumentException($"Paquet trop court ({data.Length} < {HeaderSize})");

            var m = new Message
            {
                type = (MessageType)(data[0] & 0x0F),
                reliable = (data[0] & 0x10) != 0,
                seq = (uint)(data[1] | (data[2] << 8))
            };

            int payloadLen = data.Length - HeaderSize;
            if (payloadLen > 0)
            {
                m.payload = new byte[payloadLen];
                Buffer.BlockCopy(data, HeaderSize, m.payload, 0, payloadLen);
            }
            else
            {
                m.payload = Array.Empty<byte>();
            }

            return m;
        }

        // ========== Helpers d'écriture/lecture binaire ==========

        static void WriteFloat(MemoryStream ms, float v)
        {
            byte[] b = BitConverter.GetBytes(v);
            ms.Write(b, 0, 4);
        }

        static void WriteInt(MemoryStream ms, int v)
        {
            byte[] b = BitConverter.GetBytes(v);
            ms.Write(b, 0, 4);
        }

        static void WriteUShort(MemoryStream ms, ushort v)
        {
            ms.WriteByte((byte)v);
            ms.WriteByte((byte)(v >> 8));
        }

        static void WriteString(MemoryStream ms, string s)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(s ?? "");
            WriteUShort(ms, (ushort)utf8.Length);
            if (utf8.Length > 0)
                ms.Write(utf8, 0, utf8.Length);
        }

        static float ReadFloat(MemoryStream ms)
        {
            byte[] b = new byte[4];
            ms.Read(b, 0, 4);
            return BitConverter.ToSingle(b, 0);
        }

        static int ReadInt(MemoryStream ms)
        {
            byte[] b = new byte[4];
            ms.Read(b, 0, 4);
            return BitConverter.ToInt32(b, 0);
        }

        static ushort ReadUShort(MemoryStream ms)
        {
            int lo = ms.ReadByte();
            int hi = ms.ReadByte();
            return (ushort)(lo | (hi << 8));
        }

        static string ReadString(MemoryStream ms)
        {
            ushort len = ReadUShort(ms);
            if (len == 0) return "";
            byte[] buf = new byte[len];
            ms.Read(buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        // ========== Helpers par message ==========

        static void WriteShort(MemoryStream ms, short v)
        {
            ms.WriteByte((byte)v);
            ms.WriteByte((byte)(v >> 8));
        }

        static short ReadShort(MemoryStream ms)
        {
            int lo = ms.ReadByte();
            int hi = ms.ReadByte();
            return (short)(lo | (hi << 8));
        }

        // Angle [0, 360) -> ushort [0, 65535]
        static ushort QuantizeAngle(float deg)
        {
            float n = Mathf.Repeat(deg, 360f);
            return (ushort)(n * 65535f / 360f);
        }

        static float DequantizeAngle(ushort q)
        {
            return (q * 360f) / 65535f;
        }

        // Pos -> short (x1000, clamp +/-32767)
        static short QuantizePos(float v)
        {
            return (short)Mathf.Clamp(v * 1000f, -32768f, 32767f);
        }

        static float DequantizePos(short q)
        {
            return q / 1000f;
        }

        // Speed -> ushort (x100)
        static ushort QuantizeSpeed(float v)
        {
            return (ushort)Mathf.Clamp(v * 100f, 0f, 65535f);
        }

        static float DequantizeSpeed(ushort q)
        {
            return q / 100f;
        }

        // Dir (Vector2) -> 1 byte (angle 0-255)
        static byte QuantizeDir(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float n = Mathf.Repeat(angle, 360f);
            return (byte)(n * 255f / 360f);
        }

        static Vector2 DequantizeDir(byte q)
        {
            float angle = (q * 360f) / 255f;
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        // ---------- Input ----------
        // Payload : [playerId:1][dir:1] = 2 octets

        /// <summary>Input d'un joueur : direction verticale (-1, 0, 1).</summary>
        public static Message BuildInput(int playerId, float dir)
        {
            using (var ms = new MemoryStream(2))
            {
                ms.WriteByte((byte)playerId);
                sbyte d = (sbyte)(dir > 0.5f ? 1 : (dir < -0.5f ? -1 : 0));
                ms.WriteByte((byte)d);
                return new Message { type = MessageType.Input, reliable = false, payload = ms.ToArray() };
            }
        }

        public static (int playerId, float dir) ParseInput(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
            {
                int id = ms.ReadByte();
                sbyte d = (sbyte)ms.ReadByte();
                float dir = d;
                return (id, dir);
            }
        }

        // ---------- State ----------
        // Payload : [ballPos.x:2][ballPos.y:2][ballOwner:1][phase:1][winner:1]
        //           [paddleCount:1][paddleAngle0:2]...[scoreCount:1][score0:1]...
        //           [hasBonus:1][bonusCircleIndex:1][bonusAngle:2][bonusWinner:1]

        /// <summary>Snapshot d'état (le <c>seq</c> du message reprend celui du GameState).</summary>
        public static Message BuildState(GameState s)
        {
            using (var ms = new MemoryStream(64))
            {
                // Balle
                WriteShort(ms, QuantizePos(s.ballPos.x));
                WriteShort(ms, QuantizePos(s.ballPos.y));
                ms.WriteByte((byte)s.ballOwner);

                // Phase & winner
                ms.WriteByte((byte)s.phase);
                ms.WriteByte((byte)s.winner);

                // Paddles (tableau variable précédé de son compteur)
                byte paddleCount = (byte)(s.paddleAngle != null ? s.paddleAngle.Length : 0);
                ms.WriteByte(paddleCount);
                for (int i = 0; i < paddleCount; i++)
                    WriteUShort(ms, QuantizeAngle(s.paddleAngle[i]));

                // Scores (tableau variable précédé de son compteur)
                byte scoreCount = (byte)(s.scores != null ? s.scores.Length : 0);
                ms.WriteByte(scoreCount);
                for (int i = 0; i < scoreCount; i++)
                    ms.WriteByte((byte)s.scores[i]);

                // Bonus
                ms.WriteByte(s.hasBonus ? (byte)1 : (byte)0);
                ms.WriteByte((byte)s.bonusCircleIndex);
                WriteUShort(ms, QuantizeAngle(s.bonusAngle));
                ms.WriteByte((byte)s.bonusWinner);

                // Balle direction & vitesse
                ms.WriteByte(QuantizeDir(s.ballDir));
                WriteUShort(ms, QuantizeSpeed(s.ballSpeed));

                return new Message { type = MessageType.State, seq = s.seq, reliable = false, payload = ms.ToArray() };
            }
        }

        public static GameState ParseState(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
            {
                var s = new GameState { seq = m.seq };

                s.ballPos = new Vector2(DequantizePos(ReadShort(ms)), DequantizePos(ReadShort(ms)));
                s.ballOwner = (sbyte)ms.ReadByte();

                s.phase = (GamePhase)ms.ReadByte();
                s.winner = (sbyte)ms.ReadByte();

                byte paddleCount = (byte)ms.ReadByte();
                s.paddleAngle = new float[paddleCount];
                for (int i = 0; i < paddleCount; i++)
                    s.paddleAngle[i] = DequantizeAngle(ReadUShort(ms));

                byte scoreCount = (byte)ms.ReadByte();
                s.scores = new int[scoreCount];
                for (int i = 0; i < scoreCount; i++)
                    s.scores[i] = ms.ReadByte();

                s.hasBonus = ms.ReadByte() != 0;
                s.bonusCircleIndex = ms.ReadByte();
                s.bonusAngle = DequantizeAngle(ReadUShort(ms));
                s.bonusWinner = (sbyte)ms.ReadByte();

                // Balle direction & vitesse
                s.ballDir = DequantizeDir((byte)ms.ReadByte());
                s.ballSpeed = DequantizeSpeed(ReadUShort(ms));

                return s;
            }
        }

        // ---------- Join ----------
        // Payload : [pseudoLen:2][pseudo:N][team:4]

        public static Message BuildJoin(string pseudo, int team)
        {
            using (var ms = new MemoryStream(32))
            {
                WriteString(ms, Sanitize(pseudo));
                WriteInt(ms, team);
                return new Message { type = MessageType.Join, reliable = true, payload = ms.ToArray() };
            }
        }

        public static (string pseudo, int team) ParseJoin(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
            {
                string pseudo = ReadString(ms);
                int team = ms.Position < ms.Length ? ReadInt(ms) : 0; // tolérant à un ancien JOIN sans équipe
                return (pseudo, team);
            }
        }

        // ---------- Ready ----------
        // Payload : [playerId:4]

        public static Message BuildReady(int playerId)
        {
            using (var ms = new MemoryStream(4))
            {
                WriteInt(ms, playerId);
                return new Message { type = MessageType.Ready, reliable = true, payload = ms.ToArray() };
            }
        }

        public static int ParseReady(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
                return ReadInt(ms);
        }

        // ---------- Heartbeat ----------
        // Payload : [playerId:4]

        /// <summary>
        /// Battement de cœur : le client signale périodiquement qu'il est toujours là (best-effort).
        /// Le serveur en déduit l'état de connexion ; au-delà du timeout sans battement, le joueur
        /// est marqué déconnecté.
        /// </summary>
        public static Message BuildHeartbeat(int playerId)
        {
            using (var ms = new MemoryStream(4))
            {
                WriteInt(ms, playerId);
                return new Message { type = MessageType.Heartbeat, reliable = false, payload = ms.ToArray() };
            }
        }

        public static int ParseHeartbeat(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
                return ReadInt(ms);
        }

        // ---------- Welcome ----------
        // Payload : [playerId:4]

        /// <summary>Attribution d'un identifiant joueur par le serveur.</summary>
        public static Message BuildWelcome(int playerId)
        {
            using (var ms = new MemoryStream(4))
            {
                WriteInt(ms, playerId);
                return new Message { type = MessageType.Welcome, reliable = true, payload = ms.ToArray() };
            }
        }

        public static int ParseWelcome(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
                return ReadInt(ms);
        }

        // ---------- Lobby ----------
        // Payload : [count:1] puis par joueur : [pseudoLen:2][pseudo:N][team:4][ready:1][connected:1]
        // Positionnel : index = playerId, slots libres encodés avec un pseudo vide.

        /// <summary>
        /// État du lobby diffusé par le serveur : pour chaque slot (indexé par id), pseudo + équipe
        /// + état prêt + état de connexion (battements de cœur). Les arrays sont positionnels
        /// (longueur = MaxPlayers, "" pour les slots libres). <paramref name="connected"/> null →
        /// tous considérés connectés (compat).
        /// </summary>
        public static Message BuildLobby(string[] pseudos, int[] teams, bool[] ready, bool[] connected = null)
        {
            using (var ms = new MemoryStream(96))
            {
                byte count = (byte)(pseudos != null ? pseudos.Length : 0);
                ms.WriteByte(count);
                for (int i = 0; i < count; i++)
                {
                    WriteString(ms, Sanitize(pseudos[i]));
                    WriteInt(ms, teams != null && i < teams.Length ? teams[i] : 0);
                    ms.WriteByte(ready != null && i < ready.Length && ready[i] ? (byte)1 : (byte)0);
                    bool isConnected = connected == null || (i < connected.Length && connected[i]);
                    ms.WriteByte(isConnected ? (byte)1 : (byte)0);
                }
                return new Message { type = MessageType.Lobby, reliable = true, payload = ms.ToArray() };
            }
        }

        /// <summary>Pseudos seuls (compat : nommage des paddles). Slots libres inclus comme "".</summary>
        public static string[] ParseLobby(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
            {
                byte count = (byte)ms.ReadByte();
                string[] pseudos = new string[count];
                for (int i = 0; i < count; i++)
                {
                    pseudos[i] = ReadString(ms);
                    ReadInt(ms);        // team (ignorée ici)
                    ms.ReadByte();      // ready (ignoré ici)
                    ms.ReadByte();      // connected (ignoré ici)
                }
                return pseudos;
            }
        }

        /// <summary>Joueurs occupés du lobby (pseudo + équipe + prêt + connexion), pour l'UI de salle d'attente.</summary>
        public static LobbyPlayerInfo[] ParseLobbyDetailed(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
            {
                byte count = (byte)ms.ReadByte();
                var list = new List<LobbyPlayerInfo>(count);
                for (int i = 0; i < count; i++)
                {
                    string pseudo = ReadString(ms);
                    int team = ReadInt(ms);
                    bool ready = ms.ReadByte() != 0;
                    bool connected = ms.ReadByte() != 0;
                    if (string.IsNullOrEmpty(pseudo))
                        continue;
                    list.Add(new LobbyPlayerInfo { id = i, pseudo = pseudo, team = team, ready = ready, connected = connected });
                }
                return list.ToArray();
            }
        }

        // ---------- Config ----------
        // Payload : [maxPlayers:4][winType:4][targetPoints:4][duration:4]
        //           [teamAName str][teamASkin:4][teamBName str][teamBSkin:4]

        /// <summary>Configuration de match (host → clients), diffusée de façon fiable.</summary>
        public static Message BuildConfig(MatchSettings s)
        {
            using (var ms = new MemoryStream(48))
            {
                WriteInt(ms, s.maxPlayers);
                WriteInt(ms, s.winType);
                WriteInt(ms, s.targetPoints);
                WriteFloat(ms, s.duration);
                WriteString(ms, Sanitize(s.teamAName));
                WriteInt(ms, s.teamASkin);
                WriteString(ms, Sanitize(s.teamBName));
                WriteInt(ms, s.teamBSkin);
                return new Message { type = MessageType.Config, reliable = true, payload = ms.ToArray() };
            }
        }

        public static MatchSettings ParseConfig(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
            {
                return new MatchSettings
                {
                    maxPlayers = ReadInt(ms),
                    winType = ReadInt(ms),
                    targetPoints = ReadInt(ms),
                    duration = ReadFloat(ms),
                    teamAName = ReadString(ms),
                    teamASkin = ReadInt(ms),
                    teamBName = ReadString(ms),
                    teamBSkin = ReadInt(ms)
                };
            }
        }

        // ---------- Start ----------
        // Payload : vide (0 octet)

        /// <summary>Démarrage de la partie (aucun payload).</summary>
        public static Message BuildStart() => new Message
        {
            type = MessageType.Start,
            reliable = true,
            payload = Array.Empty<byte>()
        };

        // ---------- End ----------
        // Payload : [winnerId:4]

        /// <summary>Fin de partie : identifiant du gagnant.</summary>
        public static Message BuildEnd(int winnerId)
        {
            using (var ms = new MemoryStream(4))
            {
                WriteInt(ms, winnerId);
                return new Message { type = MessageType.End, reliable = true, payload = ms.ToArray() };
            }
        }

        public static int ParseEnd(Message m)
        {
            using (var ms = new MemoryStream(m.payload))
                return ReadInt(ms);
        }

        // ---------- Ack ----------
        // Payload : [ackedSeq:4]

        /// <summary>Accusé de réception d'un message fiable.</summary>
        public static Message BuildAck(uint ackedSeq)
        {
            byte[] p = BitConverter.GetBytes(ackedSeq);
            return new Message { type = MessageType.Ack, reliable = false, payload = p };
        }

        public static uint ParseAck(Message m)
        {
            return BitConverter.ToUInt32(m.payload, 0);
        }

        // ---------- Disconnect ----------
        // Payload : vide (0 octet)

        /// <summary>Message de déconnexion ou fermeture du salon.</summary>
        public static Message BuildDisconnect() => new Message
        {
            type = MessageType.Disconnect,
            reliable = false,
            payload = Array.Empty<byte>()
        };

        // ---------- Utilitaires ----------

        static string Sanitize(string s) => s.Replace(FieldSep.ToString(), "").Replace(ListSep.ToString(), "");
    }
}
