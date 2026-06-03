# Documentation du code réseau — MMPong

> Référence concise du code réseau, dans `Assets/Demos/Pong/` (dossier du jeu).
> État actuel : transport UDP + (dé)sérialisation des messages.
> Voir aussi [`network-roadmap.md`](network-roadmap.md).

## Convention de documentation (C#)

L'équivalent C# de la JSDoc = les **commentaires de documentation XML** (`/// <summary>…`).
Placés au-dessus des membres publics, ils s'affichent en infobulle dans l'IDE et peuvent
générer une doc HTML (DocFX). Le **corps** des méthodes reste sans commentaires : le code
doit se lire seul.

---

## `Network/UdpTransport.cs`

Wrapper réutilisable au-dessus de `UdpClient`. Cache les sockets bas niveau pour offrir
un envoi/réception d'**octets bruts**. Modèle proche d'un **WebSocket** : on `Send` sans
attendre de réponse, et on reçoit via l'événement `OnData`. Ne connaît pas le sens des
messages (c'est le rôle de `Protocol`).

| Membre | Signature | Rôle |
|--------|-----------|------|
| `Open` | `void Open(int listenPort)` | Ouvre le socket et réserve le port (`Bind`). |
| `Send` | `void Send(byte[] data, IPEndPoint destination)` | Envoie des octets (fire-and-forget). |
| `OnData` | `event Action<byte[], IPEndPoint>` | Déclenché à chaque datagramme reçu. |
| `IsOpen` | `bool` | Indique si le socket est ouvert. |
| `Close` | `void Close()` | Ferme le socket et libère le port. |

Réception par *polling* dans `Update()` (pas de thread, car l'API Unity n'est accessible
que depuis le thread principal). `ReuseAddress` activé ; fermeture auto dans `OnDisable()`.

---

## `GameState.cs`

`struct` représentant l'état autoritatif du **jeu circulaire**, diffusé dans chaque message
`State`. Données uniquement, aucune logique. Taille des listes = nombre de joueurs N (variable).

| Champ | Type | Rôle |
|-------|------|------|
| `seq` | `uint` | numéro de snapshot (ignorer les paquets périmés) |
| `paddleAngle` | `float[]` | angle (degrés) de chaque paddle sur le cercle ; index = playerId |
| `ballPos` | `Vector2` | position de la balle (plan z = 0) |
| `ballOwner` | `int` | dernier joueur à avoir touché la balle (-1 = aucun) → couleur |
| `scores` | `int[]` | score de chaque joueur |
| `phase` | `GamePhase` | `WaitingForServe` / `Playing` / `GameOver` |
| `winner` | `int` | playerId du gagnant (-1 = aucun) |

> Le paddle est transmis par son **angle** (la vraie variable autoritative), pas sa position :
> le client reconstruit position + rotation depuis le rayon/centre partagés de la scène.
> `phase` est découplé de l'enum gameplay `PongBallState` (mapping fait aux coutures serveur/client).

---

## `Network/Protocol.cs`

Traduit entre des **messages qui ont du sens** et des **octets**, dans les deux sens.
Garantie : `Parse(Build(x)) == x`.

**Format du fil** (texte UTF-8) : `type|seq|reliable|champ0|champ1|...`
- `type` : nom de l'enum (`Input`, `State`, …)
- `seq` : numéro de séquence (`uint`)
- `reliable` : `0` (best-effort) ou `1` (fiable)
- séparateurs : `|` entre champs, `,` pour les listes ; nombres en `InvariantCulture`

### Types principaux
- `enum MessageType { Input, State, Join, Ready, Welcome, Lobby, Start, End, Ack }`
- `struct Message { MessageType type; uint seq; bool reliable; string[] fields; }`

### API
| Niveau | Membres | Usage |
|--------|---------|-------|
| Enveloppe | `byte[] Encode(Message)` · `Message Decode(byte[])` | utilisé par la couche réseau |
| Helpers | `BuildInput`/`ParseInput`, `BuildState`/`ParseState`, `BuildJoin`/`ParseJoin`, `BuildReady`/`ParseReady`, `BuildWelcome`/`ParseWelcome`, `BuildLobby`/`ParseLobby`, `BuildStart`, `BuildEnd`/`ParseEnd`, `BuildAck`/`ParseAck` | utilisés par le code de jeu |

Points clés :
- **La fiabilité est portée par le type** : les `Build*` posent `reliable` au bon état
  (Input/State/Ack = best-effort ; Join/Ready/Welcome/Lobby/Start/End = fiable).
- `seq` vaut 0 par défaut (estampillé plus tard par la couche d'envoi) ; `BuildState` reprend `s.seq`.
- Floats formatés en `"R"` (round-trip sans perte).
- Pseudos **nettoyés** des séparateurs (`|`, `,`).

---

## `Network/NetworkServer.cs`

Serveur **autoritatif**. Tient le registre des clients (`Dictionary<int, IPEndPoint>`),
reçoit les `INPUT`, et à **cadence fixe** (tick 30 Hz, accumulateur découplé du framerate)
**délègue à `ServerGameBridge`** la simulation, puis **diffuse** le `GameState` résultant.

| Réception | Effet |
|-----------|-------|
| `Join` | attribue le prochain `playerId` libre (réutilise si l'endpoint est connu), répond `Welcome(id)` |
| `Input` | mémorise la dernière intention `pendingInput[id]` |

À chaque tick : `bridge.ApplyInput(pendingInput)`, `state = bridge.BuildState(++seq)`,
`Broadcast(BuildState(state))`. Sans bridge, le serveur ne simule rien.

---

## `Network/ServerGameBridge.cs`

Colle entre le réseau et le jeu : `NetworkServer` reste générique, **toute la connaissance
du Pong est ici**. Référence les vrais `PongPaddle[]` (index = playerId) et le `PongBall`.

| Membre | Rôle |
|--------|------|
| `ApplyInput(float[])` | écrit `pendingInput[i]` dans `paddles[i].ExternalDirection` |
| `BuildState(seq)` | lit `CurrentAngle`/`ballPos`/`LastHitter`/scores/`State` → `GameState` |

`Start()` passe les paddles en `DrivenExternally = true`. Mappe `PongBallState` → `GamePhase` +
`winner` (`PlayerLeftWin → GameOver, winner=0` ; `PlayerRightWin → GameOver, winner=1`).

## `Network/GameBootstrap.cs`

Point d'entrée qui choisit le mode : `GameMode { Local, Host, Client }`, **`Local` par défaut**
(jeu local inchangé). Expose `StartHost()` et `StartClient(string ip)` (appelables par
l'inspecteur via `mode`, par le futur menu, ou par `DevNetLauncher`). Crée la couche réseau
**par code** (aucun prefab, scène intacte), paddles/balle récupérés via `FindObjectsByType`.

- `StartHost()` : serveur (`UdpTransport`+`ServerGameBridge`+`NetworkServer`) + un client local
  (joueur host, envoie son input, affiche sa **propre** sim → pas d'applier).
- `StartClient(ip)` : passe paddles/balle en `RemoteDisplay`, crée un client (`serverIp = ip`,
  port d'écoute **éphémère 0**) + `ClientStateApplier`.

> Seams gameplay (inertes en local) : `PongPaddle.DrivenExternally`/`ExternalDirection`/
> `CurrentAngle`/`RemoteDisplay`/`ApplyNetworkAngle`, et `PongBall.LastHitter`/`RemoteDisplay`.

## `Network/ClientStateApplier.cs`

Côté client : abonné à `OnStateReceived`, **affiche** l'état sans simuler. Positionne chaque
paddle via `ApplyNetworkAngle(angle)` (reconstruit position+rotation depuis le rayon/centre, valide
tant que `PongGameManager` aléatoire n'est pas en scène) et fixe `ball.transform.position`.

## `Network/DevNetLauncher.cs` (jetable)

Lanceur IMGUI (`OnGUI`, zéro UI à câbler) : champ IP + boutons « Héberger »/« Rejoindre » →
`GameBootstrap.StartHost()`/`StartClient(ip)`. **À supprimer** quand le vrai menu (coéquipier)
appellera ces méthodes.

---

## `Network/NetworkClient.cs`

**Miroir** du serveur, côté joueur (paradigme *client bête*). Deux rôles seulement :
capter l'input clavier → l'envoyer en `INPUT`, et exposer les `STATE` reçus.

| Membre | Signature | Rôle |
|--------|-----------|------|
| `OnStateReceived` | `event Action<GameState>` | **frontière du contrat** : la couche jeu s'y abonne pour afficher |
| `SendInput` | `void SendInput(float dir)` | envoie l'intention courante (réseau pur, réutilisable) |
| `PlayerId` | `int` | id attribué par le serveur (-1 tant que pas de `Welcome`) |

`Start()` envoie `Join(pseudo)`. À la réception : `Welcome` → mémorise `PlayerId` ;
`State` → `OnStateReceived(ParseState(m))`. `Update()` lit `Input.GetAxisRaw("Vertical")`
et appelle `SendInput` à **cadence fixe** (`sendRate`, 30 Hz, même accumulateur que le
serveur) : on **renvoie l'intention courante** plutôt que des événements → robuste aux
pertes UDP (un paquet perdu est rattrapé au pas suivant), bande passante bornée.

---

## `Network/ProtocolTest.cs` · `Network/ClientStateLogger.cs`

Composants **jetables**.
- `ProtocolTest` : allers-retours dans `Start()`, logge le bilan (`[ProtocolTest] 5/5 OK`).
- `ClientStateLogger` : s'abonne à `NetworkClient.OnStateReceived` et logge (throttlé) la
  position du paddle local — vérifie la boucle bout-en-bout sans rendu, et sert d'exemple
  de consommation du contrat.

---

## À venir
- Vrai menu (coéquipier) appelant `StartHost`/`StartClient` → suppression de `DevNetLauncher`.
- Sync scores / phase / UI de victoire côté client (`PongWinUI` sur `phase`).
- Interpolation (lerp) des positions reçues (STATE 30 Hz vs rendu 60 fps) ; pour l'instant snap.
- Cercles autoritatifs si `PongGameManager` (aléatoire) est ajouté à la scène.
- Estampillage/lecture du `seq` côté client (ignorer les paquets périmés).
- Couche fiable (ack + ré-émission) pour `Join`/`Welcome`/`Start`.
- Passage du payload en binaire (optimisation).
