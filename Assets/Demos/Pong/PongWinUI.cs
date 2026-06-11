using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using MMPong;
using MMPong.Network;

/// <summary>
/// Écran de fin de partie. Fonctionne en mode local (poll PongBall.State) et en multijoueur
/// (écoute GameState via NetworkClient). Crée un overlay animé avec le nom du gagnant,
/// le score final, et un bouton Rejouer.
/// </summary>
public class PongWinUI : MonoBehaviour
{
    [Header("Références scène (assignées dans l'inspecteur)")]
    public GameObject Panel;
    public GameObject PlayerLeft;
    public GameObject PlayerRight;

    [Header("Apparence")]
    [Tooltip("Durée du fondu d'apparition en secondes")]
    public float fadeDuration = 0.5f;

    // ── Refs internes ──
    PongBall ball;
    NetworkClient networkClient;
    CanvasGroup canvasGroup;
    TextMeshProUGUI winnerText;
    TextMeshProUGUI scoreText;

    // ── État ──
    bool isShowing = false;
    float fadeTimer = 0f;
    int cachedWinner = -1;
    int[] cachedScores;

    // ── Multijoueur : dernière phase réseau reçue ──
    bool receivedNetworkState = false;
    GamePhase lastNetworkPhase = GamePhase.WaitingForServe;
    int lastNetworkWinner = -1;
    int[] lastNetworkScores;

    void Start()
    {
        // Masque le panel au démarrage
        Panel.SetActive(false);
        PlayerLeft.SetActive(false);
        PlayerRight.SetActive(false);

        ball = FindFirstObjectByType<PongBall>();

        // Le NetworkClient peut ne pas exister encore (créé dynamiquement par GameBootstrap).
        // On le cherchera dans Update si nécessaire.
        TryBindNetworkClient();

        // Ajoute un CanvasGroup pour le fondu
        canvasGroup = Panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = Panel.AddComponent<CanvasGroup>();

        SetupOverlayUI();
    }

    void OnDestroy()
    {
        if (networkClient != null)
        {
            networkClient.OnStateReceived -= OnNetworkState;
            networkClient.OnGameStarted -= OnGameStarted;
        }
    }

    void TryBindNetworkClient()
    {
        if (networkClient != null) return;
        
        // Recherche tous les NetworkClient et ignore celui qui traîne sur l'objet Bootstrap (mort)
        var clients = FindObjectsByType<NetworkClient>(FindObjectsSortMode.None);
        foreach (var client in clients)
        {
            if (client.gameObject.name.StartsWith("NetworkClient"))
            {
                networkClient = client;
                networkClient.OnStateReceived += OnNetworkState;
                networkClient.OnGameStarted += OnGameStarted;
                break;
            }
        }
    }

    /// <summary>Construit les éléments UI supplémentaires par code (texte gagnant + score).</summary>
    void SetupOverlayUI()
    {
        // Fond semi-transparent sur le Panel (s'il n'a pas déjà une Image)
        Image bg = Panel.GetComponent<Image>();
        if (bg == null)
        {
            bg = Panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
        }
        else
        {
            bg.color = new Color(0f, 0f, 0f, 0.75f);
        }

        // ── Texte principal du gagnant ──
        // Réutilise PlayerLeft/PlayerRight s'ils ont un TMP, sinon crée un nouveau texte
        winnerText = CreateCenteredText(Panel.transform, "WinnerLabel", "", 52, new Vector2(0, 60));
        scoreText = CreateCenteredText(Panel.transform, "ScoreLabel", "", 36, new Vector2(0, -20));

        // ── Bouton Rejouer ──
        // Cherche un bouton existant dans le Panel
        Button existingBtn = Panel.GetComponentInChildren<Button>(true);
        if (existingBtn == null)
        {
            CreateReplayButton(Panel.transform, new Vector2(0, -100));
        }
        else
        {
            // S'assure que le bouton existant a le bon callback
            existingBtn.onClick.RemoveAllListeners();
            existingBtn.onClick.AddListener(OnReplay);
            // Ancre le bouton au centre et le place à Y = -100 pour éviter d'être coupé au bas de l'écran
            RectTransform btnRect = existingBtn.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                btnRect.anchorMin = new Vector2(0.5f, 0.5f);
                btnRect.anchorMax = new Vector2(0.5f, 0.5f);
                btnRect.anchoredPosition = new Vector2(0f, -100f);
            }
        }
    }

    TextMeshProUGUI CreateCenteredText(Transform parent, string name, string content, float fontSize, Vector2 position)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        textObj.layer = parent.gameObject.layer;

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600, 80);
        rect.anchoredPosition = position;

        textObj.AddComponent<CanvasRenderer>();

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontSizeMin = 18;
        tmp.fontSizeMax = fontSize;
        tmp.enableAutoSizing = true;
        tmp.raycastTarget = false; // Empêche le texte de bloquer les clics de souris sur le bouton dessous

        return tmp;
    }

    void CreateReplayButton(Transform parent, Vector2 position)
    {
        GameObject btnObj = new GameObject("ReplayButton");
        btnObj.transform.SetParent(parent, false);
        btnObj.layer = parent.gameObject.layer;

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(200, 50);
        rect.anchoredPosition = position;

        // Fond du bouton
        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.6f, 1f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(OnReplay);

        // Hover / pressed
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.2f, 0.6f, 1f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.7f, 1f, 1f);
        colors.pressedColor = new Color(0.1f, 0.4f, 0.8f, 1f);
        btn.colors = colors;

        // Texte du bouton
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        textObj.layer = btnObj.layer;

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        textObj.AddComponent<CanvasRenderer>();

        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "REJOUER";
        btnText.fontSize = 24;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
    }

    void Update()
    {
        // Cherche le NetworkClient si pas encore trouvé (créé dynamiquement)
        if (networkClient == null)
            TryBindNetworkClient();

        // ── Détection de la fin de partie ──
        if (!isShowing)
        {
            bool triggerWin = false;
            int winner = -1;
            int[] scores = null;

            // Priorité au réseau s'il a reçu le GameOver
            if (receivedNetworkState && lastNetworkPhase == GamePhase.GameOver)
            {
                triggerWin = true;
                winner = lastNetworkWinner;
                scores = lastNetworkScores;
            }
            // Fallback sur l'état local du PongBall (pour l'hôte ou le jeu local pur)
            else if (ball != null && (ball.State == PongBallState.PlayerLeftWin || ball.State == PongBallState.PlayerRightWin))
            {
                triggerWin = true;
                winner = ball.State == PongBallState.PlayerLeftWin ? 0 : 1;
                scores = new[] { ball.scoreLeft, ball.scoreRight };
            }

            if (triggerWin)
            {
                ShowWinScreen(winner, scores);
            }
        }

        // ── Animation de fondu ──
        if (isShowing && canvasGroup.alpha < 1f)
        {
            fadeTimer += Time.deltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeTimer / fadeDuration) : 1f;
            // Ease-out quad
            canvasGroup.alpha = 1f - (1f - t) * (1f - t);

            // Scale bounce léger sur le texte
            float scale = 1f + 0.1f * (1f - t);
            if (winnerText != null)
                winnerText.transform.localScale = Vector3.one * scale;
        }
    }

    void OnNetworkState(GameState s)
    {
        receivedNetworkState = true;
        lastNetworkPhase = s.phase;
        lastNetworkWinner = s.winner;
        if (s.scores != null)
        {
            lastNetworkScores = new int[s.scores.Length];
            for (int i = 0; i < s.scores.Length; i++)
                lastNetworkScores[i] = s.scores[i];
        }
    }

    void OnGameStarted()
    {
        isShowing = false;
        Panel.SetActive(false);
        
        // Remet le bouton dans son état initial si on l'a modifié
        Button existingBtn = Panel.GetComponentInChildren<Button>(true);
        if (existingBtn != null)
        {
            var btnText = existingBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = "REJOUER";
        }
    }

    void ShowWinScreen(int winnerId, int[] scores)
    {
        if (isShowing) return;
        isShowing = true;
        cachedWinner = winnerId;
        cachedScores = scores;

        // Masque les anciens textes statiques
        PlayerLeft.SetActive(false);
        PlayerRight.SetActive(false);

        // Remplis le texte dynamique
        string winnerName = GetWinnerName(winnerId);
        if (winnerText != null)
        {
            winnerText.text = $"Victoire de {winnerName}";
            // Couleur selon le joueur
            winnerText.color = winnerId == 0
                ? new Color(0.2f, 0.7f, 1f) // Bleu eau
                : new Color(1f, 0.4f, 0.1f); // Orange feu
        }

        if (scoreText != null && scores != null && scores.Length >= 2)
        {
            scoreText.text = $"{scores[0]}  -  {scores[1]}";
            scoreText.color = new Color(0.9f, 0.9f, 0.9f);
        }

        // Active le panel avec fondu
        canvasGroup.alpha = 0f;
        fadeTimer = 0f;
        Panel.SetActive(true);
    }

    string GetWinnerName(int winnerId)
    {
        PongPaddle[] paddles = FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);

        // Si c'est un match en équipe (plus de 2 paddles dans la scène)
        if (paddles.Length > 2)
        {
            // Les joueurs impairs (1, 3, 5) appartiennent à la team 0 (Gauche / Bleu)
            // Les joueurs pairs (2, 4, 6) appartiennent à la team 1 (Droit / Rouge)
            return winnerId == 0 ? "l'Équipe Bleue" : "l'Équipe Rouge";
        }

        // Mode 1vs1 : cherche le pseudo du paddle gagnant
        foreach (var p in paddles)
        {
            int playerId = (int)p.Player - 1; // PongPlayer enum commence à 1
            if (playerId == winnerId)
            {
                // Cherche le TextMeshPro enfant "PseudoText"
                TextMeshPro nameText = p.GetComponentInChildren<TextMeshPro>();
                if (nameText != null && !string.IsNullOrWhiteSpace(nameText.text))
                    return nameText.text;
            }
        }

        // Fallback local ou si aucun pseudo n'est défini
        return winnerId == 0 ? "Joueur Gauche" : "Joueur Droit";
    }

    public void OnReplay()
    {
        if (networkClient != null && networkClient.PlayerId >= 0)
        {
            networkClient.SendReady();
            Button existingBtn = Panel.GetComponentInChildren<Button>(true);
            if (existingBtn != null)
            {
                var btnText = existingBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "ATTENTE...";
            }
        }
        else
        {
            Debug.Log("[PongWinUI] OnReplay Clicked! Reloading scene: " + SceneManager.GetActiveScene().name);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
