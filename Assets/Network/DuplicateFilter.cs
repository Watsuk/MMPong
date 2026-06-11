using System.Collections.Generic;

namespace MMPong.Network
{
    /// <summary>
    /// Déduplication exactly-once des messages fiables. Contrairement à <see cref="SequenceGate"/>
    /// (monotone, jette les seq périmés), retient l'ensemble des seq déjà traités et accepte
    /// indépendamment de l'ordre d'arrivée : un message fiable arrivé en retard (seq plus petit)
    /// doit quand même être traité une fois, jamais deux. Logique pure → testable isolément.
    /// </summary>
    public sealed class DuplicateFilter
    {
        readonly HashSet<uint> seen = new HashSet<uint>();

        /// <summary>Vrai si <paramref name="seq"/> n'a jamais été vu (et le mémorise).</summary>
        public bool IsNew(uint seq) => seen.Add(seq);
    }
}
