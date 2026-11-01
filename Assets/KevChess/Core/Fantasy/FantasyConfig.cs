namespace KevChess.Core.Fantasy
{
    /// <summary>
    /// Réglages du mode Légendes (docs/design/mode-fantastique.md, § 6). Tous les nombres sont regroupés
    /// ici pour l'équilibrage par auto-parties.
    /// </summary>
    public sealed class FantasyConfig
    {
        public int MaxEnergy = 10;
        public int StartEnergy = 1;

        public int ShieldCost = 2;
        public int FreezeCost = 3;
        public int TeleportCost = 3;
        public int SummonCost = 10;

        /// <summary>Éclats gagnés en prenant une pièce, indexés par type (0, pion, cavalier, fou, tour, dame, roi, dragon).</summary>
        public int[] CaptureGain = { 0, 1, 2, 2, 2, 3, 0, 4 };
        public int CenterGain = 1;
        /// <summary>Nombre de cases centrales à occuper (pièces hors pions) pour le bonus de centre.</summary>
        public int CenterSquaresNeeded = 2;
        public int CrystalGain = 2;

        public int DragonRange = 3;
        public int BreathRange = 3;
        /// <summary>Après un Souffle, il redevient disponible au N-ième tour suivant.</summary>
        public int BreathCooldown = 3;

        public bool EventsEnabled = true;
        public int FirstEventPly = 10;
        public int EventInterval = 6;
        public int AnnounceDelay = 4;
        public int ChasmDuration = 6;
        public int PortalDuration = 8;
        public int MaxHoles = 2;
        /// <summary>Probabilités relatives : fissure, portails, gisement.</summary>
        public int FissureWeight = 40, PortalWeight = 30, CrystalWeight = 30;

        public static FantasyConfig Default => new FantasyConfig();

        /// <summary>Aucun gain, aucune énergie, aucun événement : le mode se réduit aux échecs classiques.</summary>
        public static FantasyConfig Classic => new FantasyConfig
        {
            StartEnergy = 0,
            CaptureGain = new int[8],
            CenterGain = 0,
            CrystalGain = 0,
            EventsEnabled = false,
        };
    }
}
