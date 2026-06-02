using UnityEngine;

namespace MMPong
{
    /// <summary>
    /// État autoritatif du jeu, diffusé par le serveur dans chaque message STATE.
    /// Structure de données uniquement : aucune logique.
    /// </summary>
    public struct GameState
    {
        /// <summary>Numéro de séquence du snapshot (sert à ignorer les paquets périmés).</summary>
        public uint seq;

        /// <summary>Position de la balle.</summary>
        public Vector2 ballPos;

        /// <summary>Position verticale de chaque paddle (4 joueurs).</summary>
        public float[] paddleY;

        /// <summary>Score de chaque joueur (4 joueurs).</summary>
        public int[] scores;
    }
}
