namespace MMPong.UI
{
    /// <summary>
    /// Rôle réseau choisi par le joueur dans le hub.
    /// (Distinct de <c>GameBootstrap.GameMode</c> : ici on raisonne au niveau UI.)
    /// </summary>
    public enum NetworkRole
    {
        None,
        Host,
        Client
    }

    /// <summary>
    /// État partagé entre tous les écrans du hub (mode, rôle, pseudo, IP cible,
    /// configuration de match, équipe choisie). Objet C# pur, instancié et exposé
    /// par le <see cref="ScreenManager"/>.
    ///
    /// Centralise ce qui circule d'un écran à l'autre : aucun écran ne stocke ces
    /// choix via des <c>static</c> dispersés — tout vit ici.
    /// </summary>
    public class UISession
    {
        /// <summary>true = partie en ligne, false = partie locale contre l'IA.</summary>
        public bool IsOnline;

        /// <summary>Rôle réseau choisi (Host quand on crée un salon, Client quand on rejoint).</summary>
        public NetworkRole Role = NetworkRole.None;

        /// <summary>Pseudo saisi par le joueur.</summary>
        public string Pseudo = "";

        /// <summary>IP du salon à rejoindre (mode Client uniquement).</summary>
        public string TargetIp = "";

        /// <summary>Configuration du match définie par le host (null tant que non configuré).</summary>
        public MatchConfig MatchConfig;

        /// <summary>Index de l'équipe choisie (0 = équipe A, 1 = équipe B).</summary>
        public int SelectedTeamIndex;

        /// <summary>Remet la session à son état initial (utile en revenant au tout premier écran).</summary>
        public void Reset()
        {
            IsOnline = false;
            Role = NetworkRole.None;
            Pseudo = "";
            TargetIp = "";
            MatchConfig = null;
            SelectedTeamIndex = 0;
        }
    }
}
