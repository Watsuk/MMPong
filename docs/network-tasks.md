# Tâches réseau — MMPong (binôme)

> Liste des tâches de **la couche réseau uniquement** (pas la logique de jeu / rendu / UI,
> qui appartiennent aux autres coéquipiers). Pensée pour un travail **à deux en parallèle**.
> Voir aussi : [`architecture.md`](architecture.md) · [`specification.md`](specification.md) · [`network-roadmap.md`](network-roadmap.md)

## Principe de parallélisation

Un **noyau commun** (le contrat) fait **ensemble** et qui bloque tout, puis **deux pistes
indépendantes** (serveur / client) en parallèle, chacune testée avec une **doublure** de l'autre côté.

```
GROUPE 0 — Fondations (ENSEMBLE, bloque tout)
        │
        ├──────────────┬───────────────┐
        ▼              ▼            (en parallèle)
   GROUPE A         GROUPE B
   Serveur          Client
        │              │
        └──────┬───────┘
               ▼
        GROUPE C — Fiabilité & lobby (points chauds en pairing)
               │
               ▼
        GROUPE E — Optimisation (bonus, à la fin)
```

---

## Groupe 0 — Fondations (ENSEMBLE)

| # | Tâche | Ce qui est attendu | Terminé quand |
|---|-------|--------------------|---------------|
| 0.1 | Format du protocole `Protocol.cs` | `Encode(msg) → byte[]` et `Decode(byte[]) → msg`. En-tête `[type][reliable][seq]` + payload texte. On envoie toujours des **octets**, jamais un objet C#. | `Decode(Encode(x)) == x` passe |
| 0.2 | Structure `GameState` | la `struct` d'état (balle, paddles, scores, seq). Aucune logique. | compile + accord équipe |
| 0.3 | Wrapper UDP + echo loopback ✅ **fait** | `UdpTransport.cs` (`Open`/`Send`/`OnData`/`Close`) + `UdpEchoTest.cs`. Comprendre `Bind` + boucle de réception. | la console affiche `reçu : hello` |

> Une fois ces 3 figés → **on n'y touche plus** sans accord à deux. C'est le contrat.

---

## Groupe A — Côté Serveur (parallèle avec B)

| # | Tâche | Ce qui est attendu | Terminé quand |
|---|-------|--------------------|---------------|
| A.1 | Socket serveur + boucle de réception | `Bind` sur un port, vider la file des datagrammes à chaque frame | les messages entrants sont lus |
| A.2 | Registre des clients | mémoriser `Dictionary<playerId, IPEndPoint>` (UDP est sans connexion → on tient la liste soi-même) | un nouveau client est ajouté à la liste |
| A.3 | Recevoir/appliquer `INPUT` | décoder `INPUT\|id\|dir` → `ApplyInput(id, dir)` | l'input modifie l'état serveur |
| A.4 | Broadcast `STATE` (30 Hz) | encoder le `GameState` et l'envoyer à chaque client du registre | tous les clients reçoivent l'état |
| A.5 | **Doublure : faux client** | script qui spamme des `INPUT` bidons | le serveur se teste **sans** le vrai client |

---

## Groupe B — Côté Client (parallèle avec A)

| # | Tâche | Ce qui est attendu | Terminé quand |
|---|-------|--------------------|---------------|
| B.1 | Socket client | ouvrir un socket, connaître l'`IPEndPoint` du serveur | prêt à émettre/recevoir |
| B.2 | Envoyer `INPUT` | à l'appui touche → encoder + envoyer `INPUT\|id\|dir` | le serveur reçoit l'input |
| B.3 | Recevoir `STATE` → `OnStateReceived` | décoder le state, l'exposer via un **événement** (le client ne calcule rien) | l'event se déclenche avec un `GameState` |
| B.4 | Numéros de séquence | ignorer tout `STATE` dont `seq` < dernier accepté (UDP n'ordonne pas) | un paquet en retard est jeté |
| B.5 | **Doublure : faux serveur** | script qui envoie des `STATE` bidons (balle en rond) | le client se teste **sans** le vrai serveur |

---

## Groupe C — Fiabilité & lobby (points chauds, pairing)

| # | Tâche | Ce qui est attendu | Terminé quand | Owner |
|---|-------|--------------------|---------------|-------|
| C.1 | Couche fiable (ack + resend) | pour `reliable=1` : garder le message et ré-émettre jusqu'à recevoir `ACK\|seq`. **Le morceau le plus délicat.** | un message fiable arrive malgré une perte simulée | **1 seule personne** |
| C.2 | Handshake / lobby | `JOIN → WELCOME(id) → READY → START` ; le serveur attend 4 `READY` | « 4 joueurs prêts » lance la partie | l'autre |
| C.3 | Déconnexion / timeout | retirer du registre un client silencieux depuis X s | un client parti disparaît proprement | l'autre |

---

## Groupe E — Optimisation (bonus, fin)

| # | Tâche | Ce qui est attendu |
|---|-------|--------------------|
| E.1 | Encodage binaire | remplacer le payload texte `\|` par du binaire → moins d'octets |
| E.2 | Envoi sur changement / delta | n'envoyer que ce qui a bougé |
| E.3 | Hooks d'interpolation (client) | lisser le mouvement entre 2 snapshots malgré 30 Hz |

---

## Ce qui se parallélise

| Phase | Personne 1 | Personne 2 | Parallèle ? |
|-------|-----------|-----------|-------------|
| Groupe 0 | contrat + echo | contrat + echo | ❌ ensemble |
| Groupes A & B | Serveur (A) | Client (B) | ✅ oui |
| Groupe C | fiabilité (C.1) | lobby (C.2 / C.3) | 🟡 points chauds en pairing |
| Groupe E | optim | optim | ✅ oui |

> Clé du parallélisme : **les doublures A.5 et B.5**. Elles permettent à chacun de
> tester son côté sans attendre l'autre. Sans elles, blocage mutuel.

## Règles d'équipe (hygiène Git)

- **Un fichier = un propriétaire.** Les fichiers communs (`Protocol.cs`, `GameState`)
  sont figés après le Groupe 0 ; tout changement se décide à deux.
- **Intégration quotidienne**, pas en big-bang. Le réseau cache ses bugs à l'intégration.
- **Points chauds en pairing** : premier bout-en-bout réel (A+B branchés) et la couche fiable.
