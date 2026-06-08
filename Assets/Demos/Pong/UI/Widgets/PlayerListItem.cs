using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MMPong.UI
{
    /// <summary>
    /// Ligne d'affichage d'un joueur dans une liste de lobby (host ou salle d'attente client).
    /// Construite par code via <see cref="Create"/> puis mise à jour via <see cref="Bind"/>.
    /// </summary>
    public class PlayerListItem : MonoBehaviour
    {
        private TextMeshProUGUI label;

        /// <summary>Crée une ligne vide sous <paramref name="parent"/> (à remplir via <see cref="Bind"/>).</summary>
        public static PlayerListItem Create(Transform parent)
        {
            var go = new GameObject("PlayerListItem", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            var item = go.AddComponent<PlayerListItem>();
            item.label = go.AddComponent<TextMeshProUGUI>();
            item.label.fontSize = 24f;
            item.label.alignment = TextAlignmentOptions.Left;
            item.label.color = Color.white;
            item.label.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 520f;
            le.preferredHeight = 36f;
            return item;
        }

        /// <summary>Met à jour la ligne avec l'état d'un joueur.</summary>
        public void Bind(PlayerInfo player)
        {
            string team = player.TeamIndex == 0 ? "Équipe A" : "Équipe B";
            string ready = player.IsReady ? "<color=#6CD66C>Prêt</color>" : "<color=#C8C864>En attente</color>";
            label.text = $"{player.Pseudo}  —  {team}  —  {ready}";
        }
    }
}
