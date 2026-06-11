using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// [6] Pseudo + choix d'équipe (parmi les deux du host) + bouton « Je suis prêt ».
    /// Rejoint via <see cref="ILobbyService.Join"/>, remonte l'état prêt, puis attend
    /// <c>GameStarted</c> (lancement géré par <see cref="GameLauncher"/>). Affiche la liste live.
    /// </summary>
    public class JoinSetupScreen : UIScreen
    {
        public override ScreenId Id => ScreenId.JoinSetup;

        private TMP_InputField pseudoInput;
        private TextMeshProUGUI teamLabel;
        private TextMeshProUGUI errorLabel;
        private TextMeshProUGUI statusLabel;
        private Transform listContainer;
        private int selectedTeam;
        private int selectedColor;

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform, spacing: 12f);

            UIFactory.CreateTitle(col, "Rejoindre le salon");

            UIFactory.CreateLabel(col, "PseudoHint", "Pseudo :", 20f);
            pseudoInput = UIFactory.CreateInputField(col, "PseudoInput", "Votre pseudo");

            UIFactory.CreateLabel(col, "TeamHint", "Équipe :", 20f);
            var teamRow = UIFactory.CreateRow(col, "TeamRow");
            UIFactory.CreateButton(teamRow, "TeamABtn", "Équipe A", () => SelectTeam(0), new Vector2(150f, 44f));
            UIFactory.CreateButton(teamRow, "TeamBBtn", "Équipe B", () => SelectTeam(1), new Vector2(150f, 44f));
            teamLabel = UIFactory.CreateLabel(col, "TeamLabel", "", 20f);

            UIFactory.CreateColorSelector(col, "Couleur", selectedColor, v => selectedColor = v);

            errorLabel = UIFactory.CreateLabel(col, "Error", "", 20f);
            errorLabel.color = new Color(1f, 0.45f, 0.45f, 1f);

            UIFactory.CreateButton(col, "ReadyButton", "Je suis prêt", OnReadyClicked);

            statusLabel = UIFactory.CreateLabel(col, "Status", "", 18f);
            UIFactory.CreateLabel(col, "PlayerListHeader", "Liste des joueurs :", 20f);
            listContainer = UIFactory.CreateColumn(col, "PlayerList", spacing: 6f);

            AddBackButton(root.transform);
        }

        protected override void OnEnter()
        {
            errorLabel.text = "";
            statusLabel.text = "";
            SelectTeam(0);
            Lobby.PlayersChanged += OnPlayersChanged;
            OnPlayersChanged(Lobby.Players);
        }

        protected override void OnExit()
        {
            Lobby.PlayersChanged -= OnPlayersChanged;
        }

        private string TeamName(int index)
        {
            if (Session.MatchConfig != null)
                return index == 0 ? Session.MatchConfig.TeamA.Name : Session.MatchConfig.TeamB.Name;
            return index == 0 ? "Équipe A" : "Équipe B";
        }

        private void SelectTeam(int index)
        {
            selectedTeam = index;
            Session.SelectedTeamIndex = index;
            teamLabel.text = "Équipe choisie : " + TeamName(index);
        }

        private void OnReadyClicked()
        {
            string pseudo = pseudoInput.text != null ? pseudoInput.text.Trim() : "";
            if (string.IsNullOrEmpty(pseudo))
            {
                errorLabel.text = "Le pseudo ne peut pas être vide.";
                return;
            }

            errorLabel.text = "";
            Session.Pseudo = pseudo;
            Session.SelectedTeamIndex = selectedTeam;
            Session.SelectedColorIndex = selectedColor;

            Lobby.Join(Session.TargetIp, pseudo, selectedTeam, selectedColor);
            Lobby.SetReady(true);
            statusLabel.text = "Prêt — en attente du démarrage par l'hôte…";
        }

        private void OnPlayersChanged(IReadOnlyList<PlayerInfo> players)
        {
            // La config du host (noms d'équipes) peut être arrivée entre-temps : on rafraîchit le label.
            teamLabel.text = "Équipe choisie : " + TeamName(selectedTeam);

            for (int i = listContainer.childCount - 1; i >= 0; i--)
                Destroy(listContainer.GetChild(i).gameObject);

            foreach (var p in players)
                PlayerListItem.Create(listContainer).Bind(p);
        }
    }
}
