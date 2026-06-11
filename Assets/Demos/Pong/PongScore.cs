using UnityEngine;
using TMPro; // /!\ TRÈS IMPORTANT pour pouvoir utiliser TextMeshPro

public class PongScore : MonoBehaviour
{
    // On expose les variables pour glisser-déposer nos textes dans l'éditeur
    public TextMeshProUGUI leftTextScore;
    public TextMeshProUGUI rightTextScore;

    [Header("HUD équipes (coins haut)")]
    public TextMeshProUGUI leftTeamLabel;
    public TextMeshProUGUI rightTeamLabel;
    public string leftTeamName = "Équipe Bleue";
    public string rightTeamName = "Équipe Rouge";

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

    /// <summary>Applique directement les scores reçus du serveur (côté client réseau).</summary>
    public void SetScores(int left, int right)
    {
        leftScore = left;
        rightScore = right;
        ActualiserAffichage();
    }

    /// <summary>
    /// Applique les noms d'équipe définis par le host et propagés par le serveur
    /// (message CONFIG → <c>MatchSettings.teamAName/teamBName</c>). Équipe A = gauche,
    /// équipe B = droite (cohérent avec <see cref="SetScores"/> : scores[0]=gauche).
    /// Les valeurs vides sont ignorées pour conserver le faux nom de repli.
    /// </summary>
    public void SetTeamNames(string left, string right)
    {
        if (!string.IsNullOrWhiteSpace(left)) leftTeamName = left;
        if (!string.IsNullOrWhiteSpace(right)) rightTeamName = right;
        ActualiserAffichage();
    }

    void ActualiserAffichage()
    {
        if (leftTextScore != null) leftTextScore.text = leftScore.ToString();
        if (rightTextScore != null) rightTextScore.text = rightScore.ToString();

        // HUD équipes (coins haut) : nom d'équipe (faux nom pour l'instant) + score
        if (leftTeamLabel != null) leftTeamLabel.text = $"{leftTeamName}\n{leftScore}";
        if (rightTeamLabel != null) rightTeamLabel.text = $"{rightTeamName}\n{rightScore}";
    }
}