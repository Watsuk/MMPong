using TMPro;
using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// Partie locale à 2 joueurs sur le même clavier. Le joueur saisit le nom de chaque équipe
    /// (qui remplace « Équipe Bleue / Rouge » dans le HUD) et le pseudo de chaque joueur
    /// (affiché sous le nom de son équipe). Bouton retour pour ne pas être un cul-de-sac.
    /// </summary>
    public class LocalScreen : UIScreen
    {
        public override ScreenId Id => ScreenId.Local;

        private TMP_InputField teamAInput;
        private TMP_InputField teamBInput;
        private TMP_InputField player1Input;
        private TMP_InputField player2Input;
        private int player1Color = 0; // Bleu par défaut (équipe gauche)
        private int player2Color = 1; // Rouge par défaut (équipe droite)

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform, spacing: 12f);

            UIFactory.CreateTitle(col, "Partie locale");
            UIFactory.CreateLabel(col, "Info", "Match à 2 joueurs sur le même clavier.", 20f,
                new Vector2(620f, 36f));

            UIFactory.CreateLabel(col, "TeamAHeader", "Équipe gauche", 20f);
            UIFactory.CreateLabel(col, "TeamANameLabel", "Nom de l'équipe :", 18f);
            teamAInput = UIFactory.CreateInputField(col, "TeamAName", "Nom de l'équipe gauche");
            teamAInput.text = "Équipe Bleue";
            UIFactory.CreateLabel(col, "Player1Label", "Pseudo du joueur :", 18f);
            player1Input = UIFactory.CreateInputField(col, "Player1Name", "Pseudo joueur 1");
            UIFactory.CreateColorSelector(col, "Couleur J1", player1Color, v => player1Color = v);

            UIFactory.CreateLabel(col, "TeamBHeader", "Équipe droite", 20f);
            UIFactory.CreateLabel(col, "TeamBNameLabel", "Nom de l'équipe :", 18f);
            teamBInput = UIFactory.CreateInputField(col, "TeamBName", "Nom de l'équipe droite");
            teamBInput.text = "Équipe Rouge";
            UIFactory.CreateLabel(col, "Player2Label", "Pseudo du joueur :", 18f);
            player2Input = UIFactory.CreateInputField(col, "Player2Name", "Pseudo joueur 2");
            UIFactory.CreateColorSelector(col, "Couleur J2", player2Color, v => player2Color = v);

            UIFactory.CreateButton(col, "LaunchButton", "Lancer la partie", OnLaunch);

            AddBackButton(root.transform);
        }

        private void OnLaunch()
        {
            var launcher = FindFirstObjectByType<GameLauncher>();
            if (launcher == null)
            {
                Debug.LogError("[LocalScreen] GameLauncher introuvable.");
                return;
            }

            launcher.LaunchLocal(
                Value(teamAInput, "Équipe Bleue"),
                Value(teamBInput, "Équipe Rouge"),
                Value(player1Input, "Joueur 1"),
                Value(player2Input, "Joueur 2"),
                player1Color,
                player2Color);
        }

        // Texte saisi nettoyé, ou repli si le champ est vide.
        private static string Value(TMP_InputField field, string fallback)
        {
            return field != null && !string.IsNullOrWhiteSpace(field.text) ? field.text.Trim() : fallback;
        }
    }
}
