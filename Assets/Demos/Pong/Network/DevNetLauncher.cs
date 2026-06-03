using UnityEngine;

namespace MMPong.Network
{
    /// <summary>
    /// Lanceur <b>jetable</b> (IMGUI, zéro UI à câbler) pour tester le réseau sans menu :
    /// champ IP + boutons « Héberger » / « Rejoindre ». À SUPPRIMER quand le vrai menu
    /// (coéquipier) appellera <see cref="GameBootstrap.StartHost"/> / <see cref="GameBootstrap.StartClient"/>.
    /// </summary>
    [RequireComponent(typeof(GameBootstrap))]
    public class DevNetLauncher : MonoBehaviour
    {
        GameBootstrap bootstrap;
        string ip = "127.0.0.1";
        bool started;

        void Awake() => bootstrap = GetComponent<GameBootstrap>();

        void OnGUI()
        {
            if (started) return;

            GUILayout.BeginArea(new Rect(10, 10, 240, 120), GUI.skin.box);
            GUILayout.Label("Réseau (test jetable)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP", GUILayout.Width(20));
            ip = GUILayout.TextField(ip);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Héberger")) { bootstrap.StartHost(); started = true; }
            if (GUILayout.Button("Rejoindre")) { bootstrap.StartClient(ip); started = true; }
            GUILayout.EndArea();
        }
    }
}
