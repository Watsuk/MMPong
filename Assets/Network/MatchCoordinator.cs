namespace MMPong.Network
{
    /// <summary>
    /// Décide quand la partie démarre : collecte les joueurs prêts (via <see cref="ReadyTracker"/>)
    /// et déclenche le start une seule fois, quand le quota attendu est atteint. Logique pure
    /// (aucun effet de bord) → l'orchestrateur réalise les actions de start quand
    /// <see cref="TryStart"/> renvoie vrai. Le latch garantit un démarrage unique.
    /// </summary>
    public sealed class MatchCoordinator
    {
        readonly ReadyTracker ready = new ReadyTracker();
        readonly int expectedPlayers;
        bool isRematchWaiting;

        public MatchCoordinator(int expectedPlayers)
        {
            this.expectedPlayers = expectedPlayers;
        }

        /// <summary>Vrai si la partie a déjà démarré.</summary>
        public bool Started { get; private set; }

        /// <summary>Vrai si la partie est terminée et qu'on attend que les joueurs relancent.</summary>
        public bool IsRematchWaiting => isRematchWaiting;

        /// <summary>Nombre de joueurs distincts prêts.</summary>
        public int ReadyCount => ready.Count;

        /// <summary>Prépare le coordinateur pour un match suivant.</summary>
        public void PrepareRematch()
        {
            isRematchWaiting = true;
            ready.Clear();
        }

        /// <summary>
        /// Marque un joueur prêt ; renvoie vrai <b>exactement une fois</b>, lorsque le quota
        /// attendu est atteint pour la première fois (faux avant, et faux ensuite).
        /// </summary>
        public bool TryStart(int readyPlayerId)
        {
            ready.MarkReady(readyPlayerId);
            if (!isRematchWaiting && Started) return false;
            if (!ready.AllReady(expectedPlayers)) return false;
            
            Started = true;
            isRematchWaiting = false;
            return true;
        }

        /// <summary>
        /// Démarrage autoritaire forcé par le host (bouton « Démarrer »), indépendamment du quota.
        /// Renvoie vrai exactement une fois (latch identique à <see cref="TryStart"/>).
        /// </summary>
        public bool ForceStart()
        {
            if (!isRematchWaiting && Started) return false;
            Started = true;
            isRematchWaiting = false;
            return true;
        }
    }
}
