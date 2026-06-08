using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MMPong.UI
{
    /// <summary>
    /// [4] Salle d'attente du host : IP du salon, compteur X/max, liste live des joueurs
    /// (mise à jour via <see cref="ILobbyService.PlayersChanged"/>). Bouton Démarrer interactif
    /// seulement pour le host et à partir de 2 joueurs. Le lancement réel est pris en charge par
    /// <see cref="GameLauncher"/> (abonné à <c>GameStarted</c>).
    /// </summary>
    public class HostLobbyScreen : UIScreen
    {
        public override ScreenId Id => ScreenId.HostLobby;

        private TextMeshProUGUI ipLabel;
        private TextMeshProUGUI countLabel;
        private Transform listContainer;
        private Button startButton;

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform, spacing: 12f);

            UIFactory.CreateTitle(col, "Salle d'attente");
            ipLabel = UIFactory.CreateLabel(col, "Ip", "", 22f);
            countLabel = UIFactory.CreateLabel(col, "Count", "", 24f);
            listContainer = UIFactory.CreateColumn(col, "PlayerList", spacing: 6f);

            startButton = UIFactory.CreateButton(col, "StartButton", "Démarrer", () => Lobby.StartGame());

            AddBackButton(root.transform);
        }

        protected override void OnEnter()
        {
            ipLabel.text = "IP du salon : " + NetworkUtils.LocalIPv4();
            Lobby.PlayersChanged += OnPlayersChanged;
            OnPlayersChanged(Lobby.Players);
        }

        protected override void OnExit()
        {
            Lobby.PlayersChanged -= OnPlayersChanged;
        }

        private void OnPlayersChanged(IReadOnlyList<PlayerInfo> players)
        {
            for (int i = listContainer.childCount - 1; i >= 0; i--)
                Destroy(listContainer.GetChild(i).gameObject);

            foreach (var p in players)
                PlayerListItem.Create(listContainer).Bind(p);

            int max = Session.MatchConfig != null ? Session.MatchConfig.MaxPlayerCount : MatchConfig.MaxPlayers;
            countLabel.text = $"{players.Count} / {max} joueurs";
            startButton.interactable = Lobby.IsHost && players.Count >= 2;
        }
    }
}
