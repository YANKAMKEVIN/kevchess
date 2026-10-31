using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Représentation visuelle d'une pièce et ses animations :
    /// déplacement en arc (anticipation, vol, atterrissage avec compression et rebond),
    /// saut plus haut pour le cavalier, sélection (lévitation + pulsation),
    /// capture (bris en éclats de lumière), promotion (flash puis changement de forme).
    ///
    /// Hiérarchie : racine (position sur le plateau, sans rotation) → « Visual » (maillage,
    /// squash/stretch, inclinaison, lévitation) ; « Reflection » = copie miroir sous le verre.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PieceView : MonoBehaviour
    {
        [Header("Déplacement")]
        [SerializeField, Tooltip("Durée de base d'un déplacement (s).")]
        float baseMoveDuration = 0.30f;
        [SerializeField, Tooltip("Durée ajoutée par case parcourue (s).")]
        float durationPerSquare = 0.05f;
        [SerializeField] float arcHeight = 0.32f;
        [SerializeField] float knightArcHeight = 0.95f;

        [Header("Sélection")]
        [SerializeField] float hoverHeight = 0.13f;
        [SerializeField] float hoverBob = 0.025f;

        [Header("Reflet")]
        [SerializeField, Tooltip("Copie miroir sous le plateau de verre (2 draw calls par pièce au lieu d'1).")]
        bool showReflection = true;

        public PieceKind Kind { get; private set; }
        public bool IsWhite { get; private set; }
        public int File { get; private set; } = -1;
        public int Rank { get; private set; } = -1;
        public bool IsSelected => _selectTarget > 0.5f;
        /// <summary>Vrai pendant un déplacement, une capture ou une promotion.</summary>
        public bool IsAnimating => _motion != null || _busy;

        /// <summary>Couleur de lueur de la pièce (cyan pour les blancs, magenta pour les noirs).</summary>
        public Color GlowColor => NeonPalette.Glow(IsWhite);

        Transform _visual, _reflection;
        MeshFilter _filter, _reflectionFilter;
        MeshRenderer _renderer, _reflectionRenderer;
        Material _body, _accent;
        Color _bodyEmission, _accentEmission;

        float _selectWeight, _selectTarget;
        float _flash;               // surcroît d'émission (0 = normal)
        float _lastEmissionFactor = -1f;
        Vector3 _squash = Vector3.one;
        Quaternion _lean = Quaternion.identity;
        float _extraLift;           // lévitation hors sélection (promotion)
        Coroutine _motion;
        bool _busy;

        // ------------------------------------------------------------------ création

        /// <summary>Crée une pièce posée sur la case donnée.</summary>
        public static PieceView Create(PieceKind kind, bool isWhite, int file, int rank, Transform parent = null)
        {
            var go = new GameObject((isWhite ? "White " : "Black ") + kind);
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<PieceView>();
            view.Initialize(kind, isWhite);
            view.SnapTo(file, rank);
            return view;
        }

        /// <summary>Initialise (ou réinitialise) la pièce. Appelé par <see cref="Create"/>.</summary>
        public void Initialize(PieceKind kind, bool isWhite)
        {
            IsWhite = isWhite;
            EnsureHierarchy();

            ReleaseMaterials();
            _body = new Material(NeonMaterials.PieceBody(isWhite));
            _accent = new Material(NeonMaterials.PieceAccent(isWhite));
            _bodyEmission = _body.GetColor(NeonMaterials.EmissionColorProperty);
            _accentEmission = _accent.GetColor(NeonMaterials.EmissionColorProperty);
            var mats = new[] { _body, _accent };
            _renderer.sharedMaterials = mats;
            _reflectionRenderer.sharedMaterials = mats;
            _lastEmissionFactor = -1f;

            SetKind(kind);
        }

        /// <summary>Change la forme de la pièce (sans effet ; voir <see cref="Promote"/>).</summary>
        public void SetKind(PieceKind kind)
        {
            Kind = kind;
            Mesh mesh = PieceMeshFactory.Get(kind);
            _filter.sharedMesh = mesh;
            _reflectionFilter.sharedMesh = mesh;
            gameObject.name = (IsWhite ? "White " : "Black ") + kind;
        }

        /// <summary>Place immédiatement la pièce sur une case (annule un déplacement en cours).</summary>
        public void SnapTo(int file, int rank)
        {
            StopMotion();
            File = file;
            Rank = rank;
            transform.position = WorldOf(file, rank);
        }

        /// <summary>Glisser-déposer : la pièce suit le doigt (position monde), sans animation.</summary>
        public void DragTo(Vector3 worldPosition)
        {
            StopMotion();
            transform.position = worldPosition;
        }

        /// <summary>Affiche ou masque le reflet miroir sous le verre.</summary>
        public void SetReflectionVisible(bool visible)
        {
            showReflection = visible;
            if (_reflection != null) _reflection.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------ sélection

        /// <summary>Sélection : la pièce lévite légèrement et sa lueur pulse.</summary>
        public void SetSelected(bool selected)
        {
            _selectTarget = selected ? 1f : 0f;
        }

        // ------------------------------------------------------------------ déplacement

        /// <summary>
        /// Déplace la pièce en arc jusqu'à la case. <paramref name="onArrived"/> est appelé
        /// à l'atterrissage (avant la fin du rebond), moment idéal pour déclencher la capture.
        /// </summary>
        public Coroutine MoveTo(int file, int rank, Action onArrived = null)
        {
            StopMotion();
            File = file;
            Rank = rank;
            _selectTarget = 0f;
            _motion = StartCoroutine(MoveRoutine(WorldOf(file, rank), onArrived));
            return _motion;
        }

        IEnumerator MoveRoutine(Vector3 target, Action onArrived)
        {
            Vector3 start = transform.position;
            Vector3 delta = target - start;
            delta.y = 0f;
            float dist = delta.magnitude;
            bool knight = Kind == PieceKind.Knight;
            float duration = Mathf.Clamp(baseMoveDuration + dist * durationPerSquare, 0.3f, 0.7f) + (knight ? 0.08f : 0f);
            float arc = knight ? knightArcHeight : arcHeight * Mathf.Lerp(0.8f, 1.6f, Mathf.Clamp01(dist / 7f));
            Vector3 leanAxis = dist > 0.001f ? Vector3.Cross(Vector3.up, delta / dist) : Vector3.right;

            // 1. Anticipation : légère compression.
            yield return Animate(0.07f, k =>
            {
                float s = Mathf.Sin(k * Mathf.PI * 0.5f);
                _squash = new Vector3(1f + 0.06f * s, 1f - 0.12f * s, 1f + 0.06f * s);
            });

            // 2. Vol en arc, avec étirement et inclinaison dans le sens du mouvement.
            yield return Animate(duration, k =>
            {
                float e = EaseInOutCubic(k);
                Vector3 p = Vector3.Lerp(start, target, e);
                p.y = start.y + arc * 4f * k * (1f - k);
                transform.position = p;
                float stretch = Mathf.Sin(k * Mathf.PI);
                _squash = new Vector3(1f - 0.05f * stretch, 1f + 0.10f * stretch, 1f - 0.05f * stretch);
                float leanAngle = Mathf.Sin(k * Mathf.PI) * (knight ? 14f : 9f) * (dist > 0.001f ? 1f : 0f);
                _lean = Quaternion.AngleAxis(leanAngle, leanAxis);
            });

            transform.position = target;
            _lean = Quaternion.identity;
            NeonEffects.LandingPulse(target, GlowColor);
            onArrived?.Invoke();

            // 3. Atterrissage : compression puis rebond amorti.
            yield return Animate(0.32f, k =>
            {
                float t = k * 0.32f;
                float s = -0.16f * Mathf.Exp(-9f * t) * Mathf.Cos(t * 26f);
                _squash = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);
            });
            _squash = Vector3.one;
            _motion = null;
        }

        // ------------------------------------------------------------------ capture

        /// <summary>
        /// Capture : la pièce s'illumine, se brise en éclats de lumière puis l'objet est détruit.
        /// <paramref name="onDone"/> est appelé juste avant la destruction.
        /// </summary>
        public void Capture(Action onDone = null)
        {
            StopMotion();
            _busy = true;
            _selectTarget = 0f;
            StartCoroutine(CaptureRoutine(onDone));
        }

        IEnumerator CaptureRoutine(Action onDone)
        {
            float h = PieceMeshFactory.Height(Kind);
            // Montée en lumière et gonflement.
            yield return Animate(0.09f, k =>
            {
                _flash = k * 4f;
                float s = 1f + 0.08f * k;
                _squash = new Vector3(s, s, s);
            });

            NeonEffects.Shatter(transform.position, h, GlowColor);

            // Dissolution : la silhouette s'effondre en lumière.
            yield return Animate(0.16f, k =>
            {
                _flash = 4f + k * 4f;
                float xz = 1.08f + 0.35f * k;
                _squash = new Vector3(xz * (1f - k), 1.08f * (1f - k * k), xz * (1f - k));
            });

            _busy = false;
            onDone?.Invoke();
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ promotion

        /// <summary>Promotion : flash de lumière, changement de forme au pic, puis retour en douceur.</summary>
        public Coroutine Promote(PieceKind newKind, Action onDone = null)
        {
            _busy = true;
            return StartCoroutine(PromoteRoutine(newKind, onDone));
        }

        IEnumerator PromoteRoutine(PieceKind newKind, Action onDone)
        {
            // Attendre la fin d'un éventuel déplacement (le pion arrive sur la dernière rangée).
            while (_motion != null) yield return null;

            yield return Animate(0.16f, k =>
            {
                _flash = k * 6f;
                _extraLift = 0.15f * EaseOutCubic(k);
                _squash = new Vector3(1f - 0.25f * k, 1f + 0.15f * k, 1f - 0.25f * k);
            });

            NeonEffects.Flash(transform.position, GlowColor, PieceMeshFactory.Height(newKind));
            SetKind(newKind);

            yield return Animate(0.45f, k =>
            {
                _flash = 6f * (1f - k) * (1f - k);
                _extraLift = 0.15f * (1f - EaseInOutCubic(k));
                float pop = 1f + 0.22f * Mathf.Sin(k * Mathf.PI) * (1f - k);
                _squash = new Vector3(pop, pop, pop);
            });
            _flash = 0f;
            _extraLift = 0f;
            _squash = Vector3.one;
            _busy = false;
            onDone?.Invoke();
        }

        // ------------------------------------------------------------------ boucle

        void Awake()
        {
            EnsureHierarchy();
        }

        void Update()
        {
            if (_body == null) return; // pas encore initialisée
            float dt = Time.deltaTime;
            _selectWeight = Mathf.MoveTowards(_selectWeight, _selectTarget, dt * 6f);
            float w = Smooth(_selectWeight);
            float time = Time.time;

            float hover = w * (hoverHeight + hoverBob * Mathf.Sin(time * 3.2f)) + _extraLift;
            // Le cavalier se montre de profil (sinon, vu de face, il n'est qu'une lame fine) ;
            // les autres pièces regardent l'adversaire.
            float yaw = Kind == PieceKind.Knight ? (IsWhite ? -90f : 90f) : (IsWhite ? 0f : 180f);
            float spin = w * Mathf.Sin(time * 1.3f) * 6f; // légère oscillation en sélection

            _visual.localPosition = new Vector3(0f, hover, 0f);
            _visual.localRotation = _lean * Quaternion.Euler(0f, yaw + spin, 0f);
            _visual.localScale = _squash;

            float emission = 1f + w * (0.7f + 0.5f * Mathf.Sin(time * 5.5f)) + _flash;
            if (Mathf.Abs(emission - _lastEmissionFactor) > 0.002f)
            {
                _body.SetColor(NeonMaterials.EmissionColorProperty, _bodyEmission * (1f + (emission - 1f) * 2.5f));
                _accent.SetColor(NeonMaterials.EmissionColorProperty, _accentEmission * emission);
                _lastEmissionFactor = emission;
            }
        }

        void LateUpdate()
        {
            if (!showReflection || _reflection == null) return;
            // Miroir par rapport au plan y = 0 du plateau.
            Vector3 p = _visual.position;
            _reflection.position = new Vector3(p.x, -p.y, p.z);
            Quaternion q = _visual.rotation;
            _reflection.rotation = new Quaternion(-q.x, q.y, -q.z, q.w);
            Vector3 s = _visual.localScale;
            _reflection.localScale = new Vector3(s.x, -s.y, s.z);
        }

        void OnDestroy()
        {
            ReleaseMaterials();
        }

        // ------------------------------------------------------------------ utilitaires

        void EnsureHierarchy()
        {
            if (_visual != null) return;
            transform.rotation = Quaternion.identity;

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _filter = _visual.gameObject.AddComponent<MeshFilter>();
            _renderer = _visual.gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.On;
            _renderer.receiveShadows = true;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;

            _reflection = new GameObject("Reflection").transform;
            _reflection.SetParent(transform, false);
            _reflectionFilter = _reflection.gameObject.AddComponent<MeshFilter>();
            _reflectionRenderer = _reflection.gameObject.AddComponent<MeshRenderer>();
            _reflectionRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _reflectionRenderer.receiveShadows = false;
            _reflectionRenderer.lightProbeUsage = LightProbeUsage.Off;
            _reflection.gameObject.SetActive(showReflection);
        }

        void ReleaseMaterials()
        {
            if (_body != null) Destroy(_body);
            if (_accent != null) Destroy(_accent);
            _body = _accent = null;
        }

        void StopMotion()
        {
            if (_motion != null)
            {
                StopCoroutine(_motion);
                _motion = null;
            }
            _squash = Vector3.one;
            _lean = Quaternion.identity;
        }

        static Vector3 WorldOf(int file, int rank) => BoardBuilder.SquareToWorld(file, rank);

        static IEnumerator Animate(float duration, Action<float> step)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
        }

        static float EaseInOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f;
        static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
