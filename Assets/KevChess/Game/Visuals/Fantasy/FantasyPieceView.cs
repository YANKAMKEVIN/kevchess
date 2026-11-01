using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals.Fantasy
{
    /// <summary>
    /// Personnage du mode Légendes posé sur le plateau : pendant de <see cref="PieceView"/>.
    ///
    /// Repos désynchronisé (respiration, cristal du mage, ailes, ondulation du dragon), déplacement
    /// (marche, galop sauté du chevalier, vol de la reine et du dragon), sélection, statuts (bouclier,
    /// gel), téléportation, invocation du dragon depuis le golem, frappes propres à chaque type,
    /// réaction au coup et mort en éclats de lumière.
    ///
    /// Hiérarchie : racine (position sur le plateau, sans rotation) → « Model » (racine animée du
    /// <see cref="CharacterRig"/> : orientation, lévitation, écrasement) → os ; plus, à la demande,
    /// « Shield » (dôme hexagonal) et « Ice » (gangue de glace), enfants de la racine.
    ///
    /// Une seule action à la fois (déplacement, téléportation, frappe, invocation) : en lancer une
    /// nouvelle interrompt la précédente. Les méthodes d'action renvoient la coroutine, que l'appelant
    /// peut attendre avec <c>yield return</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FantasyPieceView : MonoBehaviour
    {
        [Header("Sélection")]
        [SerializeField] float hoverHeight = 0.12f;
        [SerializeField] float hoverBob = 0.02f;

        [Header("Vitesses")]
        [SerializeField, Tooltip("Vitesse de rotation pour s'orienter (degrés/s).")]
        float turnSpeed = 620f;

        static readonly Color IceBody = new Color(0.62f, 0.82f, 0.98f);
        static readonly Color IceEmission = new Color(0.10f, 0.22f, 0.34f);
        static readonly Color IceAccent = new Color(0.55f, 0.85f, 1f) * 1.2f;

        /// <summary>Personnage affiché (devient <see cref="CharacterKind.Dragon"/> après une invocation).</summary>
        public CharacterKind Kind { get; private set; }
        public bool IsWhite { get; private set; }
        public int File { get; private set; } = -1;
        public int Rank { get; private set; } = -1;
        public bool IsSelected => _selectTarget > 0.5f;
        public bool IsShielded { get; private set; }
        public bool IsFrozen { get; private set; }
        /// <summary>Vrai pendant un déplacement, une téléportation, une frappe, une invocation ou la mort.</summary>
        public bool IsAnimating => _action != null || _busy;
        /// <summary>Couleur de lueur (cyan pour les blancs, magenta pour les noirs).</summary>
        public Color GlowColor => NeonPalette.Glow(IsWhite);
        /// <summary>Hauteur du personnage (placement des effets, cadrage des duels).</summary>
        public float Height => FantasyPieceFactory.Height(Kind);
        /// <summary>Orientation par défaut : chaque camp regarde l'adversaire (blancs vers +Z).</summary>
        public float DefaultYaw => IsWhite ? 0f : 180f;
        /// <summary>Squelette animé (accès aux os pour accrocher des effets).</summary>
        public CharacterRig Rig => _rig;

        Transform _model;
        CharacterRig _rig;
        Material _body, _accent, _crackMat;
        Color _bodyBase, _bodyEmission, _accentEmission;

        float _phase, _animTime;
        float _selectWeight, _selectTarget;
        float _flash, _lastEmissionKey = -1f;
        float _freezeWeight;
        float _yaw, _yawTarget;
        float _lookYaw;
        Vector3? _lookPoint;
        float _walkPhase, _walkWeight, _flyPhase, _flyWeight;
        StrikeKind _strikeKind;
        float _strikeT, _strikeWeight;
        float _lift;
        Vector3 _offset;
        Quaternion _lean = Quaternion.identity;
        Vector3 _squash = Vector3.one;
        float _crackGlow;
        Coroutine _action;
        bool _busy;

        Transform _shield, _ice;
        Material _shieldFill, _shieldEdge, _iceMat;
        float _shieldWeight, _shieldPulse;

        // ------------------------------------------------------------------ création

        /// <summary>Crée un personnage posé sur la case donnée, tourné vers l'adversaire.</summary>
        public static FantasyPieceView Create(CharacterKind kind, bool isWhite, int file, int rank, Transform parent = null)
        {
            var go = new GameObject((isWhite ? "White " : "Black ") + kind);
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<FantasyPieceView>();
            view.Initialize(kind, isWhite);
            view.SnapTo(file, rank);
            return view;
        }

        /// <summary>Initialise (ou réinitialise) le personnage. Appelé par <see cref="Create"/>.</summary>
        public void Initialize(CharacterKind kind, bool isWhite)
        {
            IsWhite = isWhite;
            EnsureHierarchy();
            ReleaseMaterials();
            _body = new Material(NeonMaterials.PieceBody(isWhite));
            _accent = new Material(NeonMaterials.PieceAccent(isWhite));
            _bodyBase = _body.GetColor(NeonMaterials.BaseColorProperty);
            _bodyEmission = _body.GetColor(NeonMaterials.EmissionColorProperty);
            _accentEmission = _accent.GetColor(NeonMaterials.EmissionColorProperty);
            _lastEmissionKey = -1f;
            _phase = UnityEngine.Random.Range(0f, 20f);
            _yaw = _yawTarget = DefaultYaw;
            BuildRig(kind);
        }

        void BuildRig(CharacterKind kind)
        {
            Kind = kind;
            if (kind == CharacterKind.Golem && _crackMat == null)
                _crackMat = NeonMaterials.CreateAdditive("KevChess_GolemCracks", null, Color.black, 24);
            _rig = CharacterRig.Create(kind, _model, new[] { _body, _accent }, _crackMat);
            _rig.Yaw = _yaw;
            gameObject.name = (IsWhite ? "White " : "Black ") + kind;
        }

        /// <summary>Place immédiatement le personnage sur une case (annule l'action en cours).</summary>
        public void SnapTo(int file, int rank)
        {
            StopAction();
            File = file;
            Rank = rank;
            transform.position = WorldOf(file, rank);
        }

        /// <summary>Glisser-déposer : le personnage suit le doigt (position monde), sans animation.</summary>
        public void DragTo(Vector3 worldPosition)
        {
            StopAction();
            transform.position = worldPosition;
        }

        // ------------------------------------------------------------------ orientation, regard, sélection

        /// <summary>Oriente le personnage vers un point monde (en douceur, ou immédiatement).</summary>
        public void FaceTowards(Vector3 worldPoint, bool instant = false)
        {
            Vector3 d = worldPoint - transform.position;
            if (new Vector2(d.x, d.z).sqrMagnitude < 1e-6f) return;
            _yawTarget = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (instant) _yaw = _yawTarget;
        }

        /// <summary>Revient à l'orientation par défaut (face à l'adversaire).</summary>
        public void FaceDefault(bool instant = false)
        {
            _yawTarget = DefaultYaw;
            if (instant) _yaw = _yawTarget;
        }

        /// <summary>Le regard (tête ou torse) suit un point monde, ex. la pièce sélectionnée ; null pour l'arrêter.</summary>
        public void SetLookTarget(Vector3? worldPoint)
        {
            _lookPoint = worldPoint;
        }

        /// <summary>Sélection : le personnage lévite légèrement et sa lueur pulse.</summary>
        public void SetSelected(bool selected)
        {
            _selectTarget = selected ? 1f : 0f;
        }

        // ------------------------------------------------------------------ déplacement

        /// <summary>
        /// Déplace le personnage jusqu'à la case : marche (sentinelle, mage, golem, roi), galop sauté
        /// (chevalier), vol (reine, dragon). <paramref name="onArrived"/> est appelé à l'arrivée,
        /// avant le petit rebond final.
        /// </summary>
        public Coroutine MoveTo(int file, int rank, Action onArrived = null)
        {
            StopAction();
            File = file;
            Rank = rank;
            _selectTarget = 0f;
            _action = StartCoroutine(MoveRoutine(WorldOf(file, rank), onArrived));
            return _action;
        }

        IEnumerator MoveRoutine(Vector3 target, Action onArrived)
        {
            Vector3 start = transform.position;
            Vector3 delta = target - start;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 0.001f) _yawTarget = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

            if (CharacterKinds.Flies(Kind)) yield return FlyRoutine(start, target, dist);
            else if (Kind == CharacterKind.Knight) yield return GallopRoutine(start, target, dist);
            else yield return WalkRoutine(start, target, dist);

            transform.position = target;
            NeonEffects.LandingPulse(target, GlowColor);
            onArrived?.Invoke();
            _yawTarget = DefaultYaw;

            float w0 = _walkWeight, f0 = _flyWeight;
            yield return Animate(0.28f, k =>
            {
                _walkWeight = w0 * (1f - k);
                _flyWeight = f0 * (1f - k);
                float t = k * 0.28f;
                float s = -0.10f * Mathf.Exp(-10f * t) * Mathf.Cos(t * 26f);
                _squash = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);
            });
            ResetMotion();
            _action = null;
        }

        IEnumerator TurnRoutine(float maxTime)
        {
            float t = 0f;
            while (Mathf.Abs(Mathf.DeltaAngle(_yaw, _yawTarget)) > 12f && t < maxTime)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator WalkRoutine(Vector3 start, Vector3 target, float dist)
        {
            yield return TurnRoutine(0.18f);
            float stride = Kind == CharacterKind.King ? 0.25f : Kind == CharacterKind.Mage ? 0.2f : 0.16f;
            float duration = Mathf.Clamp(0.3f + dist * 0.13f, 0.4f, 1.1f);
            float phase0 = _walkPhase;
            yield return Animate(duration, k =>
            {
                float e = EaseInOutQuad(k);
                Vector3 p = Vector3.Lerp(start, target, e);
                transform.position = p;
                _walkPhase = phase0 + dist * e / stride * Mathf.PI;
                _walkWeight = Mathf.Min(1f, k * 6f, (1f - k) * 4f + 0.25f);
            });
        }

        IEnumerator GallopRoutine(Vector3 start, Vector3 target, float dist)
        {
            yield return TurnRoutine(0.15f);
            float duration = Mathf.Clamp(0.45f + dist * 0.08f, 0.5f, 0.8f);
            const float arc = 0.5f;
            yield return Animate(duration, k =>
            {
                float e = EaseInOutCubic(k);
                Vector3 p = Vector3.Lerp(start, target, e);
                p.y = start.y + arc * 4f * k * (1f - k);
                transform.position = p;
                _walkPhase = k * Mathf.PI * 4f;
                _walkWeight = Mathf.Min(1f, k * 8f, (1f - k) * 8f);
                _lean = Quaternion.AngleAxis(Mathf.Lerp(-12f, 12f, k) * Mathf.Sin(k * Mathf.PI), Vector3.Cross(Vector3.up, (target - start).normalized));
            });
            _lean = Quaternion.identity;
        }

        IEnumerator FlyRoutine(Vector3 start, Vector3 target, float dist)
        {
            float duration = Mathf.Clamp(0.5f + dist * 0.08f, 0.6f, 1.1f);
            float height = Kind == CharacterKind.Dragon ? 0.25f : 0.36f;
            float flapRate = Kind == CharacterKind.Dragon ? 10f : 17f;
            yield return Animate(duration, k =>
            {
                float e = EaseInOutCubic(k);
                transform.position = Vector3.Lerp(start, target, e);
                float env = Smoothstep(0f, 0.25f, k) * (1f - Smoothstep(0.75f, 1f, k));
                _lift = height * env;
                _flyWeight = Mathf.Max(env, 0.35f * Mathf.Sin(k * Mathf.PI));
                _flyPhase += Time.deltaTime * flapRate;
            });
            _lift = 0f;
        }

        // ------------------------------------------------------------------ statuts

        /// <summary>Bouclier : bulle hexagonale translucide autour du personnage.</summary>
        public void SetShielded(bool shielded)
        {
            IsShielded = shielded;
            if (shielded) EnsureShield();
        }

        /// <summary>Fait frémir le bouclier (quand on le vise ou qu'il encaisse un coup).</summary>
        public void PulseShield(float strength = 1f)
        {
            _shieldPulse = Mathf.Max(_shieldPulse, strength);
        }

        /// <summary>Gel : gangue de glace, givre, teinte bleu pâle ; l'animation de repos se fige.</summary>
        public void SetFrozen(bool frozen)
        {
            if (frozen == IsFrozen) return;
            IsFrozen = frozen;
            EnsureIce();
            FantasyFx.Frost(transform.position, Height, frozen ? 36 : 18);
        }

        void EnsureShield()
        {
            if (_shield != null) return;
            var go = new GameObject("Shield");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = FantasyPieceFactory.ShieldDome();
            var r = go.AddComponent<MeshRenderer>();
            _shieldFill = NeonMaterials.CreateAdditive("KevChess_ShieldFill", null, Color.black, 26);
            _shieldEdge = NeonMaterials.CreateAdditive("KevChess_ShieldEdge", null, Color.black, 27);
            r.sharedMaterials = new[] { _shieldFill, _shieldEdge };
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            _shield = go.transform;
            _shield.localScale = Vector3.zero;
        }

        void EnsureIce()
        {
            if (_ice != null) return;
            var go = new GameObject("Ice");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = FantasyPieceFactory.IceShell();
            var r = go.AddComponent<MeshRenderer>();
            _iceMat = NeonMaterials.CreateLitTransparent("KevChess_Ice", new Color(0.55f, 0.82f, 1f, 0.30f), 0.1f, 0.97f, new Color(0.06f, 0.16f, 0.28f));
            r.sharedMaterial = _iceMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            _ice = go.transform;
            _ice.localScale = Vector3.zero;
        }

        // ------------------------------------------------------------------ téléportation

        /// <summary>
        /// Téléportation : le personnage se défait en filaments de lumière, puis se recompose sur la case.
        /// </summary>
        public Coroutine Teleport(int file, int rank, Action onDone = null)
        {
            StopAction();
            File = file;
            Rank = rank;
            _selectTarget = 0f;
            _action = StartCoroutine(TeleportRoutine(WorldOf(file, rank), onDone));
            return _action;
        }

        IEnumerator TeleportRoutine(Vector3 target, Action onDone)
        {
            float h = Height;
            Color c = GlowColor;
            Vector3 start = transform.position;
            FantasyFx.Filaments(start, h, c, true);
            bool second = false;
            yield return Animate(0.38f, k =>
            {
                float e = k * k;
                _flash = k * 5f;
                _squash = new Vector3(1f - 0.93f * e, 1f + 0.7f * e, 1f - 0.93f * e);
                if (!second && k > 0.5f)
                {
                    second = true;
                    FantasyFx.Filaments(start, h, c, true, 30);
                }
            });
            _model.gameObject.SetActive(false);
            NeonEffects.LandingPulse(start, c);
            yield return Wait(0.12f);

            transform.position = target;
            FantasyFx.Filaments(target, h, c, false);
            yield return Wait(0.16f);
            _model.gameObject.SetActive(true);
            yield return Animate(0.36f, k =>
            {
                float e = (1f - k) * (1f - k);
                _flash = 5f * e;
                _squash = new Vector3(1f - 0.93f * e, 1f + 0.7f * e, 1f - 0.93f * e);
            });
            NeonEffects.LandingPulse(target, c);
            ResetMotion();
            _action = null;
            onDone?.Invoke();
        }

        // ------------------------------------------------------------------ invocation du dragon

        /// <summary>
        /// Invocation : le golem se fissure, la lumière perce, il éclate et le dragon sort des débris
        /// en déployant ses ailes. Après l'appel, <see cref="Kind"/> vaut <see cref="CharacterKind.Dragon"/>.
        /// </summary>
        public Coroutine TransformToDragon(Action onDone = null)
        {
            StopAction();
            _selectTarget = 0f;
            _action = StartCoroutine(SummonRoutine(onDone));
            return _action;
        }

        IEnumerator SummonRoutine(Action onDone)
        {
            Color c = GlowColor;
            Vector3 pos = transform.position;
            if (Kind != CharacterKind.Golem)
                Debug.LogWarning("[KevChess] TransformToDragon appelé sur " + Kind + " (prévu pour le golem).");

            Transform cracks = _rig.Bone(RigBone.Cracks);
            if (cracks != null) cracks.gameObject.SetActive(true);
            yield return Animate(1.0f, k =>
            {
                float flicker = 0.75f + 0.25f * Mathf.Sin(Time.time * 47f);
                _crackGlow = (k * k * 7f + 0.4f) * flicker;
                _offset = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)) * (0.025f * k);
                _flash = k * 1.5f;
                float s = 1f + 0.05f * k * k;
                _squash = new Vector3(s, 1f + 0.03f * k, s);
            });

            float oldHeight = Height;
            NeonEffects.Shatter(pos, oldHeight, c);
            NeonEffects.Flash(pos, Color.Lerp(c, Color.white, 0.4f), 1.4f);
            FantasyFx.Burst(pos + Vector3.up * 0.4f, c, 50, 3.2f);
            _crackGlow = 0f;
            _offset = Vector3.zero;
            _flash = 0f;

            BuildRig(CharacterKind.Dragon);
            _rig.WingOpen = 0f;
            yield return Animate(0.9f, k =>
            {
                float e = EaseOutBack(k);
                float s = Mathf.Lerp(0.25f, 1f, e);
                _squash = new Vector3(s, s, s);
                _offset = Vector3.up * Mathf.Lerp(-0.6f, 0f, EaseOutCubic(k));
                _rig.WingOpen = Smoothstep(0.25f, 0.85f, k);
                _flyWeight = 1f - Smoothstep(0.6f, 1f, k);
                _flyPhase += Time.deltaTime * 13f;
                _flash = 3f * (1f - k);
            });
            _rig.WingOpen = 1f;
            NeonEffects.LandingPulse(pos, c);
            ResetMotion();
            _action = null;
            onDone?.Invoke();
        }

        // ------------------------------------------------------------------ frappes

        /// <summary>Durée conseillée de la frappe d'un type de personnage (s).</summary>
        public static float DefaultAttackDuration(CharacterKind attacker)
        {
            switch (CharacterKinds.StrikeOf(attacker))
            {
                case StrikeKind.Thrust: return 0.8f;
                case StrikeKind.LanceCharge: return 1.0f;
                case StrikeKind.ArcaneBolt: return 0.9f;
                case StrikeKind.Smash: return 1.1f;
                case StrikeKind.Whirl: return 1.0f;
                case StrikeKind.Cleave: return 1.3f;
                default: return 1.0f;
            }
        }

        /// <summary>Frappe propre au type du personnage (voir l'autre surcharge).</summary>
        public Coroutine PlayAttack(Vector3 target, float duration = -1f, Action onImpact = null)
        {
            return PlayAttack(Kind, target, duration, onImpact);
        }

        /// <summary>
        /// Joue la frappe du type <paramref name="attacker"/> (en général <see cref="Kind"/>) vers le point
        /// monde <paramref name="target"/> (pied de la victime) : le personnage s'oriente, s'élance jusqu'à
        /// portée (le mage reste sur place et lance un éclair), frappe, puis revient à sa position de départ.
        /// <paramref name="onImpact"/> est appelé à l'instant de l'impact (réaction ou mort de la victime).
        /// Frappes : estoc (sentinelle), charge à la lance (chevalier), éclair arcanique (mage), saut et
        /// écrasement (golem), tourbillon aérien (reine), coup à deux mains (roi), morsure plongeante (dragon).
        /// <paramref name="duration"/> ≤ 0 = <see cref="DefaultAttackDuration"/> (hors temps de rotation initial).
        /// </summary>
        public Coroutine PlayAttack(CharacterKind attacker, Vector3 target, float duration, Action onImpact)
        {
            StopAction();
            _selectTarget = 0f;
            if (duration <= 0f) duration = DefaultAttackDuration(attacker);
            _action = StartCoroutine(AttackRoutine(attacker, target, duration, onImpact));
            return _action;
        }

        static float Reach(CharacterKind kind)
        {
            switch (kind)
            {
                case CharacterKind.Sentinel: return 0.42f;
                case CharacterKind.Knight: return 0.36f;
                case CharacterKind.Mage: return 100f;
                case CharacterKind.Golem: return 0.5f;
                case CharacterKind.Queen: return 0.45f;
                case CharacterKind.King: return 0.55f;
                default: return 0.5f;
            }
        }

        IEnumerator AttackRoutine(CharacterKind attacker, Vector3 target, float duration, Action onImpact)
        {
            StrikeKind strike = CharacterKinds.StrikeOf(attacker);
            Color c = GlowColor;
            Vector3 start = transform.position;
            Vector3 flat = target - start;
            flat.y = 0f;
            float dist = flat.magnitude;
            Vector3 dir = dist > 1e-3f ? flat / dist : Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            _yawTarget = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            yield return TurnRoutine(0.25f);

            float impact = CharacterRig.ImpactTime(strike);
            float approach = Mathf.Max(0f, dist - Reach(attacker));
            float wind = impact * 0.65f;
            float[] lunge = strike == StrikeKind.LanceCharge
                ? new[] { 0f, 0f, wind, -0.12f, impact, 1f, impact + 0.1f, 1.06f, 1f, 0f }
                : new[] { 0f, 0f, wind, -0.06f, impact, 1f, impact + 0.15f, 1f, 1f, 0f };
            bool fired = false, bolt = false;
            _strikeKind = strike;
            _strikeWeight = 1f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                _strikeT = k;
                float l = StrikeTable.Eval(lunge, k);
                transform.position = start + dir * (approach * l);

                switch (strike)
                {
                    case StrikeKind.Smash:
                        _lift = 0.34f * Hump(k, wind * 0.8f, impact);
                        break;
                    case StrikeKind.Whirl:
                        _lift = 0.3f * Smoothstep(0f, 0.3f, k) * (1f - Smoothstep(0.7f, 1f, k));
                        _flyWeight = _lift / 0.3f * 0.6f;
                        _flyPhase += Time.deltaTime * 17f;
                        break;
                    case StrikeKind.Bite:
                        _lift = -0.3f * Mathf.Clamp01(l);
                        break;
                    case StrikeKind.LanceCharge:
                        _walkPhase += Time.deltaTime * 16f * Mathf.Clamp01(Mathf.Abs(l));
                        break;
                }

                if (strike == StrikeKind.ArcaneBolt && !bolt && k >= impact - 0.06f)
                {
                    bolt = true;
                    FantasyFx.ArcaneBolt(StrikeOrigin, target + Vector3.up * 0.35f, c, 0.32f);
                    _flash = 2.5f;
                }
                if (!fired && k >= impact)
                {
                    fired = true;
                    ImpactEffects(strike, target, c);
                    onImpact?.Invoke();
                    _flash = Mathf.Max(_flash, 1.2f);
                }
                _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 6f);
                yield return null;
            }
            if (!fired) onImpact?.Invoke();
            transform.position = start;
            _yawTarget = DefaultYaw;
            ResetMotion();
            _action = null;
        }

        /// <summary>Point monde d'où part la frappe (cristal du mage, gueule du dragon, sinon mi-hauteur).</summary>
        public Vector3 StrikeOrigin
        {
            get
            {
                if (_rig != null)
                {
                    Transform crystal = _rig.Bone(RigBone.Crystal);
                    if (crystal != null) return crystal.position;
                    Transform head = _rig.Bone(RigBone.Head);
                    if (head != null && Kind == CharacterKind.Dragon) return head.TransformPoint(new Vector3(0f, -0.02f, 0.16f));
                }
                return transform.position + Vector3.up * (Height * 0.6f);
            }
        }

        static void ImpactEffects(StrikeKind strike, Vector3 target, Color c)
        {
            Vector3 p = target + Vector3.up * 0.4f;
            switch (strike)
            {
                case StrikeKind.Thrust:
                    FantasyFx.Burst(p, c, 20, 2f);
                    NeonEffects.LandingPulse(target, c);
                    break;
                case StrikeKind.LanceCharge:
                    FantasyFx.Burst(p, c, 30, 3f);
                    NeonEffects.Flash(target, c, 0.8f);
                    break;
                case StrikeKind.ArcaneBolt:
                    FantasyFx.Burst(p, Color.Lerp(c, Color.white, 0.3f), 34, 2.4f);
                    NeonEffects.Flash(target, c, 0.8f);
                    break;
                case StrikeKind.Smash:
                    NeonEffects.Flash(target, c, 0.6f);
                    NeonEffects.LandingPulse(target, Color.white);
                    FantasyFx.Burst(target + Vector3.up * 0.1f, c, 30, 2.6f);
                    break;
                case StrikeKind.Whirl:
                    FantasyFx.Burst(p, c, 36, 2.6f);
                    NeonEffects.LandingPulse(target, c);
                    break;
                case StrikeKind.Cleave:
                    NeonEffects.Flash(target, c, 1f);
                    FantasyFx.Burst(p, c, 40, 3f);
                    break;
                default:
                    FantasyFx.Burst(p, c, 30, 1.8f);
                    NeonEffects.LandingPulse(target, c);
                    break;
            }
        }

        // ------------------------------------------------------------------ réaction, mort

        /// <summary>
        /// Réaction au coup : recul et inclinaison à l'opposé de <paramref name="from"/> (point monde),
        /// flash bref. Si le personnage est protégé, c'est le bouclier qui frémit et le recul est réduit.
        /// N'interrompt pas l'action en cours.
        /// </summary>
        public void PlayHitReaction(Vector3 from, float strength = 1f)
        {
            if (IsShielded)
            {
                PulseShield(1f);
                strength *= 0.3f;
            }
            StartCoroutine(HitRoutine(from, strength));
        }

        IEnumerator HitRoutine(Vector3 from, float strength)
        {
            Vector3 away = transform.position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.back;
            Vector3 axis = Vector3.Cross(Vector3.up, away);
            yield return Animate(0.42f, k =>
            {
                float e = Mathf.Sin(Mathf.Min(1f, k * 3f) * Mathf.PI * 0.5f) * (1f - k);
                _lean = Quaternion.AngleAxis(22f * strength * e, axis);
                _offset = away * (0.09f * strength * e);
                _flash = Mathf.Max(_flash, 2.2f * strength * (1f - k));
            });
            _lean = Quaternion.identity;
            _offset = Vector3.zero;
        }

        /// <summary>
        /// Mort : le personnage s'illumine, se brise en éclats de lumière, puis l'objet est détruit.
        /// <paramref name="onDone"/> est appelé juste avant la destruction.
        /// </summary>
        public void PlayDeath(Action onDone = null)
        {
            StopAction();
            _busy = true;
            _selectTarget = 0f;
            IsShielded = false;
            StartCoroutine(DeathRoutine(onDone));
        }

        IEnumerator DeathRoutine(Action onDone)
        {
            float h = Height;
            yield return Animate(0.1f, k =>
            {
                _flash = k * 4f;
                float s = 1f + 0.08f * k;
                _squash = new Vector3(s, s, s);
            });
            // Le dragon vole : ses éclats partent de son corps, pas du sol.
            bool dragon = Kind == CharacterKind.Dragon;
            NeonEffects.Shatter(transform.position + (dragon ? Vector3.up * 0.5f : Vector3.zero), dragon ? h - 0.5f : h, GlowColor);
            if (IsFrozen) FantasyFx.Frost(transform.position, h, 30);
            yield return Animate(0.17f, k =>
            {
                _flash = 4f + k * 4f;
                float xz = 1.08f + 0.35f * k;
                _squash = new Vector3(xz * (1f - k), 1.08f * (1f - k * k), xz * (1f - k));
            });
            _busy = false;
            onDone?.Invoke();
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ boucle

        void Update()
        {
            if (_rig == null) return;
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Calcule et applique une pose sans attendre Update (éditeur, captures, prévisualisation) :
        /// repos au temps <paramref name="time"/>, et éventuellement une frappe figée à <paramref name="strikeT"/>.
        /// </summary>
        public void PreviewPose(float time, StrikeKind? strike = null, float strikeT = 0f)
        {
            if (_rig == null) return;
            _animTime = time - _phase;
            // Pas de transition en prévisualisation : statuts et sélection pris à leur valeur finale.
            _selectWeight = _selectTarget;
            _freezeWeight = IsFrozen ? 1f : 0f;
            _shieldWeight = IsShielded ? 1f : 0f;
            _yaw = _yawTarget;
            if (strike.HasValue)
            {
                _strikeKind = strike.Value;
                _strikeT = strikeT;
                _strikeWeight = 1f;
            }
            Tick(0f);
            _strikeWeight = 0f;
        }

        void Tick(float dt)
        {
            if (!IsFrozen) _animTime += dt;
            _selectWeight = Mathf.MoveTowards(_selectWeight, _selectTarget, dt * 6f);
            float w = Smooth(_selectWeight);
            _yaw = Mathf.MoveTowardsAngle(_yaw, _yawTarget, dt * turnSpeed);
            _freezeWeight = Mathf.MoveTowards(_freezeWeight, IsFrozen ? 1f : 0f, dt * 3f);

            float lookTarget = 0f;
            if (_lookPoint.HasValue && !IsFrozen)
            {
                Vector3 d = _lookPoint.Value - transform.position;
                if (new Vector2(d.x, d.z).sqrMagnitude > 0.01f)
                    lookTarget = Mathf.Clamp(Mathf.DeltaAngle(_yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg), -40f, 40f);
            }
            _lookYaw = Mathf.Lerp(_lookYaw, lookTarget, 1f - Mathf.Exp(-6f * dt));

            _rig.BeginPose();
            _rig.Idle(_animTime + _phase);
            _rig.SetWalk(_walkPhase, _walkWeight);
            _rig.SetFly(_flyPhase, _flyWeight);
            _rig.SetStrike(_strikeKind, _strikeT, _strikeWeight);
            _rig.SetLook(_lookYaw);
            _rig.SetHover(w * (hoverHeight + hoverBob * Mathf.Sin(Time.time * 3.2f)) + _lift);
            _rig.Yaw = _yaw;
            _rig.SetModel(_offset, _lean, _squash);
            _rig.Apply();

            UpdateMaterials(w);
            UpdateStatus(dt);
        }

        void UpdateMaterials(float selectWeight)
        {
            if (_body == null) return;
            float emission = 1f + selectWeight * (0.7f + 0.5f * Mathf.Sin(Time.time * 5.5f)) + _flash;
            float f = Smooth(_freezeWeight);
            float key = emission + f * 17.3f;
            if (Mathf.Abs(key - _lastEmissionKey) > 0.002f)
            {
                _body.SetColor(NeonMaterials.BaseColorProperty, Color.Lerp(_bodyBase, IceBody, f * 0.85f));
                _body.SetColor(NeonMaterials.EmissionColorProperty, Color.Lerp(_bodyEmission, IceEmission, f) * (1f + (emission - 1f) * 2.5f));
                _accent.SetColor(NeonMaterials.EmissionColorProperty, Color.Lerp(_accentEmission, IceAccent, f * 0.8f) * emission);
                _lastEmissionKey = key;
            }
            if (_crackMat != null)
                _crackMat.SetColor(NeonMaterials.BaseColorProperty, Color.Lerp(GlowColor, Color.white, 0.55f) * _crackGlow);
        }

        void UpdateStatus(float dt)
        {
            if (_shield != null)
            {
                _shieldWeight = Mathf.MoveTowards(_shieldWeight, IsShielded ? 1f : 0f, dt * 3.5f);
                _shieldPulse = Mathf.MoveTowards(_shieldPulse, 0f, dt * 2.5f);
                bool visible = _shieldWeight > 0.001f;
                if (_shield.gameObject.activeSelf != visible) _shield.gameObject.SetActive(visible);
                if (visible)
                {
                    float s = EaseOutBack(_shieldWeight) * (1f + 0.04f * _shieldPulse * Mathf.Sin(Time.time * 40f));
                    bool dragon = Kind == CharacterKind.Dragon;
                    float r = dragon ? 0.62f : 0.47f;
                    _shield.localScale = new Vector3(r, Height + 0.12f, r) * s;
                    _shield.localRotation = Quaternion.Euler(0f, Time.time * 12f, 0f);
                    Color g = GlowColor;
                    float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.6f);
                    _shieldFill.SetColor(NeonMaterials.BaseColorProperty, g * ((0.10f + 0.06f * shimmer + 0.35f * _shieldPulse) * _shieldWeight));
                    _shieldEdge.SetColor(NeonMaterials.BaseColorProperty, Color.Lerp(g, Color.white, 0.2f) * ((1.1f + 0.4f * shimmer + 3f * _shieldPulse) * _shieldWeight));
                }
            }
            if (_ice != null)
            {
                bool visible = _freezeWeight > 0.001f;
                if (_ice.gameObject.activeSelf != visible) _ice.gameObject.SetActive(visible);
                if (visible)
                {
                    float g = Smooth(_freezeWeight);
                    bool dragon = Kind == CharacterKind.Dragon;
                    float r = dragon ? 0.55f : 0.42f;
                    float xz = r / 0.45f * (0.7f + 0.3f * g); // le maillage unitaire fait ≈ 0,45 de rayon
                    _ice.localScale = new Vector3(xz, Height * 1.04f * g, xz);
                }
            }
        }

        void OnDestroy()
        {
            ReleaseMaterials();
            DestroySafe(_shieldFill);
            DestroySafe(_shieldEdge);
            DestroySafe(_iceMat);
            DestroySafe(_crackMat);
        }

        // ------------------------------------------------------------------ utilitaires

        void EnsureHierarchy()
        {
            if (_model != null) return;
            transform.rotation = Quaternion.identity;
            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
        }

        void ReleaseMaterials()
        {
            DestroySafe(_body);
            DestroySafe(_accent);
            _body = _accent = null;
        }

        static void DestroySafe(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void StopAction()
        {
            if (_action != null)
            {
                StopCoroutine(_action);
                _action = null;
            }
            if (_model != null && !_model.gameObject.activeSelf) _model.gameObject.SetActive(true);
            if (_rig != null) _rig.WingOpen = 1f;
            transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
            _crackGlow = 0f;
            _yawTarget = DefaultYaw;
            ResetMotion();
        }

        void ResetMotion()
        {
            _squash = Vector3.one;
            _lean = Quaternion.identity;
            _offset = Vector3.zero;
            _lift = 0f;
            _flash = 0f;
            _walkWeight = 0f;
            _flyWeight = 0f;
            _strikeWeight = 0f;
            _strikeT = 0f;
            if (_rig != null)
            {
                Transform cracks = _rig.Bone(RigBone.Cracks);
                if (cracks != null && cracks.gameObject.activeSelf) cracks.gameObject.SetActive(false);
            }
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

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        static float Hump(float k, float a, float b) => k <= a || k >= b ? 0f : Mathf.Sin((k - a) / (b - a) * Mathf.PI);
        static float Smoothstep(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        static float Smooth(float x) => x * x * (3f - 2f * x);
        static float EaseInOutQuad(float x) => x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) * 0.5f;
        static float EaseInOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f;
        static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        static float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f); }
    }
}
