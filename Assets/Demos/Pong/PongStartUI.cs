using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MMPong.Network;

public class PongStartUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject StartPanel;
    public TMP_InputField UsernameInput;
    public Button StartButton;

    [Header("Boutons de rôle réseau (optionnels)")]
    [Tooltip("Force le mode Host puis démarre. Laisser vide pour n'utiliser que StartButton + le mode du GameBootstrap.")]
    public Button HostButton;
    [Tooltip("Force le mode Client puis démarre.")]
    public Button JoinButton;

    [Header("Game Reference")]
    public GameBootstrap Bootstrap;

    void Start()
    {
        // Show the panel
        StartPanel.SetActive(true);

        // StartButton : démarre avec le mode courant du Bootstrap (inspecteur ou auto-détection MPPM).
        if (StartButton != null) StartButton.onClick.AddListener(() => StartWithMode(null));
        // Boutons de rôle explicites : un seul build sert pour les deux fenêtres de test.
        if (HostButton != null) HostButton.onClick.AddListener(() => StartWithMode(GameMode.Host));
        if (JoinButton != null) JoinButton.onClick.AddListener(() => StartWithMode(GameMode.Client));
    }

    // mode == null → conserve le mode courant du Bootstrap ; sinon, le force avant de démarrer.
    void StartWithMode(GameMode? mode)
    {
        if (string.IsNullOrWhiteSpace(UsernameInput.text))
        {
            Debug.LogWarning("Please enter a username.");
            return; // Don't start if username is empty
        }

        if (Bootstrap == null)
        {
            Debug.LogError("GameBootstrap reference is missing in PongStartUI!");
            return;
        }

        if (mode.HasValue) Bootstrap.mode = mode.Value;

        // Hide the UI
        StartPanel.SetActive(false);

        // Trigger the network/local game start
        Bootstrap.StartGame(UsernameInput.text);
    }
}
