namespace MMPong.UI
{
    /// <summary>
    /// Identifiant de chaque écran plein écran du hub pré-partie.
    /// Toute navigation passe par le <see cref="ScreenManager"/> via ces identifiants
    /// (jamais de référence directe d'un écran vers un autre).
    /// </summary>
    public enum ScreenId
    {
        ModeSelect,   // [1] Choix Local / En ligne
        OnlineMenu,   // [2] Créer / Rejoindre
        CreateConfig, // [3] Configuration du salon (host)
        HostLobby,    // [4] Salle d'attente (host)
        JoinIp,       // [5] Saisie de l'IP
        JoinSetup,    // [6] Pseudo + équipe/skin + prêt
        Local         // Partie locale contre l'IA
    }
}
