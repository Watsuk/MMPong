using System;
using System.Collections.Generic;

namespace MMPong.UI
{
    /// <summary>
    /// Abstraction réseau côté UI : les écrans du lobby ne parlent qu'à cette interface,
    /// jamais directement à la couche réseau. Permet de développer/tester toute l'UI avec
    /// un mock (<see cref="FakeLobbyService"/>) avant que l'équipe réseau ne branche la
    /// vraie implémentation — seule l'instance injectée changera, aucun écran ne bouge.
    ///
    /// Mapping prévu (cf. docs/tickets.md) :
    ///   PlayersChanged → NetworkClient.OnLobbyReceived · GameStarted → OnGameStarted
    ///   Host → GameBootstrap (Host) · Join → GameBootstrap (Client) · SetReady → SendReady.
    /// </summary>
    public interface ILobbyService
    {
        /// <summary>Émis à chaque changement de la liste des joueurs (join/leave/ready/équipe).</summary>
        event Action<IReadOnlyList<PlayerInfo>> PlayersChanged;

        /// <summary>Émis quand la partie démarre (le host a lancé / le serveur a broadcast START).</summary>
        event Action GameStarted;

        /// <summary>Émis en réponse à un <see cref="Join"/> (succès avec id, ou rejet).</summary>
        event Action<JoinResult> JoinResult;

        /// <summary>Liste courante des joueurs du salon (snapshot lisible à tout moment).</summary>
        IReadOnlyList<PlayerInfo> Players { get; }

        /// <summary>true si l'instance locale est l'hôte du salon.</summary>
        bool IsHost { get; }

        /// <summary>Id du joueur local (-1 tant qu'aucun id n'a été attribué).</summary>
        int LocalPlayerId { get; }

        /// <summary>Ouvre un salon en tant qu'hôte avec la configuration donnée.</summary>
        void Host(MatchConfig config);

        /// <summary>Rejoint un salon distant (déclenche un <see cref="JoinResult"/>).</summary>
        void Join(string ip, string pseudo, int teamIndex);

        /// <summary>Met à jour l'état « prêt » du joueur local.</summary>
        void SetReady(bool ready);

        /// <summary>Démarre la partie (host uniquement) ; déclenche <see cref="GameStarted"/>.</summary>
        void StartGame();
    }
}
