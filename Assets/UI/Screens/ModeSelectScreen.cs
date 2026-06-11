namespace MMPong.UI
{
    /// <summary>
    /// [1] Premier écran (racine) : choix « Jouer en local » / « Jouer en ligne ».
    /// Stocke le mode dans la session et route via le manager. Pas de bouton retour (racine).
    /// </summary>
    public class ModeSelectScreen : UIScreen
    {
        public override ScreenId Id => ScreenId.ModeSelect;

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform);

            UIFactory.CreateTitle(col, "MMPong");
            UIFactory.CreateLabel(col, "Subtitle", "Pong multijoueur", 24f);

            UIFactory.CreateButton(col, "LocalButton", "Jouer en local", () =>
            {
                Session.IsOnline = false;
                Session.Role = NetworkRole.None;
                Manager.Show(ScreenId.Local);
            });

            UIFactory.CreateButton(col, "OnlineButton", "Jouer en ligne", () =>
            {
                Session.IsOnline = true;
                Manager.Show(ScreenId.OnlineMenu);
            });
        }
    }
}
