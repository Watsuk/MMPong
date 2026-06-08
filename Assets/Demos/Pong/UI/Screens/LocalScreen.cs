using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// Partie locale rapide contre l'IA : lance directement la partie avec une config aléatoire,
    /// sans passer par les écrans en ligne. Bouton retour pour ne pas être un cul-de-sac.
    /// </summary>
    public class LocalScreen : UIScreen
    {
        public override ScreenId Id => ScreenId.Local;

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform);

            UIFactory.CreateTitle(col, "Partie locale");
            UIFactory.CreateLabel(col, "Info", "Match rapide contre l'IA (configuration aléatoire).", 22f,
                new Vector2(620f, 44f));

            UIFactory.CreateButton(col, "LaunchButton", "Lancer la partie", () =>
            {
                var launcher = FindFirstObjectByType<GameLauncher>();
                if (launcher != null)
                    launcher.LaunchLocal();
                else
                    Debug.LogError("[LocalScreen] GameLauncher introuvable.");
            });

            AddBackButton(root.transform);
        }
    }
}
