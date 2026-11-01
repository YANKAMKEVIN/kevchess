using System;

namespace KevChess.Core
{
    /// <summary>Cadence de jeu : temps de base et incrément (Fischer) par coup, en secondes.</summary>
    public readonly struct TimeControl : IEquatable<TimeControl>
    {
        public int BaseSeconds { get; }
        public int IncrementSeconds { get; }
        /// <summary>Libellé court : "3+2", "Sans limite".</summary>
        public string Label { get; }
        /// <summary>Pas d'horloge (temps de base nul).</summary>
        public bool IsUnlimited => BaseSeconds <= 0;

        public TimeControl(int baseSeconds, int incrementSeconds, string label = null)
        {
            BaseSeconds = Math.Max(0, baseSeconds);
            IncrementSeconds = Math.Max(0, incrementSeconds);
            Label = label ?? MakeLabel(BaseSeconds, IncrementSeconds);
        }

        static string MakeLabel(int baseSeconds, int increment)
        {
            if (baseSeconds <= 0) return "Sans limite";
            string minutes = baseSeconds % 60 == 0
                ? (baseSeconds / 60).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : (baseSeconds / 60.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return minutes + "+" + increment;
        }

        public static readonly TimeControl Unlimited = new TimeControl(0, 0);

        /// <summary>Cadences proposées : sans limite, 1+0, 3+2, 5+0, 10+5, 15+10.</summary>
        public static TimeControl[] Presets => new[]
        {
            Unlimited,
            new TimeControl(60, 0),
            new TimeControl(180, 2),
            new TimeControl(300, 0),
            new TimeControl(600, 5),
            new TimeControl(900, 10),
        };

        public bool Equals(TimeControl other) => BaseSeconds == other.BaseSeconds && IncrementSeconds == other.IncrementSeconds;
        public override bool Equals(object obj) => obj is TimeControl t && Equals(t);
        public override int GetHashCode() => BaseSeconds * 397 ^ IncrementSeconds;
        public override string ToString() => Label;
    }

    /// <summary>
    /// Pendule d'échecs pure (pas de temps réel) : le jeu appelle Tick avec le temps écoulé à chaque image.
    /// Sans limite, Remaining renvoie l'infini et rien ne s'écoule.
    /// </summary>
    public sealed class ChessClock
    {
        PieceColor? active;

        public ChessClock(TimeControl control)
        {
            Control = control;
            RemainingWhite = control.BaseSeconds;
            RemainingBlack = control.BaseSeconds;
        }

        public TimeControl Control { get; }
        public bool IsUnlimited => Control.IsUnlimited;

        /// <summary>Temps restant des Blancs (secondes). Settable pour restaurer une sauvegarde.</summary>
        public double RemainingWhite { get; set; }
        /// <summary>Temps restant des Noirs (secondes). Settable pour restaurer une sauvegarde.</summary>
        public double RemainingBlack { get; set; }

        /// <summary>Camp dont la pendule est enclenchée (même en pause), null avant Start.</summary>
        public PieceColor? Turn => active;
        public bool IsPaused { get; private set; }

        /// <summary>Camp dont le temps s'écoule en ce moment : null avant Start, en pause ou après une chute.</summary>
        public PieceColor? Running => IsPaused || IsFlagged(out _) ? null : active;

        public double Remaining(PieceColor color)
        {
            if (IsUnlimited) return double.PositiveInfinity;
            return color == PieceColor.White ? RemainingWhite : RemainingBlack;
        }

        /// <summary>Enclenche la pendule du camp donné (début de partie ou reprise d'une sauvegarde).</summary>
        public void Start(PieceColor color)
        {
            active = color;
            IsPaused = false;
        }

        /// <summary>Décompte le temps écoulé du camp actif. Sans effet en pause, sans limite ou après une chute.</summary>
        public void Tick(double seconds)
        {
            if (IsUnlimited || IsPaused || active == null || seconds <= 0) return;
            if (IsFlagged(out _)) return;
            if (active == PieceColor.White) RemainingWhite = Math.Max(0, RemainingWhite - seconds);
            else RemainingBlack = Math.Max(0, RemainingBlack - seconds);
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        /// <summary>Le camp qui vient de jouer appuie : il reçoit l'incrément et la pendule adverse démarre.</summary>
        public void Press(PieceColor mover)
        {
            if (!IsUnlimited)
            {
                if (IsFlagged(out _)) return;
                if (mover == PieceColor.White) RemainingWhite += Control.IncrementSeconds;
                else RemainingBlack += Control.IncrementSeconds;
            }
            active = Piece.Opposite(mover);
            IsPaused = false;
        }

        /// <summary>Vrai si un camp n'a plus de temps (jamais sans limite).</summary>
        public bool IsFlagged(out PieceColor loser)
        {
            loser = PieceColor.White;
            if (IsUnlimited) return false;
            if (RemainingWhite <= 0) { loser = PieceColor.White; return true; }
            if (RemainingBlack <= 0) { loser = PieceColor.Black; return true; }
            return false;
        }
    }
}
