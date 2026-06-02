using UnityEngine;

namespace MMPong
{
    /// <summary>Phase de la partie (découplée de l'enum gameplay PongBallState).</summary>
    public enum GamePhase : byte { WaitingForServe = 0, Playing = 1, GameOver = 2 }

    /// <summary>
    /// État autoritatif du jeu, diffusé par le serveur dans chaque message STATE.
    /// Structure de données uniquement : aucune logique. Représente le jeu circulaire :
    /// chaque paddle est défini par son angle sur le cercle (le client reconstruit
    /// position + rotation depuis le rayon/centre partagés).
    /// </summary>
    public struct GameState
    {
        /// <summary>Numéro de séquence du snapshot (sert à ignorer les paquets périmés).</summary>
        public uint seq;

        /// <summary>Angle (degrés) de chaque paddle sur le cercle ; index = playerId, taille N.</summary>
        public float[] paddleAngle;

        /// <summary>Position de la balle dans le plan de jeu (z = 0).</summary>
        public Vector2 ballPos;

        /// <summary>Dernier joueur à avoir touché la balle (-1 = aucun) ; détermine sa couleur.</summary>
        public int ballOwner;

        /// <summary>Score de chaque joueur ; index = playerId, taille N.</summary>
        public int[] scores;

        /// <summary>Phase courante de la partie.</summary>
        public GamePhase phase;

        /// <summary>playerId du gagnant (-1 = aucun, tant que la partie n'est pas finie).</summary>
        public int winner;
    }
}
