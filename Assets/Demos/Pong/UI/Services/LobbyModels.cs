using System.Net;
using System.Net.Sockets;

namespace MMPong.UI
{
    /// <summary>Type de condition de victoire d'un match.</summary>
    public enum WinConditionType
    {
        Points, // Premier(s) à atteindre une cible de points
        Timer   // Meilleur score à la fin d'une durée
    }

    /// <summary>Condition de fin de match (points cible et/ou durée).</summary>
    public class WinCondition
    {
        public WinConditionType Type = WinConditionType.Points;
        public int TargetPoints = 5;
        public float DurationSeconds = 120f;
    }

    /// <summary>Configuration d'une équipe : nom affiché + skin de paddle associé.</summary>
    public class TeamConfig
    {
        public string Name = "";
        public int SkinId;

        public TeamConfig() { }

        public TeamConfig(string name, int skinId)
        {
            Name = name;
            SkinId = skinId;
        }
    }

    /// <summary>
    /// Configuration complète d'un match, définie par le host avant l'ouverture du salon
    /// et transportée via <see cref="UISession"/>. Frontière fonctionnelle entre l'UI et
    /// le futur branchement réseau.
    /// </summary>
    public class MatchConfig
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 6;

        public int MaxPlayerCount = 4;
        public WinCondition WinCondition = new WinCondition();
        public TeamConfig TeamA = new TeamConfig("Rouge", 0);
        public TeamConfig TeamB = new TeamConfig("Bleu", 1);

        /// <summary>
        /// Valide la configuration. Renvoie false et un message si quelque chose empêche
        /// d'ouvrir le salon (joueurs hors bornes, noms d'équipes vides, skins identiques,
        /// condition de victoire invalide).
        /// </summary>
        public bool IsValid(out string error)
        {
            if (MaxPlayerCount < MinPlayers || MaxPlayerCount > MaxPlayers)
            {
                error = $"Le nombre de joueurs doit être entre {MinPlayers} et {MaxPlayers}.";
                return false;
            }

            if (TeamA == null || TeamB == null ||
                string.IsNullOrWhiteSpace(TeamA.Name) || string.IsNullOrWhiteSpace(TeamB.Name))
            {
                error = "Les deux équipes doivent avoir un nom.";
                return false;
            }

            if (TeamA.SkinId == TeamB.SkinId)
            {
                error = "Les deux équipes doivent avoir des skins distincts.";
                return false;
            }

            if (WinCondition == null)
            {
                error = "Condition de victoire manquante.";
                return false;
            }

            if (WinCondition.Type == WinConditionType.Points && WinCondition.TargetPoints <= 0)
            {
                error = "La cible de points doit être supérieure à 0.";
                return false;
            }

            if (WinCondition.Type == WinConditionType.Timer && WinCondition.DurationSeconds <= 0f)
            {
                error = "La durée doit être supérieure à 0.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Génère une configuration aléatoire valide (utilisée par la partie locale rapide).
        /// </summary>
        public static MatchConfig CreateRandom()
        {
            bool timer = UnityEngine.Random.value > 0.5f;
            return new MatchConfig
            {
                MaxPlayerCount = UnityEngine.Random.Range(MinPlayers, MaxPlayers + 1),
                WinCondition = new WinCondition
                {
                    Type = timer ? WinConditionType.Timer : WinConditionType.Points,
                    TargetPoints = UnityEngine.Random.Range(3, 11),
                    DurationSeconds = UnityEngine.Random.Range(60, 181)
                },
                TeamA = new TeamConfig("Rouge", 0),
                TeamB = new TeamConfig("Bleu", 1)
            };
        }
    }

    /// <summary>Utilitaires réseau côté UI (affichage informatif uniquement).</summary>
    public static class NetworkUtils
    {
        /// <summary>
        /// Renvoie la première adresse IPv4 locale (pour afficher l'IP du salon au host).
        /// Retombe sur "127.0.0.1" si rien n'est trouvé.
        /// </summary>
        public static string LocalIPv4()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch
            {
                // Environnement sans réseau : on retombe sur le loopback.
            }
            return "127.0.0.1";
        }
    }

    /// <summary>État d'un joueur dans le salon, tel qu'affiché par l'UI.</summary>
    public class PlayerInfo
    {
        public int Id;
        public string Pseudo;
        public int TeamIndex;
        public bool IsReady;
        public bool IsConnected;

        public PlayerInfo() { }

        public PlayerInfo(int id, string pseudo, int teamIndex = 0, bool isReady = false, bool isConnected = true)
        {
            Id = id;
            Pseudo = pseudo;
            TeamIndex = teamIndex;
            IsReady = isReady;
            IsConnected = isConnected;
        }
    }

    /// <summary>Issue d'une tentative de connexion à un salon.</summary>
    public enum JoinStatus
    {
        Success,
        RoomFull,
        Failed
    }

    /// <summary>Résultat d'un <c>Join</c> : statut + id attribué + message éventuel.</summary>
    public struct JoinResult
    {
        public JoinStatus Status;
        public int AssignedPlayerId;
        public string Message;

        public bool Ok => Status == JoinStatus.Success;

        public static JoinResult Succeeded(int playerId) => new JoinResult
        {
            Status = JoinStatus.Success,
            AssignedPlayerId = playerId,
            Message = null
        };

        public static JoinResult Rejected(JoinStatus status, string message) => new JoinResult
        {
            Status = status,
            AssignedPlayerId = -1,
            Message = message
        };
    }
}
