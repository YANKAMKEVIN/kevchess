namespace KevChess.Core.Ux
{
    /// <summary>Description courte de chaque niveau d'IA et bilan compact, pour l'écran « Contre l'IA ».</summary>
    public static class LevelBlurb
    {
        static readonly string[] Traits =
        {
            "Fait souvent des erreurs et rate des mats",
            "Joue vite, laisse passer des tactiques",
            "Solide, connaît les ouvertures",
            "Calcule loin et punit les erreurs",
            "Pleine force, sans cadeau",
        };

        /// <summary>« Solide, connaît les ouvertures · réfléchit jusqu'à 0,9 s ».</summary>
        public static string Line(int level, int maxTimeMs)
        {
            if (level < 0) level = 0;
            if (level >= Traits.Length) level = Traits.Length - 1;
            return Traits[level] + " · réfléchit jusqu'à " + Seconds(maxTimeMs);
        }

        /// <summary>« 0,3 s », « 3 s » (virgule à la française).</summary>
        public static string Seconds(int ms)
        {
            int tenths = (ms + 50) / 100;
            return tenths % 10 == 0 ? tenths / 10 + " s" : tenths / 10 + "," + tenths % 10 + " s";
        }

        /// <summary>« 3 V · 1 N · 2 D », ou « aucune partie ».</summary>
        public static string Record(int wins, int draws, int losses) =>
            wins + draws + losses == 0 ? "aucune partie" : wins + " V · " + draws + " N · " + losses + " D";
    }
}
