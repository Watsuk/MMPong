using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// Implémentation mock de <see cref="ILobbyService"/> : simule un salon sans aucun
    /// réseau. De faux joueurs rejoignent à intervalles, passent « prêt » aléatoirement,
    /// et le host peut démarrer. Permet de tester tous les écrans lobby d'EPIC 1 de bout
    /// en bout. À remplacer par l'implémentation réseau réelle plus tard (mêmes events).
    /// </summary>
    public class FakeLobbyService : MonoBehaviour, ILobbyService
    {
        [Header("Simulation")]
        [Tooltip("Nombre de faux joueurs qui rejoignent en tant qu'hôte (en plus du local).")]
        [SerializeField] private int fakeJoinerCount = 3;

        [Tooltip("Délai (s) entre deux arrivées de faux joueurs.")]
        [SerializeField] private float joinInterval = 1.5f;

        [Tooltip("Délai (s) avant qu'un faux joueur passe « prêt ».")]
        [SerializeField] private float readyDelay = 1f;

        [Tooltip("Délai simulé (s) avant la réponse à un Join.")]
        [SerializeField] private float joinResponseDelay = 0.5f;

        [Tooltip("En tant que client : délai (s) après « prêt » avant que le faux host démarre la partie.")]
        [SerializeField] private float fakeHostStartDelay = 3f;

        private static readonly string[] FakePseudos =
            { "Alex", "Sam", "Robin", "Charlie", "Noa", "Lou", "Max", "Jess" };

        private readonly List<PlayerInfo> players = new List<PlayerInfo>();
        private MatchConfig config;
        private int nextId;
        private bool pendingReady; // « prêt » demandé avant la fin du Join (réponse asynchrone)

        public event Action<IReadOnlyList<PlayerInfo>> PlayersChanged;
        public event Action GameStarted;
        public event Action<JoinResult> JoinResult;

        public IReadOnlyList<PlayerInfo> Players => players;
        public bool IsHost { get; private set; }
        public int LocalPlayerId { get; private set; } = -1;

        public void Host(MatchConfig matchConfig)
        {
            StopAllCoroutines();
            players.Clear();
            nextId = 0;
            config = matchConfig ?? new MatchConfig();

            IsHost = true;
            LocalPlayerId = nextId++;
            players.Add(new PlayerInfo(LocalPlayerId, "Vous (hôte)", teamIndex: 0, isReady: true));
            RaisePlayersChanged();

            StartCoroutine(SimulateJoiners());
        }

        public void Join(string ip, string pseudo, int teamIndex)
        {
            StopAllCoroutines();
            players.Clear();
            nextId = 0;
            IsHost = false;
            pendingReady = false;
            StartCoroutine(SimulateJoinResponse(pseudo, teamIndex));
        }

        public void SetReady(bool ready)
        {
            var local = FindLocal();
            if (local == null)
            {
                // Join encore en cours (réponse asynchrone) : on appliquera l'état une fois arrivé.
                pendingReady = ready;
                return;
            }

            local.IsReady = ready;
            RaisePlayersChanged();

            // Côté client : simule le host qui lance la partie peu après qu'on est prêt.
            if (!IsHost && ready)
                StartCoroutine(SimulateHostStart());
        }

        public void StartGame()
        {
            if (!IsHost)
                return;

            GameStarted?.Invoke();
        }

        // ── Simulation ──────────────────────────────────────────────────────────

        private IEnumerator SimulateJoiners()
        {
            int maxPlayers = config != null ? config.MaxPlayerCount : MatchConfig.MaxPlayers;

            for (int i = 0; i < fakeJoinerCount && players.Count < maxPlayers; i++)
            {
                yield return new WaitForSeconds(joinInterval);

                int id = nextId++;
                string pseudo = FakePseudos[id % FakePseudos.Length];
                int team = id % 2; // alterne entre les deux équipes
                var player = new PlayerInfo(id, pseudo, team);
                players.Add(player);
                RaisePlayersChanged();

                StartCoroutine(SimulateReady(player));
            }
        }

        private IEnumerator SimulateReady(PlayerInfo player)
        {
            yield return new WaitForSeconds(readyDelay + UnityEngine.Random.Range(0f, readyDelay));

            if (!players.Contains(player))
                yield break;

            player.IsReady = true;
            RaisePlayersChanged();
        }

        private IEnumerator SimulateJoinResponse(string pseudo, int teamIndex)
        {
            yield return new WaitForSeconds(joinResponseDelay);

            // Le host fictif occupe le slot 0 ; le joueur local prend le suivant.
            if (players.Count == 0)
                players.Add(new PlayerInfo(nextId++, "Hôte", teamIndex: 0, isReady: true));

            LocalPlayerId = nextId++;
            players.Add(new PlayerInfo(LocalPlayerId, string.IsNullOrEmpty(pseudo) ? "Vous" : pseudo,
                teamIndex, isReady: pendingReady));
            RaisePlayersChanged();

            JoinResult?.Invoke(MMPong.UI.JoinResult.Succeeded(LocalPlayerId));

            // Si « prêt » a été demandé pendant le Join, on enchaîne le démarrage simulé du host.
            if (pendingReady)
            {
                pendingReady = false;
                StartCoroutine(SimulateHostStart());
            }
        }

        private IEnumerator SimulateHostStart()
        {
            yield return new WaitForSeconds(fakeHostStartDelay);
            GameStarted?.Invoke();
        }

        private PlayerInfo FindLocal()
        {
            foreach (var p in players)
                if (p.Id == LocalPlayerId)
                    return p;
            return null;
        }

        private void RaisePlayersChanged()
        {
            PlayersChanged?.Invoke(players);
        }
    }
}
