namespace MMPong.Network
{
    /// <summary>
    /// Détection de présence par <b>timeout d'inactivité</b>. Un joueur est considéré connecté
    /// tant que le serveur reçoit ses paquets régulièrement (le client émet <c>INPUT</c> à 30 Hz,
    /// ce qui sert de heartbeat implicite). Au-delà de <see cref="timeout"/> secondes sans aucun
    /// paquet, il passe déconnecté ; le retour d'activité le repasse connecté.
    ///
    /// Logique pure (aucune dépendance Unity, temps injecté) → testable isolément comme
    /// <see cref="ClientRegistry"/> ou <c>ReadyTracker</c>.
    /// </summary>
    public sealed class TimeoutPresenceTracker : IClientPresenceTracker
    {
        readonly float timeout;
        readonly bool[] registered;
        readonly bool[] connected;
        readonly float[] lastSeen;

        public TimeoutPresenceTracker(int capacity, float timeoutSeconds)
        {
            timeout = timeoutSeconds;
            registered = new bool[capacity];
            connected = new bool[capacity];
            lastSeen = new float[capacity];
        }

        public void Register(int id, float now)
        {
            if (!InRange(id)) return;
            registered[id] = true;
            connected[id] = true;
            lastSeen[id] = now;
        }

        public void MarkSeen(int id, float now)
        {
            // On n'écrit que l'horodatage d'activité : l'état connecté est dérivé dans Evaluate,
            // ce qui centralise la règle (déconnexion ET reconnexion) en un seul endroit.
            if (InRange(id) && registered[id]) lastSeen[id] = now;
        }

        public bool Evaluate(float now)
        {
            bool changed = false;
            for (int id = 0; id < registered.Length; id++)
            {
                if (!registered[id]) continue;
                bool alive = (now - lastSeen[id]) <= timeout;
                if (alive != connected[id])
                {
                    connected[id] = alive;
                    changed = true;
                }
            }
            return changed;
        }

        public bool IsConnected(int id) => InRange(id) && connected[id];

        public bool[] ConnectedFlags()
        {
            var flags = new bool[connected.Length];
            System.Array.Copy(connected, flags, connected.Length);
            return flags;
        }

        bool InRange(int id) => id >= 0 && id < registered.Length;
    }
}
