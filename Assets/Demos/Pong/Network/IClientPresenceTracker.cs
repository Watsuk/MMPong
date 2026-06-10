namespace MMPong.Network
{
    /// <summary>
    /// Suit la présence (connecté / déconnecté) des joueurs à partir de leur activité réseau.
    /// Abstraction (DIP) : <see cref="NetworkServer"/> en dépend sans connaître la stratégie
    /// concrète (timeout, heartbeat dédié, …) → testable et remplaçable.
    ///
    /// Le temps est <b>injecté</b> (<paramref name="now"/>) plutôt que lu depuis Unity, pour
    /// garder les implémentations pures et testables hors moteur.
    /// </summary>
    public interface IClientPresenceTracker
    {
        /// <summary>Enregistre un joueur qui rejoint : connecté, dernière activité = <paramref name="now"/>.</summary>
        void Register(int id, float now);

        /// <summary>Signale un paquet reçu de ce joueur : met à jour sa dernière activité.</summary>
        void MarkSeen(int id, float now);

        /// <summary>
        /// Recalcule l'état connecté de chaque joueur enregistré d'après les délais d'inactivité.
        /// Retourne <c>true</c> si au moins un état a basculé (→ le serveur peut rediffuser le lobby).
        /// </summary>
        bool Evaluate(float now);

        /// <summary>État connecté courant d'un joueur (faux si inconnu ou déconnecté).</summary>
        bool IsConnected(int id);

        /// <summary>États connectés positionnels (longueur = capacité, indexé par id).</summary>
        bool[] ConnectedFlags();
    }
}
