using System.Collections.Generic;

namespace MMPong.Network
{
    /// <summary>
    /// Suit les joueurs ayant signalé être prêts (lobby) et indique si le quota attendu est
    /// atteint. Idempotent : un même joueur marqué prêt plusieurs fois ne compte qu'une fois.
    /// Logique pure → testable isolément.
    /// </summary>
    public sealed class ReadyTracker
    {
        readonly HashSet<int> ready = new HashSet<int>();

        /// <summary>Marque un joueur comme prêt ; renvoie vrai si c'est un nouvel état (pas un doublon).</summary>
        public bool MarkReady(int playerId) => ready.Add(playerId);

        /// <summary>Nombre de joueurs distincts prêts.</summary>
        public int Count => ready.Count;

        /// <summary>Vrai si au moins <paramref name="expected"/> joueurs sont prêts.</summary>
        public bool AllReady(int expected) => ready.Count >= expected;
    }
}
