namespace KevChess.Game.Visuals.Fantasy
{
    /// <summary>Personnages du mode Légendes (une pièce d'échecs incarnée, plus le dragon).</summary>
    public enum CharacterKind
    {
        /// <summary>Pion : petit soldat trapu, casque à crête, épée courte et bouclier rond.</summary>
        Sentinel,
        /// <summary>Cavalier : destrier de cristal et cavalier à la lance.</summary>
        Knight,
        /// <summary>Fou : robe conique, capuche pointue, bâton au cristal flottant.</summary>
        Mage,
        /// <summary>Tour : tour crénelée avec deux bras de pierre et deux yeux luisants.</summary>
        Golem,
        /// <summary>Dame : armure fine, ailes de lumière, épée longue et orbe.</summary>
        Queen,
        /// <summary>Roi : le plus grand, cape, couronne à pointes, grande épée plantée.</summary>
        King,
        /// <summary>Dragon de cristal invoqué (corps serpentin en vol stationnaire).</summary>
        Dragon
    }

    /// <summary>Frappe propre à chaque type de personnage (duels de capture).</summary>
    public enum StrikeKind
    {
        /// <summary>Estoc à l'épée après un pas de charge (Sentinelle).</summary>
        Thrust,
        /// <summary>Charge à la lance, monture cabrée (Chevalier).</summary>
        LanceCharge,
        /// <summary>Éclair arcanique depuis le cristal du bâton (Mage).</summary>
        ArcaneBolt,
        /// <summary>Saut et écrasement des deux poings (Golem).</summary>
        Smash,
        /// <summary>Tourbillon d'épée aérien (Reine).</summary>
        Whirl,
        /// <summary>Coup d'épée à deux mains, lent et lourd (Roi).</summary>
        Cleave,
        /// <summary>Morsure plongeante (Dragon).</summary>
        Bite
    }

    /// <summary>Correspondances et constantes des personnages.</summary>
    public static class CharacterKinds
    {
        /// <summary>Personnage qui incarne une pièce d'échecs classique.</summary>
        public static CharacterKind FromPiece(PieceKind piece)
        {
            switch (piece)
            {
                case PieceKind.Pawn: return CharacterKind.Sentinel;
                case PieceKind.Knight: return CharacterKind.Knight;
                case PieceKind.Bishop: return CharacterKind.Mage;
                case PieceKind.Rook: return CharacterKind.Golem;
                case PieceKind.Queen: return CharacterKind.Queen;
                default: return CharacterKind.King;
            }
        }

        /// <summary>Frappe associée au personnage.</summary>
        public static StrikeKind StrikeOf(CharacterKind kind)
        {
            switch (kind)
            {
                case CharacterKind.Sentinel: return StrikeKind.Thrust;
                case CharacterKind.Knight: return StrikeKind.LanceCharge;
                case CharacterKind.Mage: return StrikeKind.ArcaneBolt;
                case CharacterKind.Golem: return StrikeKind.Smash;
                case CharacterKind.Queen: return StrikeKind.Whirl;
                case CharacterKind.King: return StrikeKind.Cleave;
                default: return StrikeKind.Bite;
            }
        }

        /// <summary>Vrai pour les personnages qui se déplacent en volant (reine, dragon).</summary>
        public static bool Flies(CharacterKind kind) => kind == CharacterKind.Queen || kind == CharacterKind.Dragon;
    }
}
