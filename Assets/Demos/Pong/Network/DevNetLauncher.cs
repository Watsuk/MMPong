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

            // L'IMGUI par défaut est minuscule en haute résolution : on force des tailles lisibles.
            var title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            var label = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            var field = new GUIStyle(GUI.skin.textField) { fontSize = 24 };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 24 };

            const float pad = 20f;
            GUILayout.BeginArea(new Rect(pad, pad, 440, 300), GUI.skin.box);

            GUILayout.Label("Réseau (test jetable)", title);
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Label("IP", label, GUILayout.Width(40));
            ip = GUILayout.TextField(ip, field, GUILayout.Height(42));
            GUILayout.EndHorizontal();

            GUILayout.Space(16);
            if (GUILayout.Button("Héberger", button, GUILayout.Height(60))) { bootstrap.StartHost(); started = true; }
            GUILayout.Space(10);
            if (GUILayout.Button("Rejoindre", button, GUILayout.Height(60))) { bootstrap.StartClient(ip); started = true; }

            GUILayout.EndArea();
        }
    }
}
