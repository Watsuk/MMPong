using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMPong.Network;

/// <summary>
/// Affiche, sous chaque label d'équipe du HUD (<see cref="PongScore.leftTeamLabel"/> = équipe A,
/// <see cref="PongScore.rightTeamLabel"/> = équipe B), la liste des joueurs du match (pseudos),
/// chacun précédé/suivi d'un point d'état clignotant : <b>vert = connecté</b>, <b>rouge = déconnecté</b>.
///
/// La liste est alimentée par le serveur via <see cref="NetworkClient.OnLobbyDetailed"/>
/// (pseudo + équipe par joueur). Composant créé au runtime par <c>GameBootstrap</c> (comme
/// <c>ClientStateApplier</c>) : aucune dépendance de scène hormis le HUD de score existant.
///
/// <b>Périmètre actuel (UI uniquement)</b> : l'état connecté/déconnecté n'est pas encore piloté
/// par le serveur. Tous les joueurs présents au lobby sont considérés connectés (point vert).
/// <see cref="SetConnected"/> est le point de couture pour la future détection de déconnexion
/// côté serveur (basculera le point en rouge).
/// </summary>
public class TeamRosterHUD : MonoBehaviour
{
    const float RowHeight = 26f;
    const float DotSize = 14f;
    const float TopMargin = 8f;     // espace entre le label d'équipe et la 1re ligne
    const float DotGap = 6f;        // espace entre le point et le pseudo
    const float BlinkSpeed = 4f;    // vitesse de clignotement (rad/s)

    static readonly Color ConnectedColor = new Color(0.30f, 0.90f, 0.36f);    // vert
    static readonly Color DisconnectedColor = new Color(0.95f, 0.30f, 0.30f); // rouge

    /// <summary>Une ligne joueur affichée (pseudo + point d'état).</summary>
    class Row
    {
        public int playerId;
        public bool connected = true;
        public Image dot;
        public GameObject go;
    }

    RectTransform leftContainer;
    RectTransform rightContainer;
    readonly List<Row> rows = new List<Row>();
    static Sprite circleSprite;

    /// <summary>
    /// (Re)construit la liste des joueurs par équipe. Appelé à chaque LOBBY reçu
    /// (<see cref="NetworkClient.OnLobbyDetailed"/>). L'état connecté connu est préservé
    /// d'une mise à jour à l'autre.
    /// </summary>
    public void SetPlayers(LobbyPlayerInfo[] players)
    {
        EnsureContainers();
        if (leftContainer == null && rightContainer == null) return;
        if (players == null) players = System.Array.Empty<LobbyPlayerInfo>();

        // Mémorise l'état connecté courant avant reconstruction
        var prevConnected = new Dictionary<int, bool>();
        foreach (var r in rows) prevConnected[r.playerId] = r.connected;
        ClearRows();

        int leftCount = 0, rightCount = 0;
        foreach (var p in players)
        {
            if (string.IsNullOrEmpty(p.pseudo)) continue;
            bool toLeft = p.team == 0;
            RectTransform container = toLeft ? leftContainer : rightContainer;
            if (container == null) continue;
            int index = toLeft ? leftCount++ : rightCount++;
            bool connected = !prevConnected.TryGetValue(p.id, out bool c) || c;
            CreateRow(container, p, index, toLeft, connected);
        }
    }

    /// <summary>
    /// Bascule l'état connecté/déconnecté d'un joueur. Point de couture pour la future
    /// détection de déconnexion côté serveur (non câblé pour l'instant).
    /// </summary>
    public void SetConnected(int playerId, bool connected)
    {
        foreach (var r in rows)
            if (r.playerId == playerId) r.connected = connected;
    }

    void Update()
    {
        if (rows.Count == 0) return;
        // Alpha oscillant 0.15 → 1 pour l'effet clignotant.
        float a = Mathf.Lerp(0.15f, 1f, (Mathf.Sin(Time.time * BlinkSpeed) + 1f) * 0.5f);
        foreach (var r in rows)
        {
            if (r.dot == null) continue;
            Color col = r.connected ? ConnectedColor : DisconnectedColor;
            col.a = a;
            r.dot.color = col;
        }
    }

    void EnsureContainers()
    {
        if (leftContainer != null || rightContainer != null) return;
        PongScore score = FindFirstObjectByType<PongScore>();
        if (score == null)
        {
            Debug.LogWarning("[TeamRosterHUD] PongScore introuvable : impossible d'ancrer la liste des joueurs.");
            return;
        }
        if (score.leftTeamLabel != null)
            leftContainer = CreateContainer(score.leftTeamLabel.rectTransform, "RosterLeft", true);
        if (score.rightTeamLabel != null)
            rightContainer = CreateContainer(score.rightTeamLabel.rectTransform, "RosterRight", false);
    }

    /// <summary>Crée un container ancré juste sous le label d'équipe, côté gauche ou droit.</summary>
    RectTransform CreateContainer(RectTransform label, string name, bool left)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(label, false);
        // Ancré sur le bord bas du label ; le container grandit vers le bas.
        rt.anchorMin = new Vector2(left ? 0f : 1f, 0f);
        rt.anchorMax = new Vector2(left ? 0f : 1f, 0f);
        rt.pivot = new Vector2(left ? 0f : 1f, 1f);
        rt.sizeDelta = new Vector2(label.sizeDelta.x, 0f);
        rt.anchoredPosition = new Vector2(0f, -TopMargin);
        return rt;
    }

    /// <summary>Crée une ligne (point d'état + pseudo) à l'index donné dans le container.</summary>
    void CreateRow(RectTransform container, LobbyPlayerInfo p, int index, bool left, bool connected)
    {
        var rowGo = new GameObject("Row_" + p.pseudo, typeof(RectTransform));
        var rrt = (RectTransform)rowGo.transform;
        rrt.SetParent(container, false);
        rrt.anchorMin = new Vector2(left ? 0f : 1f, 1f);
        rrt.anchorMax = new Vector2(left ? 0f : 1f, 1f);
        rrt.pivot = new Vector2(left ? 0f : 1f, 1f);
        rrt.sizeDelta = new Vector2(container.sizeDelta.x, RowHeight);
        rrt.anchoredPosition = new Vector2(0f, -index * RowHeight);

        // Point d'état (cercle), placé sur le bord extérieur de la ligne.
        var dotGo = new GameObject("Dot", typeof(RectTransform));
        var drt = (RectTransform)dotGo.transform;
        drt.SetParent(rrt, false);
        drt.sizeDelta = new Vector2(DotSize, DotSize);
        drt.anchorMin = drt.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
        drt.pivot = new Vector2(left ? 0f : 1f, 0.5f);
        drt.anchoredPosition = Vector2.zero;
        var dot = dotGo.AddComponent<Image>();
        dot.sprite = GetCircleSprite();
        dot.raycastTarget = false;
        dot.color = connected ? ConnectedColor : DisconnectedColor;

        // Pseudo, à côté du point (à droite pour l'équipe gauche, à gauche pour l'équipe droite).
        var txtGo = new GameObject("Pseudo", typeof(RectTransform));
        var trt = (RectTransform)txtGo.transform;
        trt.SetParent(rrt, false);
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(left ? 0f : 1f, 0.5f);
        float pad = DotSize + DotGap;
        trt.offsetMin = new Vector2(left ? pad : 0f, 0f);
        trt.offsetMax = new Vector2(left ? 0f : -pad, 0f);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = p.pseudo;
        txt.fontSize = 16f;
        txt.color = Color.white;
        txt.raycastTarget = false;
        txt.alignment = left ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;

        rows.Add(new Row { playerId = p.id, connected = connected, dot = dot, go = rowGo });
    }

    void ClearRows()
    {
        foreach (var r in rows)
            if (r.go != null) Destroy(r.go);
        rows.Clear();
    }

    /// <summary>Génère (une seule fois) un sprite de cercle plein blanc, teinté ensuite par ligne.</summary>
    static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float alpha = Mathf.Clamp01(r - d); // anti-aliasing sur ~1px au bord
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }
}
