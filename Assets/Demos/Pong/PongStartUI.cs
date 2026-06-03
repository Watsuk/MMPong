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

    [Header("Game Reference")]
    public GameBootstrap Bootstrap;

    void Start()
    {
        // Show the panel
        StartPanel.SetActive(true);

        // Hook up the button
        StartButton.onClick.AddListener(OnStartClicked);
    }

    void OnStartClicked()
    {
        if (string.IsNullOrWhiteSpace(UsernameInput.text))
        {
            Debug.LogWarning("Please enter a username.");
            return; // Don't start if username is empty
        }

        // Hide the UI
        StartPanel.SetActive(false);

        // Trigger the network/local game start
        if (Bootstrap != null) {
            Bootstrap.StartGame(UsernameInput.text);
        } else {
            Debug.LogError("GameBootstrap reference is missing in PongStartUI!");
        }
    }
}
