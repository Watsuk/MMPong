using System.Collections.Generic;
using System.Net;

namespace MMPong.Network
{
    /// <summary>
    /// Registre des clients connectés : bijection identifiant ↔ endpoint ↔ pseudo, avec une
    /// capacité bornée. Logique pure (sans Unity ni socket) → testable isolément. N'attribue un
    /// identifiant qu'aux nouveaux endpoints (la recherche d'un existant est explicite via
    /// <see cref="TryFindId"/>, pour préserver l'idempotence d'un re-JOIN).
    /// </summary>
    public sealed class ClientRegistry
    {
        public const int MaxPlayers = 4;

        readonly Dictionary<int, IPEndPoint> endpoints = new Dictionary<int, IPEndPoint>();
        readonly Dictionary<int, string> pseudos = new Dictionary<int, string>();

        /// <summary>Nombre de clients enregistrés.</summary>
        public int Count => endpoints.Count;

        /// <summary>Cherche l'identifiant déjà attribué à un endpoint. Faux si inconnu.</summary>
        public bool TryFindId(IPEndPoint ep, out int id)
        {
            foreach (var kv in endpoints)
            {
                if (kv.Value.Equals(ep)) { id = kv.Key; return true; }
            }
            id = -1;
            return false;
        }

        /// <summary>Attribue le premier identifiant libre (0..MaxPlayers-1) à un nouvel endpoint, ou -1 si plein.</summary>
        public int Register(IPEndPoint ep, string pseudo)
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (!endpoints.ContainsKey(i))
                {
                    endpoints[i] = ep;
                    pseudos[i] = pseudo;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Endpoints de tous les clients enregistrés (pour la diffusion).</summary>
        public IEnumerable<IPEndPoint> Endpoints => endpoints.Values;

        /// <summary>Pseudos indexés par identifiant (longueur MaxPlayers, "" pour les slots libres).</summary>
        public string[] Pseudos()
        {
            var result = new string[MaxPlayers];
            for (int i = 0; i < MaxPlayers; i++)
                result[i] = pseudos.TryGetValue(i, out var p) ? p : "";
            return result;
        }
    }
}
