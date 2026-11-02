using System;
using System.Collections;
using System.IO;
using System.Linq;
using KevChess.Core;
using KevChess.Core.Fantasy;
using KevChess.Game.Legends;
using KevChess.Game.Match;
using UnityEngine;

namespace KevChess.Game.App
{
    /// <summary>
    /// Recette automatique du mode Légendes, lancée avec « -kevchess-legends-autotest [dossier] » :
    /// personnages, duel, invocation, pouvoirs, plateau vivant, partie contre l'IA ; captures et journal.
    /// </summary>
    public sealed class LegendsAutoTest : MonoBehaviour
    {
        const string Arg = "-kevchess-legends-autotest";

        LegendsMode legends;
        LegendsController match;
        string folder;
        int shot, failures;

        public static bool Requested(out string folder)
        {
            folder = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != Arg) continue;
                folder = i + 1 < args.Length && !args[i + 1].StartsWith("-")
                    ? args[i + 1]
                    : Path.Combine(Application.persistentDataPath, "legends-autotest");
                return true;
            }
            return false;
        }

        public void Run(LegendsMode mode, Action enter, string outputFolder)
        {
            legends = mode;
            folder = outputFolder;
            Directory.CreateDirectory(folder);
            Application.runInBackground = true;
            StartCoroutine(Script(enter));
        }

        IEnumerator Script(Action enter)
        {
            Log("début, dossier " + folder);
            yield return new WaitForSeconds(2f);
            enter();
            match = legends.Controller;
            yield return new WaitForSeconds(1f);
            yield return Capture("reglages");
            legends.Hud.ShowSetup(false);

            match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
            yield return new WaitForSeconds(1.5f);
            yield return Capture("depart");

            // Ouverture puis prise : duel de capture.
            yield return Play(a => Is(a, "e2", "e4"), "e4");
            yield return Play(a => Is(a, "d7", "d5"), "d5");
            yield return Play(a => Is(a, "e4", "d5"), "exd5", 1.0f, "duel_pion");

            // Invocation du dragon (jauge pleine pour la recette).
            match.Game.Position.SetEnergy(PieceColor.Black, 10);
            match.RefreshAfterSetup();
            yield return Play(a => a.Power == PowerKind.Summon && a.PowerSquare == Sq("h8") && a.From == Sq("h8"), "invocation", 1.1f, "invocation");
            Check(match.Game.Position[Sq("h8")] == Piece.Empty || Enumerable.Range(0, 64).Any(sq => match.Game.Position[sq] == Piece.Make(PieceColor.Black, FantasyPiece.Dragon)), "dragon noir sur le plateau");
            yield return Capture("dragon");

            // Bouclier blanc puis Gel noir.
            match.Game.Position.SetEnergy(PieceColor.White, 6);
            match.RefreshAfterSetup();
            // Parcours de l'interface : outil Bouclier, cible, confirmation du coût.
            legends.Interaction.ToggleTool(LegendsTool.Shield);
            yield return new WaitForSeconds(0.4f);
            yield return Capture("bouclier_cible");
            Check(legends.Interaction.TargetPower(Sq("d5")), "cible du Bouclier acceptée");
            yield return new WaitForSeconds(0.4f);
            yield return Capture("bouclier_confirmation");
            legends.Interaction.ConfirmPower();
            Check(legends.Interaction.ArmedSquare == Sq("d5"), "Bouclier armé après confirmation");
            yield return new WaitForSeconds(0.3f);
            yield return Play(a => a.Power == PowerKind.Shield && a.PowerSquare == Sq("d5") && a.From == Sq("g1") && a.To == Sq("f3"), "bouclier d5 + Cf3");
            yield return Capture("bouclier");
            match.Game.Position.SetEnergy(PieceColor.Black, 6);
            match.RefreshAfterSetup();
            yield return Play(a => a.Power == PowerKind.Freeze && a.PowerSquare == Sq("f3") && a.Kind == ActionKind.Normal && !a.IsCapture, "gel f3");
            yield return Capture("gel");

            // Téléportation blanche.
            yield return Play(a => a.Kind == ActionKind.Teleport, "téléportation", 0.5f, "teleportation");

            // Souffle du dragon s'il a une cible.
            if (match.Game.LegalActions.Any(a => a.Kind == ActionKind.Breath))
                yield return Play(a => a.Kind == ActionKind.Breath, "souffle", 0.9f, "souffle");
            else
            {
                var dragonMove = match.Game.LegalActions.FirstOrDefault(a => !a.HasPower && Piece.TypeOf(match.Game.Position[a.From]) == FantasyPiece.Dragon);
                if (!dragonMove.IsNull) yield return Play(a => a == dragonMove, "vol du dragon");
            }

            // Plateau vivant.
            var pos = match.Game.Position;
            int free = Enumerable.Range(16, 32).First(sq => pos[sq] == Piece.Empty && !pos.IsFeatureSquare(sq));
            pos.AddFissure(free, 2);
            int chasm = Enumerable.Range(16, 32).First(sq => pos[sq] == Piece.Empty && !pos.IsFeatureSquare(sq));
            pos.AddChasm(chasm, 5);
            int portal = Enumerable.Range(16, 16).First(sq => pos[sq] == Piece.Empty && pos[63 - sq] == Piece.Empty && !pos.IsFeatureSquare(sq) && !pos.IsFeatureSquare(63 - sq));
            pos.SetPortals(portal, 63 - portal, PortalState.Open, 6);
            int crystal = Enumerable.Range(16, 32).First(sq => pos[sq] == Piece.Empty && !pos.IsFeatureSquare(sq));
            pos.SetCrystal(crystal);
            match.RefreshAfterSetup();
            yield return new WaitForSeconds(1.2f);
            yield return Capture("plateau_vivant");

            // Contre l'IA (noirs pour l'humain : l'IA ouvre).
            match.StartNew(new MatchSettings { Mode = MatchMode.VersusAI, HumanColor = PieceColor.Black, Level = AILevel.Medium });
            float t = 0f;
            while (match.Game.Actions.Count < 1 && t < 15f) { t += Time.deltaTime; yield return null; }
            Check(match.Game.Actions.Count == 1, "l'IA Légendes a joué (" + t.ToString("0.0") + " s)");
            yield return WaitIdle();
            for (int i = 0; i < 3 && !match.Game.IsOver; i++)
            {
                var reply = match.Game.LegalActions.First(a => !a.HasPower && a.Kind == ActionKind.Normal);
                match.TryHumanAction(reply);
                int expected = match.Game.Actions.Count + 2;
                t = 0f;
                while (match.Game.Actions.Count < expected && t < 15f && !match.Game.IsOver) { t += Time.deltaTime; yield return null; }
                yield return WaitIdle();
            }
            Check(match.Game.Actions.Count >= 7, "partie contre l'IA : " + match.Game.Actions.Count + " demi-coups");
            yield return Capture("contre_ia");

            Log(failures == 0 ? "TERMINÉ : OK" : "TERMINÉ : " + failures + " échec(s)");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(failures == 0 ? 0 : 1);
        }

        static int Sq(string name) => Square.Parse(name);
        static bool Is(FantasyAction a, string from, string to) => !a.HasPower && a.Kind == ActionKind.Normal && a.From == Sq(from) && a.To == Sq(to);

        IEnumerator Play(Func<FantasyAction, bool> pick, string label, float midShot = -1f, string shotName = null)
        {
            var action = match.Game.LegalActions.FirstOrDefault(pick);
            if (action.IsNull || !match.TryHumanAction(action))
            {
                Fail("action introuvable ou refusée : " + label + " (" + match.Game.Position.ToFen() + ")");
                yield break;
            }
            Log("joué " + label + " : " + match.Game.Notations[match.Game.Notations.Count - 1]);
            if (midShot > 0f)
            {
                yield return new WaitForSeconds(midShot);
                yield return Capture(shotName);
            }
            yield return WaitIdle();
        }

        IEnumerator WaitIdle()
        {
            float t = 0f;
            yield return null;
            while ((match.IsAnimating || match.IsAIThinking) && t < 15f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.2f);
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, $"{++shot:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log("capture " + path);
            yield return new WaitForSeconds(0.3f);
        }

        void Check(bool ok, string what)
        {
            if (ok) Log("OK " + what);
            else Fail(what);
        }

        void Fail(string what)
        {
            failures++;
            Debug.LogError("[LegendsAutoTest] ÉCHEC " + what);
        }

        static void Log(string msg) => Debug.Log("[LegendsAutoTest] " + msg);
    }
}
