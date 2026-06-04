using UnityEngine;
using UnityEngine.SceneManagement;

public class PongWinUI : MonoBehaviour
{
    public GameObject Panel;
    public GameObject PlayerBlue;
    public GameObject PlayerRed;

    PongBall Ball;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Panel.SetActive(false);
        PlayerBlue.SetActive(false);
        PlayerRed.SetActive(false);
        Ball = GameObject.FindFirstObjectByType<PongBall>();
    }

    // Update is called once per frame
    void Update()
    {
        switch (Ball.State) {
          case PongBallState.WaitingForServe:
          case PongBallState.Playing:
            Panel.SetActive(false);
            PlayerBlue.SetActive(false);
            PlayerRed.SetActive(false);
            break;
          case PongBallState.PlayerBlueWin:
            Panel.SetActive(true);
            PlayerBlue.SetActive(true);
            break;
          case PongBallState.PlayerRedWin:
            Panel.SetActive(true);
            PlayerRed.SetActive(true);
            break;
        }
       
    }

    public void OnReplay() {
      SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
