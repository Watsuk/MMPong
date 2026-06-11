using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private Button teamAButton;
        private Button teamBButton;
        private int selectedTeam;

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform, spacing: 12f);

            UIFactory.CreateTitle(col, "Rejoindre le salon");

            UIFactory.CreateLabel(col, "PseudoHint", "Pseudo :", 20f);
            pseudoInput = UIFactory.CreateInputField(col, "PseudoInput", "Votre pseudo");

            UIFactory.CreateLabel(col, "TeamHint", "Équipe :", 20f);
            var teamRow = UIFactory.CreateRow(col, "TeamRow");
            teamAButton = UIFactory.CreateButton(teamRow, "TeamABtn", "Équipe A", () => SelectTeam(0), new Vector2(150f, 44f));
            teamBButton = UIFactory.CreateButton(teamRow, "TeamBBtn", "Équipe B", () => SelectTeam(1), new Vector2(150f, 44f));
            teamLabel = UIFactory.CreateLabel(col, "TeamLabel", "", 20f);

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
            Lobby.Disconnected += OnDisconnected;
            OnPlayersChanged(Lobby.Players);
        }

        protected override void OnExit()
        {
            Lobby.PlayersChanged -= OnPlayersChanged;
            Lobby.Disconnected -= OnDisconnected;
            Lobby.Leave();
        }

        private void OnDisconnected()
        {
            // L'hôte est perdu (déconnexion gracieuse ou timeout). On réaffiche le hub — masqué
            // pendant la partie — puis on renvoie le joueur à l'écran de saisie d'IP, dans tous les cas.
            Manager.ShowHub();
            Manager.Show(ScreenId.JoinIp);
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

            Lobby.Join(Session.TargetIp, pseudo, selectedTeam);
            Lobby.SetReady(true);
            statusLabel.text = "Prêt — en attente du démarrage par l'hôte…";
        }

        private void OnPlayersChanged(IReadOnlyList<PlayerInfo> players)
        {
            UpdateTeamAvailability(players);

            // La config du host (noms d'équipes) peut être arrivée entre-temps : on rafraîchit le label.
            teamLabel.text = "Équipe choisie : " + TeamName(selectedTeam);

            for (int i = listContainer.childCount - 1; i >= 0; i--)
                Destroy(listContainer.GetChild(i).gameObject);

            foreach (var p in players)
                PlayerListItem.Create(listContainer).Bind(p);
        }

        /// <summary>
        /// Grise l'équipe pleine et bascule la sélection vers l'équipe libre. Plafond par équipe =
        /// ceil(nbJoueurs du match / 2). Le serveur reste autoritaire : ceci n'est qu'un confort UI.
        /// </summary>
        private void UpdateTeamAvailability(IReadOnlyList<PlayerInfo> players)
        {
            int max = Session.MatchConfig != null ? Session.MatchConfig.MaxPlayerCount : 4;
            int cap = (max + 1) / 2; // ceil(max / 2)

            int countA = 0, countB = 0;
            foreach (var p in players)
            {
                if (p.TeamIndex == 1) countB++;
                else countA++;
            }

            bool aFull = countA >= cap;
            bool bFull = countB >= cap;

            teamAButton.interactable = !aFull;
            teamBButton.interactable = !bFull;

            // Si l'équipe choisie est pleine, on bascule vers l'autre (qui a forcément de la place).
            if (selectedTeam == 0 && aFull && !bFull) SelectTeam(1);
            else if (selectedTeam == 1 && bFull && !aFull) SelectTeam(0);
        }
    }
}
