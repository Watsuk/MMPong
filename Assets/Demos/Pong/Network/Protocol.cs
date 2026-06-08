using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MMPong.Network
{
    /// <summary>Types de messages échangés sur le réseau.</summary>
    public enum MessageType { Input, State, Join, Ready, Welcome, Lobby, Start, End, Ack, Config }

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
    }

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

        /// <summary>Demande de connexion (pseudo nettoyé des séparateurs) + équipe choisie.</summary>
        public static Message BuildJoin(string pseudo, int teamIndex) => new Message
        {
            type = MessageType.Join,
            reliable = true,
            fields = new[] { Sanitize(pseudo), teamIndex.ToString(Inv) }
        };

        public static (string pseudo, int team) ParseJoin(Message m)
            => (m.fields[0], m.fields.Length > 1 ? int.Parse(m.fields[1], Inv) : 0);

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

        /// <summary>
        /// État du lobby diffusé par le serveur : pour chaque slot (indexé par id), pseudo + équipe
        /// + état prêt. Les arrays sont positionnels (longueur = MaxPlayers, "" pour les slots libres).
        /// </summary>
        public static Message BuildLobby(string[] pseudos, int[] teams, bool[] ready) => new Message
        {
            type = MessageType.Lobby,
            reliable = true,
            fields = new[]
            {
                pseudos.Length.ToString(Inv),
                string.Join(ListSep.ToString(), pseudos.Select(Sanitize)),
                string.Join(ListSep.ToString(), teams.Select(t => t.ToString(Inv))),
                string.Join(ListSep.ToString(), ready.Select(r => r ? "1" : "0"))
            }
        };

        /// <summary>Pseudos seuls (compat : nommage des paddles). Slots libres inclus comme "".</summary>
        public static string[] ParseLobby(Message m)
            => m.fields.Length > 1 && m.fields[1].Length > 0
               ? m.fields[1].Split(ListSep)
               : Array.Empty<string>();

        /// <summary>Joueurs occupés du lobby (pseudo + équipe + prêt), pour l'UI de salle d'attente.</summary>
        public static LobbyPlayerInfo[] ParseLobbyDetailed(Message m)
        {
            if (m.fields.Length < 2 || m.fields[1].Length == 0)
                return Array.Empty<LobbyPlayerInfo>();

            string[] pseudos = m.fields[1].Split(ListSep);
            string[] teams = m.fields.Length > 2 && m.fields[2].Length > 0 ? m.fields[2].Split(ListSep) : Array.Empty<string>();
            string[] ready = m.fields.Length > 3 && m.fields[3].Length > 0 ? m.fields[3].Split(ListSep) : Array.Empty<string>();

            var list = new List<LobbyPlayerInfo>();
            for (int i = 0; i < pseudos.Length; i++)
            {
                if (string.IsNullOrEmpty(pseudos[i]))
                    continue;
                list.Add(new LobbyPlayerInfo
                {
                    id = i,
                    pseudo = pseudos[i],
                    team = i < teams.Length ? int.Parse(teams[i], Inv) : 0,
                    ready = i < ready.Length && ready[i] == "1"
                });
            }
            return list.ToArray();
        }

        /// <summary>Configuration de match (host → clients), diffusée de façon fiable.</summary>
        public static Message BuildConfig(MatchSettings s) => new Message
        {
            type = MessageType.Config,
            reliable = true,
            fields = new[]
            {
                s.maxPlayers.ToString(Inv),
                s.winType.ToString(Inv),
                s.targetPoints.ToString(Inv),
                F(s.duration),
                Sanitize(s.teamAName), s.teamASkin.ToString(Inv),
                Sanitize(s.teamBName), s.teamBSkin.ToString(Inv)
            }
        };

        public static MatchSettings ParseConfig(Message m) => new MatchSettings
        {
            maxPlayers = int.Parse(m.fields[0], Inv),
            winType = int.Parse(m.fields[1], Inv),
            targetPoints = int.Parse(m.fields[2], Inv),
            duration = PF(m.fields[3]),
            teamAName = m.fields[4],
            teamASkin = int.Parse(m.fields[5], Inv),
            teamBName = m.fields[6],
            teamBSkin = int.Parse(m.fields[7], Inv)
        };

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
