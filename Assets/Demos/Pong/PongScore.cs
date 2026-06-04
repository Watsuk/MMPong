using UnityEngine;
using TMPro;

public class PongScore : MonoBehaviour
{
    public TextMeshProUGUI blueTextScore;
    public TextMeshProUGUI redTextScore;

    private int blueScore = 0;
    private int redScore = 0;

    void Start()
    {
        RefreshScoreDisplay();
    }

    public void BlueScorePoint()
    {
        blueScore++;
        RefreshScoreDisplay();
    }

    public void RedScorePoint()
    {
        redScore++;
        RefreshScoreDisplay();
    }

    void RefreshScoreDisplay()
    {
        blueTextScore.text = blueScore.ToString();
        redTextScore.text = redScore.ToString();
    }
}