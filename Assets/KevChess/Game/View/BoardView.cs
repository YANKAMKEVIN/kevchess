using System.Collections;
using System.Collections.Generic;
using KevChess.Core;
using KevChess.Game.Match;
using KevChess.Game.Visuals;
using UnityEngine;

namespace KevChess.Game.View
{
    /// <summary>
    /// Plateau 3D synchronisé avec la partie : pièces, animations de coups, surbrillances,
    /// flèche d'indice, animation de mat et sélection au rayon.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardPresenter
    {
        const float AnimationTimeout = 8f;
        const float HintSeconds = 2.8f;

        /// <summary>
        /// Vitesse des animations de coup (déplacement, prise, roque, promotion) :
        /// 0,5 = lent, 1 = normal, 2 = rapide. Bornée entre 0,25 et 4.
        /// </summary>
        public static float AnimationSpeed = 1f;

        static float Speed => Mathf.Clamp(AnimationSpeed, 0.25f, 4f);

        HintArrow hintArrow;
        int arrowFrom = Square.None, arrowTo = Square.None;

        readonly PieceView[] pieces = new PieceView[64];
        BoardHighlights highlights;
        Transform piecesRoot;
        Coroutine hintRoutine;
        int selectedSquare = Square.None;

        public void Build()
        {
            BoardBuilder.Build(transform);
            highlights = BoardHighlights.Create(transform);
            piecesRoot = new GameObject("Pieces").transform;
            piecesRoot.SetParent(transform, false);
            trayRoot = new GameObject("CapturedTrays").transform;
            trayRoot.SetParent(transform, false);
            hintArrow = HintArrow.Create(transform);
            VisualTheme.Changed += OnThemeChanged;
        }

        void OnDestroy()
        {
            VisualTheme.Changed -= OnThemeChanged;
        }

        void OnThemeChanged()
        {
            if (hintArrow != null && hintArrow.IsVisible) hintArrow.SetColor(ArrowColor(arrowFrom));
        }

        // ---------------------------------------------------------------- IBoardPresenter

        public void ShowPosition(Position position, PieceColor viewSide)
        {
            StopHint();
            ClearHintArrow();
            // Tous les enfants, y compris une victime retirée du tableau dont l'animation a été interrompue.
            for (int i = piecesRoot.childCount - 1; i >= 0; i--)
            {
                var child = piecesRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            System.Array.Clear(pieces, 0, 64);
            for (int sq = 0; sq < 64; sq++)
            {
                int p = position[sq];
                if (p != Piece.Empty) pieces[sq] = Spawn(p, sq);
            }
            highlights.ClearAll();
            RefreshTrays(position);
            selectedSquare = Square.None;
        }

        public IEnumerator AnimateMove(MoveVisual v)
        {
            StopHint();
            ClearHintArrow();
            float speed = Speed;
            ClearSelection();
            highlights.ClearCheck();

            var move = v.Move;
            var mover = pieces[move.From];
            PieceView victim = v.IsCapture ? pieces[v.CaptureSquare] : null;
            pieces[move.From] = null;
            if (v.IsCapture) pieces[v.CaptureSquare] = null;

            if (mover == null)
            {
                // Vue désynchronisée : on repart de l'état exact plutôt que d'animer faux.
                Debug.LogWarning("BoardView : pièce absente en " + Square.Name(move.From));
                yield break;
            }
            pieces[move.To] = mover;

            highlights.ShowLastMove(Square.File(move.From), Square.Rank(move.From), Square.File(move.To), Square.Rank(move.To));

            bool landed = false;
            mover.MoveTo(Square.File(move.To), Square.Rank(move.To), () =>
            {
                landed = true;
                if (victim != null) victim.Capture(null, speed);
            }, speed);

            PieceView rook = null;
            if (v.IsCastle)
            {
                rook = pieces[v.RookFrom];
                pieces[v.RookFrom] = null;
                pieces[v.RookTo] = rook;
                yield return new WaitForSeconds(0.12f / speed);
                if (rook != null) rook.MoveTo(Square.File(v.RookTo), Square.Rank(v.RookTo), null, speed);
            }

            float t = 0f;
            while ((!landed || mover.IsAnimating || (rook != null && rook.IsAnimating)) && t < AnimationTimeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (!landed && victim != null) victim.Capture(null, speed);

            if (v.IsPromotion)
            {
                bool done = false;
                mover.Promote(ToKind(Piece.TypeOf(v.PromotedPiece)), () => done = true, speed);
                t = 0f;
                while (!done && t < AnimationTimeout) { t += Time.deltaTime; yield return null; }
                FitCollider(mover, ToKind(Piece.TypeOf(v.PromotedPiece)));
            }

            // Filet de sécurité : position finale exacte.
            if (mover != null) mover.SnapTo(Square.File(move.To), Square.Rank(move.To));
            if (rook != null) rook.SnapTo(Square.File(v.RookTo), Square.Rank(v.RookTo));
        }

        public void ShowState(ChessGame game)
        {
            SetTrayBaseline(game.Moves.Count == 0 ? game.Position : game.PositionAt(0));
            RefreshTrays(game.Position);
            var last = game.LastMove;
            if (last.IsNull) highlights.ClearLastMove();
            else highlights.ShowLastMove(Square.File(last.From), Square.Rank(last.From), Square.File(last.To), Square.Rank(last.To));

            if (game.InCheck)
            {
                int k = game.Position.KingSquare(game.SideToMove);
                highlights.ShowCheck(Square.File(k), Square.Rank(k));
            }
            else highlights.ClearCheck();
        }

        // ---------------------------------------------------------------- Sélection

        public void ShowSelection(int square, IReadOnlyList<Move> targets)
        {
            StopHint();
            ClearSelection();
            selectedSquare = square;
            if (pieces[square] != null) pieces[square].SetSelected(true);
            highlights.ShowSelection(Square.File(square), Square.Rank(square));

            var quiet = new List<Vector2Int>();
            var captures = new List<Vector2Int>();
            var seen = new HashSet<int>();
            foreach (var m in targets)
            {
                if (!seen.Add(m.To)) continue; // promotions : 4 coups vers la même case
                var cell = new Vector2Int(Square.File(m.To), Square.Rank(m.To));
                if (m.IsCapture) captures.Add(cell);
                else quiet.Add(cell);
            }
            highlights.ShowLegalMoves(quiet, captures);
        }

        /// <summary>Coup refusé : fait clignoter les cases en cause (roi menacé, pièces qui l'attaquent).</summary>
        public void FlashSquares(IEnumerable<int> squares)
        {
            var cells = new List<Vector2Int>();
            foreach (int sq in squares)
                if (sq != Square.None) cells.Add(new Vector2Int(Square.File(sq), Square.Rank(sq)));
            if (cells.Count > 0) highlights.Flash(cells);
        }

        /// <summary>Promotion en attente : garde la pièce et la case d'arrivée marquées.</summary>
        public void ShowPendingMove(int from, int to)
        {
            highlights.ShowSelection(Square.File(from), Square.Rank(from));
            highlights.ShowLegalMoves(new[] { new Vector2Int(Square.File(to), Square.Rank(to)) }, new Vector2Int[0]);
        }

        public void ClearSelection()
        {
            if (selectedSquare != Square.None && pieces[selectedSquare] != null) pieces[selectedSquare].SetSelected(false);
            selectedSquare = Square.None;
            highlights.ClearSelection();
            highlights.ClearLegalMoves();
        }

        /// <summary>Indice : surligne le coup quelques secondes et pose la flèche (qui reste jusqu'au prochain coup).</summary>
        public void ShowHint(Move move)
        {
            StopHint();
            hintRoutine = StartCoroutine(HintRoutine(move));
            ShowHintArrow(move.From, move.To);
        }

        // ---------------------------------------------------------------- Flèche d'indice

        /// <summary>
        /// Flèche néon en arc de <paramref name="from"/> vers <paramref name="to"/> (cases 0–63).
        /// Elle reste affichée jusqu'à <see cref="ClearHintArrow"/>, un coup animé ou <see cref="ShowPosition"/>.
        /// </summary>
        public void ShowHintArrow(int from, int to)
        {
            if (hintArrow == null || from < 0 || from > 63 || to < 0 || to > 63 || from == to) return;
            arrowFrom = from;
            arrowTo = to;
            hintArrow.Show(BoardBuilder.SquareToWorld(Square.File(from), Square.Rank(from)),
                BoardBuilder.SquareToWorld(Square.File(to), Square.Rank(to)), ArrowColor(from));
        }

        /// <summary>Retire la flèche d'indice (sans effet si elle n'est pas affichée).</summary>
        public void ClearHintArrow()
        {
            arrowFrom = arrowTo = Square.None;
            if (hintArrow != null) hintArrow.Hide();
        }

        /// <summary>Couleur de la flèche : surbrillance du thème teintée par la lueur de la pièce jouée.</summary>
        Color ArrowColor(int from)
        {
            var piece = from >= 0 && from < 64 ? pieces[from] : null;
            return piece != null ? Color.Lerp(NeonPalette.Selection, piece.GlowColor, 0.5f) : NeonPalette.Selection;
        }

        // ---------------------------------------------------------------- Mat

        /// <summary>
        /// Animation de mat (≈ 2 s) : le roi en <paramref name="kingSquare"/> vacille, bascule puis se brise ;
        /// onde de choc sur le plateau et pluie d'étincelles de la couleur du gagnant.
        /// Le roi reste absent du plateau ensuite (ShowPosition le remet, par exemple après annulation).
        /// </summary>
        public IEnumerator PlayMateAnimation(int kingSquare)
        {
            StopHint();
            ClearHintArrow();
            ClearSelection();
            var king = kingSquare >= 0 && kingSquare < 64 ? pieces[kingSquare] : null;
            if (king == null) yield break;

            Color winner = NeonPalette.Glow(!king.IsWhite);
            Vector3 center = king.transform.position;
            bool shattered = false;
            king.Checkmate(() => shattered = true);

            float t = 0f;
            while (!shattered && king != null && t < 3f) { t += Time.deltaTime; yield return null; }
            highlights.ClearCheck();
            if (pieces[kingSquare] == king || pieces[kingSquare] == null) pieces[kingSquare] = null;

            NeonEffects.Shockwave(center, winner);
            NeonEffects.SparkRain(winner, 1.2f);
            yield return new WaitForSeconds(0.55f);
        }

        IEnumerator HintRoutine(Move move)
        {
            ClearSelection();
            highlights.ShowSelection(Square.File(move.From), Square.Rank(move.From));
            var cell = new[] { new Vector2Int(Square.File(move.To), Square.Rank(move.To)) };
            if (move.IsCapture) highlights.ShowLegalMoves(new Vector2Int[0], cell);
            else highlights.ShowLegalMoves(cell, new Vector2Int[0]);
            if (pieces[move.From] != null) pieces[move.From].SetSelected(true);
            yield return new WaitForSeconds(HintSeconds);
            if (pieces[move.From] != null) pieces[move.From].SetSelected(false);
            highlights.ClearSelection();
            highlights.ClearLegalMoves();
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
        }

        // ---------------------------------------------------------------- Glisser-déposer

        const float DragLift = 0.45f;
        static readonly Plane BoardPlane = new Plane(Vector3.up, Vector3.zero);

        /// <summary>Point du plateau (y = 0) sous le rayon.</summary>
        public static bool TryBoardPoint(Ray ray, out Vector3 point)
        {
            point = Vector3.zero;
            if (!BoardPlane.Raycast(ray, out float d)) return false;
            point = ray.GetPoint(d);
            return true;
        }

        /// <summary>Case du plateau sous le rayon (ignore les pièces : sert au lâcher).</summary>
        public static int SquareUnder(Ray ray)
        {
            if (!TryBoardPoint(ray, out var p) || !BoardBuilder.WorldToSquare(p, out int f, out int r)) return Square.None;
            return Square.Make(f, r);
        }

        /// <summary>La pièce suit le doigt, soulevée au-dessus du plateau.</summary>
        public void DragPiece(int square, Ray ray)
        {
            var piece = square >= 0 ? pieces[square] : null;
            if (piece == null || !TryBoardPoint(ray, out var p)) return;
            p.x = Mathf.Clamp(p.x, -4.6f, 4.6f);
            p.z = Mathf.Clamp(p.z, -4.6f, 4.6f);
            piece.DragTo(p + Vector3.up * DragLift);
        }

        /// <summary>Lâcher refusé : la pièce revient en glissant sur sa case.</summary>
        public void ReturnPiece(int square)
        {
            var piece = square >= 0 ? pieces[square] : null;
            if (piece != null) piece.MoveTo(Square.File(square), Square.Rank(square), null, Speed);
        }

        // ---------------------------------------------------------------- Pièces prises

        const float TrayScale = 0.55f;
        const float TraySpacing = 0.56f;
        const float TrayOffset = 5.35f;
        // Portrait : la rangée proche est vue presque de dessus, on la grossit et on la rapproche du cadre.
        // (Valeurs reprises par OrbitCamera pour le cadrage.)
        const float PortraitTrayScale = 0.68f;
        const float PortraitTraySpacing = 0.6f;
        const float PortraitTrayOffset = 5.0f;
        static readonly PieceType[] TrayOrder = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight, PieceType.Pawn };
        static readonly int[] StartCount = { 0, 8, 2, 2, 2, 1, 0 }; // indexé par PieceType
        // Pièces de la position de départ de la partie (un puzzle ne part pas du jeu complet) : seules les pièces
        // prises depuis ce départ vont dans les rangées.
        readonly int[,] baseCount = new int[2, 7];
        bool hasBaseline;

        void SetTrayBaseline(Position start)
        {
            hasBaseline = true;
            for (int c = 0; c < 2; c++)
                for (int t = 1; t <= 5; t++)
                {
                    int count = 0;
                    for (int sq = 0; sq < 64; sq++)
                        if (start[sq] == Piece.Make((PieceColor)c, (PieceType)t)) count++;
                    baseCount[c, t] = count;
                }
        }

        Transform trayRoot;
        string trayKey;
        bool trayOnX;

        /// <summary>Axe des rangées fourni par la caméra (voir OrbitCamera.TraysOnXSides) ; à défaut, selon l'écran.</summary>
        public System.Func<bool> TraysOnXSides;

        bool CurrentTraysOnX => TraysOnXSides != null ? TraysOnXSides() : Screen.width > Screen.height;

        /// <summary>
        /// Pièces prises posées en miniature à côté du plateau : devant/derrière en portrait,
        /// à gauche/à droite en paysage, quel que soit le quart de tour de la caméra.
        /// Chaque camp garde de son côté du plateau les pièces qu'il a prises.
        /// </summary>
        Position lastPosition;

        void RefreshTrays(Position position)
        {
            lastPosition = position;
            bool onX = CurrentTraysOnX;
            bool portraitNow = Screen.height >= Screen.width;
            var sb = new System.Text.StringBuilder(onX ? "X" : "Z").Append(portraitNow ? "P" : "L");
            var missing = new int[2, 7];
            for (int c = 0; c < 2; c++)
                for (int t = 1; t <= 5; t++)
                {
                    int count = 0;
                    for (int sq = 0; sq < 64; sq++)
                        if (position[sq] == Piece.Make((PieceColor)c, (PieceType)t)) count++;
                    // Une promotion peut donner plus de pièces qu'au départ : on ne compte que les manquantes.
                    missing[c, t] = Mathf.Max(0, (hasBaseline ? baseCount[c, t] : StartCount[t]) - count);
                    sb.Append(missing[c, t]);
                }
            string key = sb.ToString();
            if (key == trayKey) return;
            trayKey = key;
            trayOnX = onX;

            for (int i = trayRoot.childCount - 1; i >= 0; i--) Destroy(trayRoot.GetChild(i).gameObject);

            // Les blancs exposent les pièces noires prises de leur côté (z négatif), et inversement.
            for (int capturer = 0; capturer < 2; capturer++)
            {
                int victim = 1 - capturer;
                int index = 0;
                foreach (var type in TrayOrder)
                    for (int n = 0; n < missing[victim, (int)type]; n++)
                        SpawnTrayPiece(type, victim == 0, capturer == 0, index++, onX);
            }
        }

        void SpawnTrayPiece(PieceType type, bool whitePiece, bool whiteSide, int index, bool onX)
        {
            bool portrait = Screen.height >= Screen.width;
            float spacing = portrait ? PortraitTraySpacing : TraySpacing;
            float along = -7f * spacing + index * spacing;
            float side = (whiteSide ? -1f : 1f) * (portrait ? PortraitTrayOffset : TrayOffset);
            var pos = onX ? new Vector3(side, 0f, whiteSide ? along : -along) : new Vector3(whiteSide ? along : -along, 0f, side);
            var view = PieceView.Create(ToKind(type), whitePiece, 0, 0, trayRoot);
            view.SetReflectionVisible(false);
            view.transform.position = pos;
            view.transform.localScale = Vector3.one * (portrait ? PortraitTrayScale : TrayScale);
        }

        void Update()
        {
            // Rotation de l'écran ou quart de tour de la caméra : les rangées de prises changent d'axe.
            if (trayKey != null && CurrentTraysOnX != trayOnX)
            {
                trayKey = null;
                if (lastPosition != null) RefreshTrays(lastPosition);
            }
            LeanTraysTowardCamera();
        }

        /// <summary>
        /// Une pièce prise vue presque de dessus (rangée proche en portrait) n'est qu'un disque :
        /// on la penche en arrière (à l'opposé de la caméra), pivot à la base, pour montrer son profil.
        /// </summary>
        void LeanTraysTowardCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            for (int i = 0; i < trayRoot.childCount; i++)
            {
                var piece = trayRoot.GetChild(i);
                Vector3 to = cam.transform.position - piece.position;
                var flat = new Vector3(to.x, 0f, to.z);
                if (flat.sqrMagnitude < 1e-4f) continue;
                float elevation = Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
                float tilt = Mathf.Clamp(elevation - 35f, 0f, 35f);
                piece.rotation = Quaternion.AngleAxis(tilt, Vector3.Cross(flat, Vector3.up).normalized);
            }
        }

        // ---------------------------------------------------------------- Picking

        /// <summary>
        /// Cases visées par le rayon : celle de la pièce touchée (pièces hautes vues de biais)
        /// et celle du plateau sous le rayon. L'une ou l'autre peut valoir Square.None.
        /// </summary>
        public bool TryPick(Ray ray, out int pieceSquare, out int boardSquare)
        {
            pieceSquare = boardSquare = Square.None;
            int count = Physics.RaycastNonAlloc(ray, rayHits, 100f);
            float bestPiece = float.MaxValue, bestBoard = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var h = rayHits[i];
                var view = h.collider.GetComponentInParent<PieceView>();
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

        readonly RaycastHit[] rayHits = new RaycastHit[16];

        // ---------------------------------------------------------------- Outils

        PieceView Spawn(int piece, int square)
        {
            var view = PieceView.Create(ToKind(Piece.TypeOf(piece)), Piece.ColorOf(piece) == PieceColor.White,
                Square.File(square), Square.Rank(square), piecesRoot);
            EnsureCollider(view);
            return view;
        }

        static void EnsureCollider(PieceView view) => FitCollider(view, view.Kind);

        static void FitCollider(PieceView view, PieceKind kind)
        {
            if (!view.TryGetComponent<CapsuleCollider>(out var c)) c = view.gameObject.AddComponent<CapsuleCollider>();
            float h = PieceMeshFactory.Height(kind);
            c.height = h;
            c.radius = 0.34f;
            c.center = new Vector3(0f, h * 0.5f, 0f);
        }

        public static PieceKind ToKind(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return PieceKind.Pawn;
                case PieceType.Knight: return PieceKind.Knight;
                case PieceType.Bishop: return PieceKind.Bishop;
                case PieceType.Rook: return PieceKind.Rook;
                case PieceType.Queen: return PieceKind.Queen;
                default: return PieceKind.King;
            }
        }
    }
}
