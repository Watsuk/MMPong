# Feuille de route réseau — MMPong

> Scope personnel du rôle **réseau + intégration** (Lucas).
> Méthode : **architecture d'abord** (débloquer l'équipe), **puis** implémentation
> réseau par itérations testables. On ne monte d'un barreau que quand le précédent marche.

Voir aussi : [`architecture.md`](architecture.md) · [`specification.md`](specification.md)

---

## Vue d'ensemble en deux phases

```
PHASE A — Architecture (Jour 1, débloque toute l'équipe)
  squelette → spike echo (apprendre) → contrat GameState → stubs → push

PHASE B — Implémentation réseau (Jours 2+, remplace les stubs un par un)
  2 machines → Protocol → 1 paddle réseau → balle serveur
  → 4 joueurs → robustesse → lobby → optim
```

**Principe clé** : on livre d'abord une app qui **compile et tourne de bout en bout**
avec un réseau **bouchonné (faux)** — un *walking skeleton*. Ça débloque B/C/D
immédiatement et valide l'architecture avant d'écrire le vrai code réseau. Ensuite on
remplace les stubs un par un, sans jamais casser l'app.

---

## 🏗️ Phase A — Architecture (Jour 1)

Objectif : une coquille fonctionnelle + le contrat partagé, pour que toute l'équipe
puisse coder en parallèle dès aujourd'hui.

| # | Action | Durée | But |
|---|--------|-------|-----|
| A1 | **Squelette** : `Assets/MMPong/{Network,Game,UI,Scenes}` + scène vide | 20 min | structure partagée |
| A2 | **Spike echo (jetable)** : echo loopback UDP, juste pour apprendre | 1–2 h | valider que l'UDP marche **avant** de figer le contrat |
| A3 | **Contrat v0** : struct `GameState` + interfaces | 1 h | la frontière de l'architecture |
| A4 | **Stubs** : un `FakeNetwork` qui renvoie un `GameState` bidon en local | 1 h | l'app tourne, l'équipe est débloquée |
| A5 | **Push + brief équipe** | 30 min | tout le monde code contre le contrat |

> ⚠️ Le **spike echo (A2) est jetable** : il sert à apprendre les sockets et à
> informer le contrat, pas à être conservé. On évite ainsi de designer un `GameState`
> théorique qui ne colle pas à la réalité réseau (piège de l'interface-first quand
> on débute un domaine).

**Bonus archi optionnel** : créer des **assembly definitions** (`.asmdef`) sur
`Network/`, `Game/`, `UI/` pour forcer les dépendances dans le bon sens (`Game` ne
peut pas appeler les sockets directement, seulement via le contrat). Argument fort
pour les 3 pts « clean architecture » à l'oral. Non bloquant.

---

## 🪜 Phase B — Implémentation réseau (Jours 2+)

On remplace les stubs de la Phase A par du vrai réseau, un barreau à la fois.
Chaque barreau est testable seul et enseigne **un** concept.

| # | Tu construis | Concept appris | Comment vérifier | Lien grille |
|---|--------------|----------------|------------------|-------------|
| **2** | **2 instances** (2 ports) : A envoie, B reçoit | client vs serveur, `IPEndPoint` | 2 fenêtres, le texte passe de l'une à l'autre | paquets (1 pt) |
| **3** | **`Protocol.cs`** : encode/décode `INPUT\|1\|-1` au lieu d'une string brute | sérialisation, protocole custom | round-trip : encode → décode → mêmes valeurs | protocole (2 pts) |
| **4** | **1 paddle en réseau** : input client → serveur applique au `GameState` → renvoie position → client affiche | la boucle input→état→rendu (cœur autoritatif, en miniature) | bouger sur A, voir bouger sur B | **sync position (3 pts)** |
| **5** | **Balle côté serveur** : la balle vit sur le host, `STATE` diffusé à **30 Hz** | tick serveur / game loop, timestep fixe | la balle bouge identiquement sur les 2 écrans | sync gestes (2 pts) |
| **6** | **Passage à 4 clients** : le serveur tient un registre d'endpoints, broadcast à tous, chaque client a un `playerId` | registre clients, broadcast multi-client | 4 fenêtres, 4 paddles synchronisés | MVP complet |
| **7** | **Robustesse** : numéro de séquence → on ignore les paquets périmés | UDP n'ordonne/ne garantit rien | couper le réseau 1s, ça récupère sans bug | race conditions (3 pts) |
| **8** | **Canal fiable UDP** (ack + resend) : `JOIN` / `READY` / `START` | fiabilité custom sur UDP, handshake | « 4 joueurs prêts » avant de lancer | connexion (2 pts) |
| **9** | **Optim** : binaire, envoi sur changement, dead reckoning | latence / bande passante | mesurer les octets/s, mouvement fluide | **optim (jusqu'à 8 pts)** |

**Phase A + barreaux 2→6 = MVP** (les 11 pts). **7→9 = bonus.**

---

## 🎯 Les 3 checkpoints qui comptent

1. **Barreau 4** — « je bouge un paddle sur une machine, il bouge sur l'autre ».
   Le jour où ça marche, toute la chaîne est prouvée. Le reste est de la répétition.
2. **Barreau 6** — 4 joueurs synchronisés = MVP démontrable.
3. **Barreau 9** — l'optim, là où se gagnent les grosses notes bonus.

---

## 🛠️ Outillage de test (à mettre en place dès la Phase A)

Tester du multijoueur = faire tourner **plusieurs instances Unity** simultanément :

- **ParrelSync** (recommandé) — clone le projet pour ouvrir 2+ éditeurs côte à côte. Itération rapide.
- **Build + éditeur** — un build standalone + l'éditeur. Plus lent, zéro dépendance.
- **2 builds** — le plus lent, à réserver à la démo finale.

---

## ✅ Définition de « terminé » (frontière du scope)

Livrables réseau : `Protocol.cs`, `NetworkServer.cs`, `NetworkClient.cs`, la struct
`GameState` et ses interfaces (`OnStateReceived`, `SendInput`, `ApplyInput`, `Tick`).
Au-delà du contrat `GameState` → couche jeu/UI des coéquipiers.
