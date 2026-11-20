namespace KevChess.Core.Ux
{
    /// <summary>Gestes du plateau enseignés à la première partie.</summary>
    public enum BoardGesture { MovePlayed, CameraOrbited, CameraRecentered }

    /// <summary>
    /// Bulles d'accueil : trois gestes, dans l'ordre (jouer un coup, tourner la vue, recentrer).
    /// Chaque bulle se ferme dès que son geste est fait ; l'étape atteinte est enregistrée par l'appelant.
    /// </summary>
    public sealed class BoardTips
    {
        /// <summary>Glissement cumulé (pixels) qui compte comme « tourner la vue » : un frôlement ne suffit pas.</summary>
        public const float OrbitPixels = 160f;

        static readonly BoardGesture[] Order = { BoardGesture.MovePlayed, BoardGesture.CameraOrbited, BoardGesture.CameraRecentered };

        static readonly string[] Texts =
        {
            "Touchez une pièce puis une case, ou faites-la glisser, pour jouer un coup.",
            "Glissez un doigt hors des pièces pour faire tourner la vue ; pincez pour zoomer.",
            "Touchez deux fois hors du plateau pour recentrer la vue.",
        };

        float orbitAccumulated;

        public BoardTips(int step = 0)
        {
            Step = step < 0 ? 0 : step > Count ? Count : step;
        }

        public static int Count => Order.Length;

        /// <summary>Étape en cours (Count = terminé).</summary>
        public int Step { get; private set; }

        public bool Active => Step < Count;

        /// <summary>Texte de la bulle en cours, null une fois terminé.</summary>
        public string Text => Active ? Texts[Step] : null;

        /// <summary>« 1 / 3 » pour la bulle en cours.</summary>
        public string Counter => Active ? (Step + 1) + " / " + Count : "";

        public BoardGesture? Expected => Active ? Order[Step] : (BoardGesture?)null;

        /// <summary>Signale un geste ; vrai si la bulle en cours se ferme.</summary>
        public bool OnGesture(BoardGesture gesture)
        {
            if (!Active || Order[Step] != gesture) return false;
            Step++;
            orbitAccumulated = 0f;
            return true;
        }

        /// <summary>Glissement de caméra (en pixels) ; la bulle « tourner la vue » se ferme au-delà de OrbitPixels cumulés.</summary>
        public bool OnOrbit(float pixels)
        {
            if (Expected != BoardGesture.CameraOrbited) return false;
            orbitAccumulated += pixels < 0 ? -pixels : pixels;
            return orbitAccumulated >= OrbitPixels && OnGesture(BoardGesture.CameraOrbited);
        }

        /// <summary>Ferme toutes les bulles.</summary>
        public void Skip()
        {
            Step = Count;
            orbitAccumulated = 0f;
        }

        /// <summary>Recommence depuis la première bulle (bouton des Réglages).</summary>
        public void Restart()
        {
            Step = 0;
            orbitAccumulated = 0f;
        }
    }
}
