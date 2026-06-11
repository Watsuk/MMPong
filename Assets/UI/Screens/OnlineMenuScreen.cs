namespace MMPong.UI
{
    /// <summary>
    /// [2] Écran en ligne : « Créer un salon » (rôle Host → config) ou « Rejoindre » (rôle Client → IP).
    /// Stocke le rôle dans la session.
    /// </summary>
    public class OnlineMenuScreen : UIScreen
    {
        public override ScreenId Id => ScreenId.OnlineMenu;

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform);

            UIFactory.CreateTitle(col, "Jouer en ligne");

            UIFactory.CreateButton(col, "CreateButton", "Créer un salon", () =>
            {
                Session.Role = NetworkRole.Host;
                Manager.Show(ScreenId.CreateConfig);
            });

            UIFactory.CreateButton(col, "JoinButton", "Rejoindre un salon", () =>
            {
                Session.Role = NetworkRole.Client;
                Manager.Show(ScreenId.JoinIp);
            });

            AddBackButton(root.transform);
        }
    }
}
