using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Représentation visuelle d'une pièce et ses animations :
    /// glissade avec traînée lumineuse (le cavalier, lui, saute en arc), atterrissage avec
    /// compression et rebond, sélection (lévitation + pulsation), capture (bris en éclats
    /// de lumière), promotion (flash puis changement de forme), mat (le roi vacille, bascule
    /// et se brise). Les couleurs suivent <see cref="VisualTheme"/> en direct.
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
        [SerializeField, Tooltip("Légère élévation pendant la glissade.")] float glideLift = 0.07f;
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
        float _speed = 1f;          // multiplicateur de vitesse des animations de coup
        TrailRenderer _trail;

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
            VisualTheme.Changed -= OnThemeChanged;
            VisualTheme.Changed += OnThemeChanged;

            SetKind(kind);
        }

        /// <summary>Recopie les couleurs du thème courant dans les instances de matériaux.</summary>
        void OnThemeChanged()
        {
            if (_body == null) return;
            CopyColors(_body, NeonMaterials.PieceBody(IsWhite));
            CopyColors(_accent, NeonMaterials.PieceAccent(IsWhite));
            _bodyEmission = _body.GetColor(NeonMaterials.EmissionColorProperty);
            _accentEmission = _accent.GetColor(NeonMaterials.EmissionColorProperty);
            _lastEmissionFactor = -1f;
            if (_trail != null) _trail.colorGradient = TrailGradient(GlowColor);
        }

        static void CopyColors(Material target, Material source)
        {
            target.SetColor(NeonMaterials.BaseColorProperty, source.GetColor(NeonMaterials.BaseColorProperty));
            target.SetColor(NeonMaterials.EmissionColorProperty, source.GetColor(NeonMaterials.EmissionColorProperty));
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
            if (_trail != null) _trail.Clear();
        }

        /// <summary>Glisser-déposer : la pièce suit le doigt (position monde), sans animation.</summary>
        public void DragTo(Vector3 worldPosition)
        {
            StopMotion();
            transform.position = worldPosition;
            if (_trail != null) _trail.Clear();
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
        /// Déplace la pièce jusqu'à la case : glissade avec traînée, ou saut en arc pour le cavalier.
        /// <paramref name="onArrived"/> est appelé à l'atterrissage (avant la fin du rebond),
        /// moment idéal pour déclencher la capture. <paramref name="speed"/> : 0,5 = lent, 2 = rapide.
        /// </summary>
        public Coroutine MoveTo(int file, int rank, Action onArrived = null, float speed = 1f)
        {
            StopMotion();
            File = file;
            Rank = rank;
            _selectTarget = 0f;
            _speed = Mathf.Clamp(speed, 0.1f, 8f);
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
            Vector3 leanAxis = dist > 0.001f ? Vector3.Cross(Vector3.up, delta / dist) : Vector3.right;
            float moving = dist > 0.001f ? 1f : 0f;

            // 1. Anticipation : légère compression.
            yield return Animate(0.07f, k =>
            {
                float s = Mathf.Sin(k * Mathf.PI * 0.5f);
                _squash = new Vector3(1f + 0.06f * s, 1f - 0.12f * s, 1f + 0.06f * s);
            });

            EnsureTrail(knight);
            if (moving > 0f) _trail.emitting = true;

            if (knight)
            {
                // 2a. Saut en arc, avec étirement et inclinaison dans le sens du mouvement.
                yield return Animate(duration, k =>
                {
                    float e = EaseInOutCubic(k);
                    Vector3 p = Vector3.Lerp(start, target, e);
                    p.y = start.y + knightArcHeight * 4f * k * (1f - k);
                    transform.position = p;
                    float stretch = Mathf.Sin(k * Mathf.PI);
                    _squash = new Vector3(1f - 0.05f * stretch, 1f + 0.10f * stretch, 1f - 0.05f * stretch);
                    _lean = Quaternion.AngleAxis(Mathf.Sin(k * Mathf.PI) * 14f * moving, leanAxis);
                });
            }
            else
            {
                // 2b. Glissade : à peine décollée, penchée vers l'avant au départ puis redressée à l'arrivée.
                yield return Animate(duration, k =>
                {
                    float e = EaseInOutCubic(k);
                    Vector3 p = Vector3.Lerp(start, target, e);
                    p.y = start.y + glideLift * Mathf.Sin(k * Mathf.PI);
                    transform.position = p;
                    float stretch = Mathf.Sin(k * Mathf.PI);
                    _squash = new Vector3(1f + 0.03f * stretch, 1f - 0.04f * stretch, 1f + 0.03f * stretch);
                    // Inclinaison : vers l'avant à l'accélération, en arrière au freinage.
                    float lean = Mathf.Sin(k * Mathf.PI * 2f) * 7f * moving;
                    _lean = Quaternion.AngleAxis(lean, leanAxis);
                });
            }

            transform.position = target;
            _lean = Quaternion.identity;
            if (_trail != null) _trail.emitting = false;
            NeonEffects.LandingPulse(target, GlowColor);
            onArrived?.Invoke();

            // 3. Atterrissage : compression puis rebond amorti.
            yield return Animate(0.32f, k =>
            {
                float t = k * 0.32f;
                float s = (knight ? -0.16f : -0.09f) * Mathf.Exp(-9f * t) * Mathf.Cos(t * 26f);
                _squash = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);
            });
            _squash = Vector3.one;
            _motion = null;
        }

        /// <summary>Traînée lumineuse créée au premier déplacement (matériau néon partagé).</summary>
        void EnsureTrail(bool knight)
        {
            if (_trail == null)
            {
                var go = new GameObject("Trail");
                go.transform.SetParent(transform, false);
                _trail = go.AddComponent<TrailRenderer>();
                _trail.sharedMaterial = NeonMaterials.Trail;
                _trail.minVertexDistance = 0.03f;
                _trail.alignment = LineAlignment.View;
                _trail.textureMode = LineTextureMode.Stretch;
                _trail.numCapVertices = 2;
                _trail.shadowCastingMode = ShadowCastingMode.Off;
                _trail.receiveShadows = false;
                _trail.lightProbeUsage = LightProbeUsage.Off;
                _trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
                _trail.colorGradient = TrailGradient(GlowColor);
                _trail.emitting = false;
            }
            // Le cavalier laisse une traînée plus fine à mi-hauteur ; les autres un sillage au ras du verre.
            float h = PieceMeshFactory.Height(Kind);
            _trail.transform.localPosition = new Vector3(0f, knight ? h * 0.45f : 0.16f, 0f);
            _trail.widthMultiplier = knight ? 0.22f : 0.34f;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            _trail.time = 0.3f / _speed; // même longueur de sillage quelle que soit la vitesse
            _trail.Clear();
        }

        static Gradient TrailGradient(Color glow)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(glow, Color.white, 0.35f), 0f), new GradientColorKey(glow, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.45f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ------------------------------------------------------------------ capture

        /// <summary>
        /// Capture : la pièce s'illumine, se brise en éclats de lumière puis l'objet est détruit.
        /// <paramref name="onDone"/> est appelé juste avant la destruction.
        /// </summary>
        public void Capture(Action onDone = null, float speed = 1f)
        {
            StopMotion();
            _busy = true;
            _speed = Mathf.Clamp(speed, 0.1f, 8f);
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
        public Coroutine Promote(PieceKind newKind, Action onDone = null, float speed = 1f)
        {
            _busy = true;
            _speed = Mathf.Clamp(speed, 0.1f, 8f);
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

        // ------------------------------------------------------------------ mat

        /// <summary>
        /// Roi maté : il vacille, bascule vers son camp, puis se brise en éclats (≈ 1,6 s).
        /// <paramref name="onShattered"/> est appelé au moment du bris ; l'objet est ensuite détruit.
        /// </summary>
        public Coroutine Checkmate(Action onShattered = null)
        {
            StopMotion();
            _busy = true;
            _selectTarget = 0f;
            _speed = 1f;
            return StartCoroutine(CheckmateRoutine(onShattered));
        }

        IEnumerator CheckmateRoutine(Action onShattered)
        {
            float h = PieceMeshFactory.Height(Kind);
            Vector3 fall = IsWhite ? Vector3.back : Vector3.forward; // il tombe vers son propre camp
            Vector3 axis = Vector3.Cross(Vector3.up, fall);

            // 1. Vacillement d'amplitude croissante, la lueur monte.
            yield return Animate(0.75f, k =>
            {
                float amp = Mathf.Lerp(2f, 10f, k * k);
                _lean = Quaternion.AngleAxis(Mathf.Sin(k * 24f) * amp, axis) * Quaternion.AngleAxis(Mathf.Sin(k * 17f + 1f) * amp * 0.6f, fall);
                _flash = k * 1.5f;
            });

            // 2. Bascule : chute accélérée jusqu'à presque l'horizontale (soulevée pour ne pas traverser le verre).
            yield return Animate(0.42f, k =>
            {
                float e = k * k;
                _lean = Quaternion.AngleAxis(e * 80f, axis);
                _extraLift = 0.16f * Mathf.Sin(e * Mathf.PI * 0.5f);
                _flash = 1.5f + k;
            });
            NeonEffects.LandingPulse(transform.position + fall * h * 0.5f, GlowColor);

            // Petit rebond à l'impact, la pièce se charge de lumière.
            yield return Animate(0.2f, k =>
            {
                _lean = Quaternion.AngleAxis(80f - 7f * Mathf.Sin(k * Mathf.PI), axis);
                _flash = 2.5f + k * 3.5f;
            });

            // 3. Bris.
            NeonEffects.Shatter(transform.position + fall * h * 0.45f + Vector3.up * 0.05f, 0.5f, GlowColor);
            NeonEffects.Shatter(transform.position + fall * h * 0.8f, 0.4f, GlowColor);
            onShattered?.Invoke();
            yield return Animate(0.18f, k =>
            {
                _flash = 6f + k * 4f;
                float s = 1f - k;
                _squash = new Vector3(s * 1.2f, s, s * 1.2f);
            });
            _busy = false;
            Destroy(gameObject);
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
            VisualTheme.Changed -= OnThemeChanged;
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
            if (_trail != null) _trail.emitting = false;
        }

        static Vector3 WorldOf(int file, int rank) => BoardBuilder.SquareToWorld(file, rank);

        IEnumerator Animate(float duration, Action<float> step)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime * _speed;
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
