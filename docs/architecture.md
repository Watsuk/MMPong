# Architecture réseau — MMPong

> Schéma d'architecture du projet. Les diagrammes sont en [Mermaid](https://mermaid.js.org/)
> et se rendent automatiquement sur GitHub.

## 1. Topologie : Client-Serveur autoritatif

Un **serveur** détient l'unique vérité de l'état du jeu. Les clients n'affichent
que ce que le serveur leur envoie, et ne remontent que l'input du joueur.

```mermaid
graph TD
    S["🖥️ SERVEUR (autoritatif)<br/>— détient le GameState (vérité)<br/>— simule la balle + collisions<br/>— calcule les scores<br/>— tick fixe 30 Hz"]

    C1["🎮 Client 1<br/>affiche + envoie son input"]
    C2["🎮 Client 2<br/>affiche + envoie son input"]
    C3["🎮 Client 3<br/>affiche + envoie son input"]
    C4["🎮 Client 4<br/>affiche + envoie son input"]

    C1 -- "INPUT (UDP)" --> S
    C2 -- "INPUT (UDP)" --> S
    C3 -- "INPUT (UDP)" --> S
    C4 -- "INPUT (UDP)" --> S

    S -- "STATE 30 Hz (UDP)" --> C1
    S -- "STATE 30 Hz (UDP)" --> C2
    S -- "STATE 30 Hz (UDP)" --> C3
    S -- "STATE 30 Hz (UDP)" --> C4
```

**Pourquoi ce choix** (paradigme « dumb client » du cours) :
- Une seule source de vérité → pas de désaccord entre clients sur la position de la balle.
- La physique partagée (balle) est simulée **une seule fois**, sur le serveur.
- Les conflits (race conditions) sont arbitrés par le serveur en *first-come, first-served*.

> Pour la démo, le serveur est hébergé par un joueur (**client-host**) : une seule
> instance fait à la fois serveur ET client local. Plus simple à déployer en 10 jours.

## 2. Transport unique : UDP avec deux canaux logiques

**Tout passe par UDP, sur un seul socket et un seul port.** Un octet d'en-tête dans
chaque message indique son niveau de fiabilité. C'est l'approche des vraies libs de
netcode (ENet, GameNetworkingSockets, QUIC) : un seul tuyau, plusieurs « canaux ».

```mermaid
graph LR
    subgraph Client
      I[Input joueur]
      R[Rendu / affichage]
      L[Lobby & connexion]
    end

    subgraph Serveur
      G[GameState autoritatif]
      LOBBY[Gestion connexions]
    end

    I -- "UDP best-effort : INPUT" --> G
    G -- "UDP best-effort : STATE 30 Hz" --> R
    L -- "UDP fiable (ack+resend) : JOIN / READY / START" --> LOBBY
    LOBBY -- "UDP fiable : événements critiques" --> L
```

En-tête de message :

```
[type] [reliable] [seq] [payload...]
   │        │        │
   │        │        └─ numéro de séquence
   │        └─ 0 = best-effort (STATE/INPUT) | 1 = fiable (START/END/JOIN)
   └─ type de message
```

| Canal logique | Fiabilité | Contenu | Pourquoi |
|---------------|-----------|---------|----------|
| **État de jeu** | best-effort (`reliable=0`) | positions balle/paddles, scores (30 msg/s) | On veut le paquet le **plus frais**, pas la fiabilité. La perte est OK (le suivant corrige). |
| **Événements** | fiable (`reliable=1`) | connexion, « prêt », démarrage, fin de partie | Rares mais **critiques** : la perte est inacceptable. Géré par **ack + ré-émission** (~30 lignes). |

> **Pourquoi UDP-only et pas un hybride TCP/UDP** : un seul socket, un seul port
> (déploiement internet trivial), un seul modèle mental, pas de *framing* TCP à gérer.
> La fiabilité des rares événements critiques tient en une petite couche ack/resend
> custom — ce qui rapporte aussi les points « protocole custom » et « race conditions ».
>
> **Repli** : si la couche fiable se révèle galère près de la deadline, ajouter un
> socket TCP juste pour le lobby en filet de secours.

## 3. Boucle de synchronisation (1 tick serveur)

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Serveur

    Note over C: Le joueur appuie haut/bas
    C->>S: INPUT|playerId|direction  (UDP best-effort)
    Note over S: 1) applique les inputs reçus<br/>2) avance la balle (physique)<br/>3) détecte collisions / buts<br/>4) met à jour les scores
    S->>C: STATE|seq|ball|paddles|scores  (UDP best-effort, broadcast)
    Note over C: ignore si seq < dernier reçu<br/>sinon affiche le nouvel état
```

## 4. Couches de code (séparation des responsabilités)

```mermaid
graph TD
    subgraph "Couche Réseau (toi)"
      P[Protocol.cs<br/>encode/décode + en-tête fiabilité]
      NS[NetworkServer.cs<br/>socket UDP serveur]
      NC[NetworkClient.cs<br/>socket UDP client]
    end

    subgraph "Couche Jeu (coéquipiers)"
      GS[GameState<br/>structure partagée]
      SIM[Simulation balle/collisions<br/>tourne CÔTÉ SERVEUR]
      REN[Rendu + Input<br/>tourne CÔTÉ CLIENT]
    end

    subgraph "Couche UI (coéquipier)"
      UI[Lobby / Score / Build]
    end

    SIM --> GS
    GS --> P
    P --> NS
    P --> NC
    NC --> GS
    GS --> REN
    UI --> NC
```

> **Le contrat `GameState` est la frontière.** Tant que chaque couche le respecte,
> les travaux de chacun se branchent sans friction. Voir `specification.md`.

## 5. Fluidité & scalabilité (note)

Le serveur autoritatif introduit une latence (aller-retour vers le serveur). Ce n'est
pas un bug mais de la physique : on ne la supprime pas, on la **cache** côté client.
**Autorité** (qui détient la vérité) et **affichage** (ce qu'on rend tout de suite)
sont deux axes séparés — on garde l'autorité serveur ET on rend fluide via :

- **Interpolation** des objets distants (balle, paddles) entre deux snapshots → mouvement lisse malgré 30 Hz.
- **Client-side prediction** de son propre paddle → zéro latence ressentie sur ses actions.
- **Reconciliation** : correction douce quand la vérité serveur contredit la prédiction.

À grande échelle, le goulot n'est pas l'autorité mais la **bande passante** (état en N²) :
delta compression, encodage binaire, interest management. Pour 4 joueurs, inutile —
interpolation + prediction suffisent (barreau 9 de la roadmap).
