# MMPong — hetic/MMPong

Pong **multijoueur 4+ joueurs** temps réel sous Unity (projet de cours HETIC).
Cours & specs : <https://learn.glassworks.tech/mmporg/> · **Rendu : vendredi 12 juin 2026**
(démo à 4 joueurs + explication du code + dépôt GitHub).

## Documentation du projet

Lire en priorité avant toute contribution :
- [`docs/architecture.md`](docs/architecture.md) — schéma d'architecture (diagrammes Mermaid)
- [`docs/specification.md`](docs/specification.md) — spec technique (protocole, contrat `GameState`, rôles, roadmap, grille /20)
- [`docs/network-roadmap.md`](docs/network-roadmap.md) — échelle d'itération de la couche réseau
- [`docs/network-tasks.md`](docs/network-tasks.md) — tâches réseau détaillées, parallélisables à deux
- [`docs/network-code.md`](docs/network-code.md) — référence concise du code réseau (fichiers de `Assets/MMPong/Network/`)

## Décisions d'architecture (actées — ne pas dévier sans accord équipe)

- **Topologie** : client-serveur **autoritatif** (paradigme « dumb client »).
  Le serveur simule balle/collisions/scores ; les clients **affichent** l'état reçu
  et **envoient** seulement leur input. Démo en **client-host** (un joueur héberge).
- **Transport** : **UDP-only**, un seul socket / un seul port. Octet d'en-tête de
  fiabilité : `reliable=0` best-effort pour le flux d'état (`STATE` à 30 Hz, numéro de
  séquence anti-paquet-périmé) ; `reliable=1` (ack + ré-émission custom) pour les
  événements critiques (lobby, start, end). Repli : ajouter TCP pour le lobby si besoin.
- **Contrat `GameState`** = frontière entre la couche réseau et la couche jeu/UI.
  À ne pas modifier sans accord de l'équipe.
- La logique de jeu « locale » écrite par les coéquipiers **migre côté serveur**
  (elle ne tourne pas sur les clients).

## Convention clé

> **La balle et les scores vivent uniquement sur le serveur.**
> Côté client : on ne *calcule* rien, on *affiche* l'état reçu et on envoie l'input.

## Organisation du code

```
Assets/MMPong/
├── Network/   # Protocol.cs, NetworkServer.cs, NetworkClient.cs, UdpTransport.cs
├── Game/      # GameState.cs, Ball.cs, Paddle.cs (simulation côté serveur)
├── UI/        # Lobby.cs, ScoreUI.cs
└── Scenes/
```

⚠️ Les dossiers `Assets/Demos/*` (Pong, UDP, TCP, MetaVerse) sont des **projets Unity
séparés** : référence uniquement, **non réutilisables** comme assets du jeu.

## Protocole de messages (custom, UDP)

En-tête `[type][reliable][seq]` + payload texte `|`.

- Client → Serveur : `INPUT|playerId|direction` (best-effort) · `JOIN|pseudo`, `READY|playerId` (fiable)
- Serveur → Client : `STATE|seq|ballX|ballY|p1Y|p2Y|p3Y|p4Y|s1|s2|s3|s4` (best-effort) ·
  `WELCOME|playerId`, `LOBBY|n|...`, `START`, `END|winnerId` (fiable) · `ACK|seq`

## Répartition de l'équipe (4 personnes)

| Rôle | Responsable | Périmètre |
|------|-------------|-----------|
| Réseau + intégration | Lucas | `Protocol`, `NetworkServer/Client`, `GameState`, serveur autoritatif |
| Logique de jeu (serveur) | B | physique balle, collisions, scoring → écrit dans `GameState` |
| Client / rendu | C | input → `SendInput`, affichage `OnStateReceived`, interpolation |
| UI / lobby / build | D | phase de connexion, score, exécutable, scène |

## État d'avancement

Phase de cadrage terminée (docs rédigées). Couche réseau : **pilier 1 implémenté**
(`Assets/MMPong/Network/UdpTransport.cs` + `UdpEchoTest.cs`, echo loopback) — reste à
tester dans Unity (`reçu : hello`). Suite : pilier 3 (`GameState`) puis pilier 2 (`Protocol.cs`).
Voir `docs/network-roadmap.md` et `docs/network-tasks.md`.

## Priorité de notation

~85 % de la note = **réseau / synchro**. La 3D et le design ne valent que ~2 pts bonus
→ réutiliser des assets simples, **ne pas sur-investir** dans le visuel.

## Outillage de test

Pour tester le multijoueur en local : **ParrelSync** (cloner le projet, ouvrir 2+ éditeurs
Unity), ou build + éditeur. Indispensable dès le barreau 1.
