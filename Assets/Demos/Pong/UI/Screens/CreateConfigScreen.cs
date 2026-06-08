using TMPro;
using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// [3] Configuration du salon (host) : nombre de joueurs (2–6), condition de victoire
    /// (Points ou Timer, paramétrables), deux équipes (noms + skins distincts). Valide la
    /// config avant d'ouvrir le salon via <see cref="ILobbyService.Host"/>.
    /// </summary>
    public class CreateConfigScreen : UIScreen
    {
        public override ScreenId Id => ScreenId.CreateConfig;

        private int maxPlayers = 4;
        private WinConditionType winType = WinConditionType.Points;
        private int targetPoints = 5;
        private int durationSeconds = 120;
        private int skinA = 0;
        private int skinB = 1;

        private TMP_InputField teamAInput;
        private TMP_InputField teamBInput;
        private TextMeshProUGUI winTypeLabel;
        private TextMeshProUGUI errorLabel;

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform, spacing: 12f);

            UIFactory.CreateTitle(col, "Configurer le salon");

            UIFactory.CreateStepper(col, "Joueurs", maxPlayers, MatchConfig.MinPlayers, MatchConfig.MaxPlayers,
                v => maxPlayers = v);

            UIFactory.CreateLabel(col, "WinHeader", "Condition de victoire", 22f);
            var winRow = UIFactory.CreateRow(col, "WinTypeRow");
            UIFactory.CreateButton(winRow, "PointsBtn", "Points", () => SetWinType(WinConditionType.Points),
                new Vector2(140f, 44f));
            UIFactory.CreateButton(winRow, "TimerBtn", "Timer", () => SetWinType(WinConditionType.Timer),
                new Vector2(140f, 44f));
            winTypeLabel = UIFactory.CreateLabel(col, "WinTypeLabel", "", 20f);

            UIFactory.CreateStepper(col, "Points cible", targetPoints, 1, 21, v => targetPoints = v);
            UIFactory.CreateStepper(col, "Durée (s)", durationSeconds, 30, 300, v => durationSeconds = v);

            UIFactory.CreateLabel(col, "TeamAHeader", "Équipe A", 22f);
            teamAInput = UIFactory.CreateInputField(col, "TeamAName", "Nom équipe A");
            teamAInput.text = "Rouge";
            UIFactory.CreateStepper(col, "Skin A", skinA, 0, 3, v => skinA = v);

            UIFactory.CreateLabel(col, "TeamBHeader", "Équipe B", 22f);
            teamBInput = UIFactory.CreateInputField(col, "TeamBName", "Nom équipe B");
            teamBInput.text = "Bleu";
            UIFactory.CreateStepper(col, "Skin B", skinB, 0, 3, v => skinB = v);

            errorLabel = UIFactory.CreateLabel(col, "Error", "", 20f);
            errorLabel.color = new Color(1f, 0.45f, 0.45f, 1f);

            UIFactory.CreateButton(col, "OpenButton", "Ouvrir le salon", OnOpenClicked);

            AddBackButton(root.transform);

            SetWinType(winType);
        }

        protected override void OnEnter()
        {
            errorLabel.text = "";
        }

        private void SetWinType(WinConditionType type)
        {
            winType = type;
            winTypeLabel.text = type == WinConditionType.Points
                ? "Sélectionné : Points"
                : "Sélectionné : Timer";
        }

        private void OnOpenClicked()
        {
            var config = new MatchConfig
            {
                MaxPlayerCount = maxPlayers,
                WinCondition = new WinCondition
                {
                    Type = winType,
                    TargetPoints = targetPoints,
                    DurationSeconds = durationSeconds
                },
                TeamA = new TeamConfig(teamAInput.text != null ? teamAInput.text.Trim() : "", skinA),
                TeamB = new TeamConfig(teamBInput.text != null ? teamBInput.text.Trim() : "", skinB)
            };

            if (!config.IsValid(out string error))
            {
                errorLabel.text = error;
                return;
            }

            Session.MatchConfig = config;
            Lobby.Host(config);
            Manager.Show(ScreenId.HostLobby);
        }
    }
}
