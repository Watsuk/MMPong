using UnityEngine;
using TMPro; // /!\ TRÈS IMPORTANT pour pouvoir utiliser TextMeshPro

public class PongScore : MonoBehaviour
{
    // On expose les variables pour glisser-déposer nos textes dans l'éditeur
    public TextMeshProUGUI leftTextScore;
    public TextMeshProUGUI rightTextScore;

    private int leftScore = 0;
    private int rightScore = 0;

    void Start()
    {
        // On initialise l'affichage au début de la partie
        ActualiserAffichage();
    }

    public void MarquerPointGauche()
    {
        leftScore++;
        ActualiserAffichage();
    }

    public void MarquerPointDroit()
    {
        rightScore++;
        ActualiserAffichage();
    }

    void ActualiserAffichage()
    {
        leftTextScore.text = leftScore.ToString();
        rightTextScore.text = rightScore.ToString();
    }
}