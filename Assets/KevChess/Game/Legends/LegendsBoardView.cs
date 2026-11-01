using System.Collections;
using System.Collections.Generic;
using KevChess.Core;
using KevChess.Core.Fantasy;
using KevChess.Game.View;
using KevChess.Game.Visuals;
using KevChess.Game.Visuals.Fantasy;
using UnityEngine;

namespace KevChess.Game.Legends
{
    /// <summary>Longueur des duels de capture (réglage du joueur).</summary>
    public enum DuelLength { Long = 0, Short = 1, Off = 2 }

    /// <summary>
    /// Plateau du mode Légendes : personnages, duels de capture, pouvoirs, plateau vivant, surbrillances, sélection.
    /// </summary>
    public sealed class LegendsBoardView : MonoBehaviour, ILegendsPresenter, ILegendsBoard
    {
        const string DuelKey = "kevchess.legends.duels";
        const float DragLift = 0.45f;
        static readonly Color BreathColor = new Color(1f, 0.45f, 0.2f);
        static readonly Color GoldColor = new Color(1f, 0.82f, 0.35f);

        readonly FantasyPieceView[] pieces = new FantasyPieceView[64];
        readonly RaycastHit[] hits = new RaycastHit[16];
        BoardHighlights highlights;
        LivingBoard living;
        DuelCamera duelCamera;
        Transform piecesRoot, armedMarker;
        Coroutine hintRoutine;
        int selectedSquare = Square.None;
        bool skipRequested;

        /// <summary>Un duel (ou une animation longue) est en cours : un toucher peut l'accélérer.</summary>
        public bool IsPlayingDuel { get; private set; }

        public static DuelLength Duels
        {
            get => (DuelLength)Mathf.Clamp(PlayerPrefs.GetInt(DuelKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(DuelKey, (int)value); PlayerPrefs.Save(); }
        }

        public void Build(Camera camera, Behaviour orbit)
        {
            BoardBuilder.Build(transform);
            highlights = BoardHighlights.Create(transform);
            piecesRoot = new GameObject("Characters").transform;
            piecesRoot.SetParent(transform, false);
            living = new GameObject("LivingBoard").AddComponent<LivingBoard>();
            living.transform.SetParent(transform, false);
            duelCamera = new DuelCamera(camera, orbit);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(marker.GetComponent<Collider>());
            marker.name = "ArmedPower";
            marker.transform.SetParent(transform, false);
            marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = NeonMaterials.CreateAdditive("KevChess_Armed", NeonTextures.Ring, GoldColor * 2.4f, 23);
            armedMarker = marker.transform;
            marker.SetActive(false);
        }

        /// <summary>Accélère le duel en cours (toucher l'écran).</summary>
        public void SkipDuel()
        {
            if (IsPlayingDuel) skipRequested = true;
        }

        // ---------------------------------------------------------------- présentation

        public void ShowPosition(FantasyGame game, PieceColor viewSide)
        {
            StopHint();
            for (int i = piecesRoot.childCount - 1; i >= 0; i--)
            {
                var child = piecesRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            System.Array.Clear(pieces, 0, 64);
            var pos = game.Position;
            for (int sq = 0; sq < 64; sq++)
                if (pos[sq] != Piece.Empty) pieces[sq] = Spawn(pos[sq], sq);
            highlights.ClearAll();
            armedMarker.gameObject.SetActive(false);
            selectedSquare = Square.None;
            ApplyStatuses(pos);
            living.Sync(pos);
        }

        public void ShowState(FantasyGame game)
        {
            var pos = game.Position;
            Resync(pos);
            ApplyStatuses(pos);
            living.Sync(pos);
            var last = game.LastAction;
            if (last.IsNull) highlights.ClearLastMove();
            else highlights.ShowLastMove(Square.File(last.From), Square.Rank(last.From), Square.File(last.To), Square.Rank(last.To));
            if (game.InCheck)
            {
                int k = pos.KingSquare(game.SideToMove);
                highlights.ShowCheck(Square.File(k), Square.Rank(k));
            }
            else highlights.ClearCheck();
        }

        void ApplyStatuses(FantasyPosition pos)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                var v = pieces[sq];
                if (v == null) continue;
                bool shield = pos.IsShielded(sq), frozen = pos.IsFrozen(sq);
                if (v.IsShielded != shield) v.SetShielded(shield);
                if (v.IsFrozen != frozen) v.SetFrozen(frozen);
            }
        }

        /// <summary>Vérifie que chaque case montre la bonne pièce (filet de sécurité après une animation).</summary>
        void Resync(FantasyPosition pos)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int p = pos[sq];
                var v = pieces[sq];
                if (p == Piece.Empty)
                {
                    if (v != null) { Destroy(v.gameObject); pieces[sq] = null; }
                    continue;
                }
                var kind = KindOf(p);
                bool white = Piece.ColorOf(p) == PieceColor.White;
                if (v == null || v.Kind != kind || v.IsWhite != white)
                {
                    if (v != null) Destroy(v.gameObject);
                    pieces[sq] = Spawn(p, sq);
                }
                else if (!v.IsAnimating && (v.File != Square.File(sq) || v.Rank != Square.Rank(sq))) v.SnapTo(Square.File(sq), Square.Rank(sq));
            }
        }

        // ---------------------------------------------------------------- animation d'une action

        public IEnumerator AnimateAction(ActionVisual v, FantasyGame game)
        {
            StopHint();
            ClearSelection();
            armedMarker.gameObject.SetActive(false);
            var a = v.Action;
            skipRequested = false;

            // 1) Pouvoir.
            switch (a.Power)
            {
                case PowerKind.Shield:
                    if (pieces[a.PowerSquare] != null) { pieces[a.PowerSquare].SetShielded(true); pieces[a.PowerSquare].PulseShield(); }
                    yield return Wait(0.35f);
                    break;
                case PowerKind.Freeze:
                    if (pieces[a.PowerSquare] != null) pieces[a.PowerSquare].SetFrozen(true);
                    yield return Wait(0.45f);
                    break;
                case PowerKind.Summon:
                    if (pieces[a.PowerSquare] != null)
                    {
                        IsPlayingDuel = true;
                        if (Duels == DuelLength.Long) yield return duelCamera.Focus(pieces[a.PowerSquare].transform.position, null, 0.45f);
                        yield return pieces[a.PowerSquare].TransformToDragon();
                        if (Duels == DuelLength.Long) yield return duelCamera.Release(0.4f);
                        IsPlayingDuel = false;
                    }
                    break;
            }

            // 2) Coup.
            var mover = pieces[a.From];
            int final = v.FinalSquare != Square.None ? v.FinalSquare : a.To;
            if (a.Kind == ActionKind.Breath)
            {
                yield return Breath(mover, v);
            }
            else if (a.Kind == ActionKind.Teleport)
            {
                pieces[a.From] = null;
                pieces[a.To] = mover;
                if (mover != null) yield return WaitAnimation(mover, mover.Teleport(Square.File(a.To), Square.Rank(a.To)));
            }
            else
            {
                if (v.IsCapture && mover != null && pieces[v.CaptureSquare] != null)
                    yield return Duel(mover, pieces[v.CaptureSquare], v);
                else if (v.IsCapture && pieces[v.CaptureSquare] != null)
                    pieces[v.CaptureSquare].PlayDeath();
                pieces[v.CaptureSquare >= 0 ? v.CaptureSquare : a.To] = null;

                pieces[a.From] = null;
                pieces[a.To] = mover;
                if (mover != null) yield return WaitAnimation(mover, mover.MoveTo(Square.File(a.To), Square.Rank(a.To)));

                if (a.IsCastle)
                {
                    int rookFrom = Square.File(a.To) == 6 ? a.To + 1 : a.To - 2;
                    int rookTo = Square.File(a.To) == 6 ? a.To - 1 : a.To + 1;
                    var rook = pieces[rookFrom];
                    pieces[rookFrom] = null;
                    pieces[rookTo] = rook;
                    if (rook != null) yield return WaitAnimation(rook, rook.MoveTo(Square.File(rookTo), Square.Rank(rookTo)));
                }

                if (a.IsPromotion && mover != null)
                {
                    FantasyFx.Burst(mover.transform.position + Vector3.up * 0.5f, mover.GlowColor, 40, 2.6f);
                    NeonEffects.Flash(mover.transform.position, mover.GlowColor, 1.4f);
                    Destroy(mover.gameObject);
                    pieces[a.To] = Spawn(Piece.Make(v.MoverIsWhite ? PieceColor.White : PieceColor.Black, a.Promotion), a.To);
                    yield return Wait(0.35f);
                }

                if (v.PortalTo != Square.None)
                {
                    var traveller = pieces[v.PortalFrom];
                    pieces[v.PortalFrom] = null;
                    pieces[v.PortalTo] = traveller;
                    if (traveller != null) yield return WaitAnimation(traveller, traveller.Teleport(Square.File(v.PortalTo), Square.Rank(v.PortalTo)));
                }
            }

            if (v.CrystalSquare != Square.None)
            {
                living.CollectCrystal(v.CrystalSquare);
                yield return Wait(0.3f);
            }

            // 3) Plateau vivant : chutes et effondrements.
            for (int i = 0; i < v.FallenSquares.Length; i++)
            {
                int sq = v.FallenSquares[i];
                var victim = pieces[sq];
                pieces[sq] = null;
                if (victim != null) victim.PlayDeath();
            }
            foreach (int sq in v.CollapsedSquares) living.Collapse(sq);
            if (v.CollapsedSquares.Length > 0 || v.FallenSquares.Length > 0) yield return Wait(0.6f);
            if (!v.SpawnedEvent.IsNone) living.Announce(v.SpawnedEvent);

            Time.timeScale = 1f;
            skipRequested = false;
        }

        IEnumerator Duel(FantasyPieceView attacker, FantasyPieceView victim, ActionVisual v)
        {
            var mode = Duels;
            var target = victim.transform.position;
            if (mode == DuelLength.Off)
            {
                victim.PlayDeath();
                yield break;
            }
            IsPlayingDuel = true;
            if (mode == DuelLength.Long) yield return duelCamera.Focus(attacker.transform.position, target, v.EndsGame ? 0.7f : 0.45f);
            victim.FaceTowards(attacker.transform.position);
            float duration = mode == DuelLength.Long ? (v.EndsGame ? 1.6f : -1f) : 0.55f;
            bool impact = false;
            var shardsTo = attacker.transform.position + Vector3.up * 0.6f;
            yield return WaitAnimation(attacker, attacker.PlayAttack(target, duration, () =>
            {
                impact = true;
                victim.PlayDeath();
                // Les éclats du vaincu rejoignent le vainqueur (lien avec sa jauge d'énergie).
                FantasyFx.Burst(target + Vector3.up * 0.4f, GoldColor, 26, 1.6f);
                FantasyFx.Filaments(shardsTo, 0.6f, GoldColor, true, 22);
            }));
            if (!impact && victim != null) victim.PlayDeath();
            if (mode == DuelLength.Long)
            {
                yield return Wait(v.EndsGame ? 0.6f : 0.15f);
                yield return duelCamera.Release(0.4f);
            }
            IsPlayingDuel = false;
        }

        IEnumerator Breath(FantasyPieceView dragon, ActionVisual v)
        {
            var victim = pieces[v.CaptureSquare];
            pieces[v.CaptureSquare] = null;
            if (dragon == null || victim == null)
            {
                if (victim != null) victim.PlayDeath();
                yield break;
            }
            var mode = Duels;
            IsPlayingDuel = mode != DuelLength.Off;
            if (mode == DuelLength.Long) yield return duelCamera.Focus(dragon.transform.position, victim.transform.position, 0.45f);
            dragon.FaceTowards(victim.transform.position);
            yield return Wait(0.25f);
            float d = mode == DuelLength.Long ? 0.7f : 0.35f;
            FantasyFx.ArcaneBolt(dragon.StrikeOrigin, victim.transform.position + Vector3.up * 0.4f, BreathColor, d);
            FantasyFx.Burst(victim.transform.position + Vector3.up * 0.3f, BreathColor, 30, 2.2f);
            yield return Wait(d * 0.8f);
            victim.PlayDeath();
            yield return Wait(0.35f);
            dragon.FaceDefault();
            if (mode == DuelLength.Long) yield return duelCamera.Release(0.4f);
            IsPlayingDuel = false;
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                if (skipRequested) Time.timeScale = 5f;
                yield return null;
            }
        }

        IEnumerator WaitAnimation(FantasyPieceView view, Coroutine c)
        {
            float started = Time.time;
            while (view != null && view.IsAnimating && Time.time - started < 6f)
            {
                if (skipRequested) Time.timeScale = 5f;
                yield return null;
            }
        }

        // ---------------------------------------------------------------- sélection

        public bool TryPick(Ray ray, out int pieceSquare, out int boardSquare)
        {
            pieceSquare = boardSquare = Square.None;
            int count = Physics.RaycastNonAlloc(ray, hits, 100f);
            float bestPiece = float.MaxValue, bestBoard = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                var view = h.collider.GetComponentInParent<FantasyPieceView>();
                if (view != null)
                {
                    if (h.distance < bestPiece && BoardBuilder.WorldToSquare(view.transform.position, out int f, out int r))
                    {
                        bestPiece = h.distance;
                        pieceSquare = Square.Make(f, r);
                    }
                }
                else if (h.collider.transform.IsChildOf(transform) && h.distance < bestBoard
                         && BoardBuilder.WorldToSquare(h.point, out int bf, out int br))
                {
                    bestBoard = h.distance;
                    boardSquare = Square.Make(bf, br);
                }
            }
            return pieceSquare != Square.None || boardSquare != Square.None;
        }

        public int SquareUnder(Ray ray) => BoardView.SquareUnder(ray);

        public void ShowSelection(int square, IReadOnlyList<FantasyAction> targets)
        {
            StopHint();
            ClearSelection();
            selectedSquare = square;
            if (pieces[square] != null) pieces[square].SetSelected(true);
            highlights.ShowSelection(Square.File(square), Square.Rank(square));
            var quiet = new List<Vector2Int>();
            var captures = new List<Vector2Int>();
            var seen = new HashSet<int>();
            foreach (var a in targets)
            {
                if (!seen.Add(a.To)) continue;
                var cell = new Vector2Int(Square.File(a.To), Square.Rank(a.To));
                if (a.IsCapture) captures.Add(cell); else quiet.Add(cell);
            }
            highlights.ShowLegalMoves(quiet, captures);
        }

        public void ShowPowerTargets(IEnumerable<int> squares, LegendsTool tool)
        {
            ClearSelection();
            var cells = new List<Vector2Int>();
            foreach (int sq in squares) cells.Add(new Vector2Int(Square.File(sq), Square.Rank(sq)));
            if (tool == LegendsTool.Freeze) highlights.ShowLegalMoves(null, cells);
            else highlights.ShowLegalMoves(cells, null);
        }

        public void ShowArmedPower(LegendsTool tool, int square)
        {
            armedMarker.gameObject.SetActive(true);
            armedMarker.position = BoardBuilder.SquareToWorld(Square.File(square), Square.Rank(square)) + Vector3.up * 0.03f;
            armedMarker.localScale = Vector3.one * 1.05f;
            if (tool == LegendsTool.Shield && pieces[square] != null) pieces[square].PulseShield(0.6f);
        }

        public void ClearSelection()
        {
            if (selectedSquare != Square.None && pieces[selectedSquare] != null) pieces[selectedSquare].SetSelected(false);
            selectedSquare = Square.None;
            highlights.ClearSelection();
            highlights.ClearLegalMoves();
            armedMarker.gameObject.SetActive(false);
        }

        public void DragPiece(int square, Ray ray)
        {
            var piece = square >= 0 ? pieces[square] : null;
            if (piece == null || !BoardView.TryBoardPoint(ray, out var p)) return;
            p.x = Mathf.Clamp(p.x, -4.6f, 4.6f);
            p.z = Mathf.Clamp(p.z, -4.6f, 4.6f);
            piece.DragTo(p + Vector3.up * DragLift);
        }

        public void ReturnPiece(int square)
        {
            var piece = square >= 0 ? pieces[square] : null;
            if (piece != null) piece.MoveTo(Square.File(square), Square.Rank(square));
        }

        public void ShowHint(FantasyAction a)
        {
            StopHint();
            hintRoutine = StartCoroutine(HintRoutine(a));
        }

        IEnumerator HintRoutine(FantasyAction a)
        {
            ClearSelection();
            highlights.ShowSelection(Square.File(a.From), Square.Rank(a.From));
            var cell = new[] { new Vector2Int(Square.File(a.To), Square.Rank(a.To)) };
            if (a.IsCapture) highlights.ShowLegalMoves(null, cell); else highlights.ShowLegalMoves(cell, null);
            if (a.HasPower) ShowArmedPower(LegendsTool.None, a.PowerSquare);
            if (pieces[a.From] != null) pieces[a.From].SetSelected(true);
            yield return new WaitForSeconds(2.8f);
            if (pieces[a.From] != null) pieces[a.From].SetSelected(false);
            ClearSelection();
            hintRoutine = null;
        }

        void StopHint()
        {
            if (hintRoutine == null) return;
            StopCoroutine(hintRoutine);
            hintRoutine = null;
            for (int sq = 0; sq < 64; sq++)
                if (pieces[sq] != null && pieces[sq].IsSelected) pieces[sq].SetSelected(false);
            highlights.ClearSelection();
            highlights.ClearLegalMoves();
            armedMarker.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- outils

        FantasyPieceView Spawn(int piece, int square)
        {
            var view = FantasyPieceView.Create(KindOf(piece), Piece.ColorOf(piece) == PieceColor.White,
                Square.File(square), Square.Rank(square), piecesRoot);
            if (!view.TryGetComponent<CapsuleCollider>(out var c)) c = view.gameObject.AddComponent<CapsuleCollider>();
            float h = Mathf.Max(0.6f, view.Height);
            c.height = h;
            c.radius = 0.36f;
            c.center = new Vector3(0f, h * 0.5f, 0f);
            return view;
        }

        public static CharacterKind KindOf(int piece)
        {
            switch (Piece.TypeOf(piece))
            {
                case PieceType.Pawn: return CharacterKind.Sentinel;
                case PieceType.Knight: return CharacterKind.Knight;
                case PieceType.Bishop: return CharacterKind.Mage;
                case PieceType.Rook: return CharacterKind.Golem;
                case PieceType.Queen: return CharacterKind.Queen;
                case PieceType.King: return CharacterKind.King;
                default: return CharacterKind.Dragon;
            }
        }

        void OnDisable() => Time.timeScale = 1f;
    }

    /// <summary>
    /// Caméra des duels : prend la main sur la caméra d'orbite (désactivée le temps du duel), cadre
    /// l'attaquant et sa victime de près, puis revient exactement à la vue de jeu.
    /// </summary>
    public sealed class DuelCamera
    {
        readonly Camera camera;
        readonly Behaviour orbit;
        Vector3 savedPos;
        Quaternion savedRot;
        bool active;

        public DuelCamera(Camera camera, Behaviour orbit)
        {
            this.camera = camera;
            this.orbit = orbit;
        }

        public IEnumerator Focus(Vector3 a, Vector3? b, float seconds)
        {
            var t = camera.transform;
            if (!active)
            {
                savedPos = t.position;
                savedRot = t.rotation;
                active = true;
            }
            if (orbit != null) orbit.enabled = false;

            Vector3 center = b.HasValue ? (a + b.Value) * 0.5f : a;
            Vector3 axis = b.HasValue ? (b.Value - a) : Vector3.forward;
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-3f) axis = Vector3.forward;
            // On regarde le duel de côté, du côté où se trouvait déjà la caméra.
            Vector3 side = Vector3.Cross(Vector3.up, axis.normalized);
            Vector3 fromCam = savedPos - center;
            fromCam.y = 0f;
            if (Vector3.Dot(side, fromCam) < 0f) side = -side;
            bool portrait = Screen.height > Screen.width;
            float dist = portrait ? 5.2f : 4.0f;
            Vector3 targetPos = center + side * dist + (savedPos - center).normalized * 0.8f + Vector3.up * 2.4f;
            Quaternion targetRot = Quaternion.LookRotation(center + Vector3.up * 0.55f - targetPos);

            Vector3 startPos = t.position;
            Quaternion startRot = t.rotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                float e = k * k * (3f - 2f * k);
                t.position = Vector3.Lerp(startPos, targetPos, e);
                t.rotation = Quaternion.Slerp(startRot, targetRot, e);
                yield return null;
            }
            t.position = targetPos;
            t.rotation = targetRot;
        }

        public IEnumerator Release(float seconds)
        {
            if (!active) yield break;
            var t = camera.transform;
            Vector3 startPos = t.position;
            Quaternion startRot = t.rotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                float e = k * k * (3f - 2f * k);
                t.position = Vector3.Lerp(startPos, savedPos, e);
                t.rotation = Quaternion.Slerp(startRot, savedRot, e);
                yield return null;
            }
            t.position = savedPos;
            t.rotation = savedRot;
            active = false;
            if (orbit != null) orbit.enabled = true;
        }
    }
}
