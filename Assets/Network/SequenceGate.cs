namespace MMPong.Network
{
    /// <summary>
    /// Garde anti-paquet-périmé. UDP n'ordonne ni ne garantit la livraison : cette classe
    /// n'accepte qu'un <c>seq</c> strictement croissant et rejette doublons et paquets en
    /// retard. Logique pure (sans Unity ni socket) → testable isolément et réutilisable
    /// (la déduplication du transport fiable s'appuiera sur la même notion de séquence).
    /// </summary>
    public sealed class SequenceGate
    {
        uint lastSeq;

        /// <summary>Vrai si <paramref name="seq"/> est plus frais que le dernier accepté (et le mémorise).</summary>
        public bool Accept(uint seq)
        {
            if (seq <= lastSeq) return false;
            lastSeq = seq;
            return true;
        }

        /// <summary>Réinitialise le compteur (resync à la (re)connexion, ex. serveur redémarré).</summary>
        public void Reset() => lastSeq = 0;
    }
}
