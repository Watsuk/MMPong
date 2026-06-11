# Spécification technique — MMPong

> Projet : transformer le Pong 2 joueurs en version multijoueur **4+ joueurs** temps réel.
> Cours : <https://learn.glassworks.tech/mmporg/>
> Rendu : **vendredi 12 juin 2026** — démonstration à 4 joueurs + explication du code.

---

## 1. Objectif et contraintes

| Contrainte (specs) | Décision projet |
|--------------------|-----------------|
| Plus de 4 joueurs simultanés | 4 paddles sur un terrain (carré) |
| Protocole **custom** TCP ou UDP (pas de netcode Unity intégré) | **UDP-only**, protocole maison avec en-tête de fiabilité |
| Synchro temps réel haute fréquence | Tick serveur **30 Hz** |
| Implémentation C# / Unity3D | Unity, scripts dans `Assets/` |

> ⚠️ Tout le jeu vit dans `Assets/` (scène `Pong.unity` à la racine). Les anciens
> projets de démo (`Assets/Demos/`) ont été supprimés du repo lors du clean.

---

## 2. Architecture retenue

- **Topologie** : Client-Serveur (recommandé par le cours).
- **Paradigme de synchro** : **Dumb client / serveur autoritatif**.
  - Le serveur simule la balle, les collisions et les scores.
  - Les clients **n'affichent** que l'état reçu et **n'envoient** que leur input.
- **Déploiement démo** : un joueur est **host** (serveur + client local).

Voir `architecture.md` pour les diagrammes.

---

## 3. Transport réseau — UDP-only

**Tout passe par UDP**, un seul socket, un seul port. Deux canaux logiques distingués
par un octet de fiabilité dans l'en-tête de chaque message.

### Canal best-effort (`reliable=0`) — flux d'état
- Le serveur diffuse `STATE` à **30 Hz** à tous les clients.
- Les clients envoient `INPUT` quand leur direction change.
- Pas de garantie d'ordre → **numéro de séquence** : un client ignore tout `STATE`
  dont le `seq` est inférieur au dernier reçu (gestion des paquets périmés).
- Perte de paquet sans conséquence : le snapshot suivant corrige.

### Canal fiable (`reliable=1`) — événements critiques
- Connexion / lobby, démarrage, fin de partie.
- Fiabilité assurée par une petite couche **ack + ré-émission** maison (~30 lignes) :
  l'expéditeur garde le message jusqu'à recevoir l'accusé, sinon ré-émet.

> **Pourquoi UDP-only plutôt qu'un hybride TCP/UDP** : un seul port (déploiement
> internet trivial), un seul socket, un seul modèle mental, pas de *framing* TCP à
> gérer (TCP est un flux d'octets, UDP est orienté message). La couche fiable custom
> rapporte en plus les points « protocole custom » et « race conditions ».
>
> **Repli** : si la couche fiable pose problème près de la deadline, ajouter un socket
> TCP uniquement pour le lobby.

Base technique : l'API C# `UdpClient` (`System.Net.Sockets`), encapsulée dans
`Assets/Network/UdpTransport.cs` (pilier 1, déjà implémenté).

---

## 4. Protocole de messages (custom)

En-tête binaire suivi d'un payload texte, séparateur `|` (lisible et débuggable ;
passage du payload en binaire possible plus tard pour l'optim de bande passante).

```
[type:1o] [reliable:1o] [seq:4o] [payload texte...]
```

### Client → Serveur

| Message | Payload | Fiabilité |
|---------|---------|-----------|
| Input joueur | `INPUT\|playerId\|direction` | best-effort |
| Rejoindre | `JOIN\|pseudo` | fiable |
| Prêt | `READY\|playerId` | fiable |

`direction` ∈ `{-1, 0, 1}`.

### Serveur → Client

| Message | Payload | Fiabilité |
|---------|---------|-----------|
| État de jeu | `STATE\|seq\|ballX\|ballY\|p1Y\|p2Y\|p3Y\|p4Y\|s1\|s2\|s3\|s4` | best-effort |
| Attribution ID | `WELCOME\|playerId` | fiable |
| Liste lobby | `LOBBY\|n\|pseudo1,pseudo2,...` | fiable |
| Démarrage | `START` | fiable |
| Fin de partie | `END\|winnerId` | fiable |
| Accusé de réception | `ACK\|seq` | (réponse aux messages fiables) |

> Exemple de payload : `STATE|184|2.31|-0.50|1.2|-3.0|0.0|2.5|3|1|0|2`

---

## 5. Contrat partagé : `GameState`

Frontière entre la couche réseau et la couche jeu. **À ne pas modifier sans accord
de l'équipe.**

```csharp
// L'état autoritatif du jeu, sérialisé dans chaque message STATE.
public struct GameState {
    public int      seq;        // numéro de séquence (anti-paquet-périmé)
    public Vector2  ballPos;    // position de la balle
    public float[]  paddleY;    // position verticale des 4 paddles
    public int[]    scores;     // score des 4 joueurs
}
```

Interfaces côté client (les coéquipiers les utilisent sans toucher aux sockets) :

```csharp
// Côté client : écouter l'état, envoyer l'input.
public event Action<GameState> OnStateReceived;
public void SendInput(int playerId, float direction);

// Côté serveur : la simulation écrit ici, le réseau diffuse.
public GameState Current { get; }
public void ApplyInput(int playerId, float direction);
public void Tick(float deltaTime);   // avance la balle, collisions, scores
```

> **Stubs dès J2** : une fausse couche réseau renverra un `GameState` factice en local
> pour que rendu / UI / simulation avancent sans attendre l'UDP.

---

## 6. Organisation du code

```
Assets/
├── Network/
│   ├── UdpTransport.cs    # ✅ pilier 1 : wrapper UDP (Open/Send/OnData/Close)
│   ├── Protocol.cs        # encode/décode + en-tête de fiabilité (§4)
│   ├── NetworkServer.cs   # socket UDP côté serveur + ack/resend
│   └── NetworkClient.cs   # socket UDP côté client + ack/resend
├── Game/
│   ├── GameState.cs       # structure partagée (§5)
│   ├── Ball.cs            # physique balle (tourne sur le serveur)
│   └── Paddle.cs          # paddle joueur
├── UI/
│   ├── Lobby.cs           # phase de connexion
│   └── ScoreUI.cs
└── Scenes/
```

---

## 7. Répartition de l'équipe (4 personnes)

| Rôle | Responsable | Tâches |
|------|-------------|--------|
| **Réseau + intégration** | Lucas | `Protocol`, `NetworkServer/Client`, `GameState`, serveur autoritatif, stubs |
| Logique de jeu (serveur) | B | Physique balle, collisions, scoring → écrit dans `GameState` |
| Client / rendu | C | Capture input → `SendInput`, affiche `OnStateReceived`, interpolation |
| UI / lobby / build | D | Phase de connexion, écran score, exécutable, scène 3D |

> Règle d'équipe : **la balle et les scores vivent uniquement sur le serveur.**
> Côté client, on n'en *calcule* aucun — on *affiche* l'état reçu.

---

## 8. Feuille de route (10 jours)

### Phase 1 — MVP (J1 → J5) — vise les 11 pts
1. Réécrire la couche UDP (envoi/réception propre).
2. Sync position : 1 paddle de la machine A visible sur la machine B (**3 pts**).
3. Serveur autoritatif : la balle vit sur le host, diffusée à tous.
4. Pong 2 → 4 paddles.
5. Sync des inputs des 4 joueurs (**2 pts**).

### Phase 2 — Bonus (J6 → J9)
- Couche fiable (ack/resend) + numéros de séquence (protocole custom, race conditions).
- Phase de connexion / lobby via le canal fiable UDP (**2 pts**).
- Optim latence / bande passante : tick fixe, interpolation, dead reckoning (**jusqu'à 8 pts**).
- UI de fin / score (**2 pts**).

### Phase 3 — Livraison (J10)
- Build exécutable, répétition de la démo à 4 machines.
- Préparation de l'explication : architecture, structures de données, algorithmes.

---

## 9. Grille de notation (rappel — /20)

| Bloc | Points |
|------|--------|
| MVP (exécutable, UI, paquets, sync position ×3, sync gestes ×2) | 11 |
| Qualité du code / architecture | 3 |
| Bonus (protocole custom 2, connexion 2, optim latence/BP **8**, objet partagé 2, multi-perso 4, race conditions 3, UI initiale 2, déploiement internet 3, graphismes 2, autres 3) | jusqu'à 8 retenus |

> Lecture clé : **~85 % de la note est du réseau/synchro.** La 3D et le design ne
> valent presque rien (2 pts bonus) → réutiliser les assets, ne pas sur-investir.
