using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals.Fantasy
{
    /// <summary>Os (transformations animées) d'un personnage. Tous ne sont pas présents chez tous.</summary>
    public enum RigBone
    {
        /// <summary>Torse / corps principal (parties fixes fusionnées).</summary>
        Body,
        Head,
        ArmL,
        ArmR,
        /// <summary>Bras du roi fusionnés avec l'épée à deux mains.</summary>
        Arms,
        LegL,
        LegR,
        /// <summary>Paire de jambes avant du destrier.</summary>
        FrontLegs,
        /// <summary>Paire de jambes arrière du destrier.</summary>
        HindLegs,
        /// <summary>Cristal flottant du bâton du mage.</summary>
        Crystal,
        WingL,
        WingR,
        Neck1,
        Neck2,
        Tail,
        /// <summary>Fissures lumineuses du golem (cachées hors invocation).</summary>
        Cracks,
        Count
    }

    /// <summary>
    /// Petite hiérarchie de transformations animée par code (ni Animator ni SkinnedMesh).
    ///
    /// Chaque image : <see cref="BeginPose"/> remet les décalages à zéro, puis les couches
    /// s'additionnent (<see cref="Idle"/>, <see cref="SetWalk"/>, <see cref="SetFly"/>,
    /// <see cref="SetStrike"/>, <see cref="SetHover"/>, <see cref="SetModel"/>), enfin
    /// <see cref="Apply"/> écrit les transformations. Les décalages de rotation sont appliqués
    /// dans le repère local de chaque os, après sa rotation de repos.
    ///
    /// Conventions : le personnage regarde vers +Z local, +X est sa droite. Une rotation positive
    /// autour de X penche vers l'avant (un bras pendant part vers l'arrière, un bras tendu vers l'avant
    /// descend) ; un bras pendant levé vers l'avant = rotation négative autour de X.
    /// </summary>
    public sealed class CharacterRig
    {
        const int N = (int)RigBone.Count;

        readonly Transform[] _bones = new Transform[N];
        readonly Vector3[] _restPos = new Vector3[N];
        readonly Quaternion[] _restRot = new Quaternion[N];
        readonly Vector3[] _offPos = new Vector3[N];
        readonly Vector3[] _offEuler = new Vector3[N];
        readonly Vector3[] _scale = new Vector3[N];
        readonly List<Renderer> _renderers = new List<Renderer>(8);
        readonly float _baseScale;

        Vector3 _modelPos;
        Quaternion _modelRot = Quaternion.identity;
        Vector3 _modelScale = Vector3.one;
        float _hover;

        /// <summary>Personnage représenté.</summary>
        public CharacterKind Kind { get; }
        /// <summary>Racine animée (enfant de la vue) : orientation, lévitation, écrasement.</summary>
        public Transform Model { get; }
        /// <summary>Orientation (lacet, degrés) du personnage autour de Y.</summary>
        public float Yaw { get; set; }
        /// <summary>Ouverture des ailes : 0 = repliées, 1 = déployées (reine, dragon).</summary>
        public float WingOpen { get; set; } = 1f;
        /// <summary>Hauteur du personnage au repos (unités monde, une case = 1).</summary>
        public float Height { get; }
        /// <summary>Renderers du personnage (hors effets de statut de la vue).</summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;

        // ------------------------------------------------------------------ création

        CharacterRig(CharacterKind kind, Transform model, float baseScale, float height)
        {
            Kind = kind;
            Model = model;
            _baseScale = baseScale;
            Height = height;
            for (int i = 0; i < N; i++) _scale[i] = Vector3.one;
        }

        /// <summary>
        /// Instancie le personnage sous <paramref name="model"/> (les enfants existants sont détruits).
        /// <paramref name="materials"/> = { corps, accents } ; <paramref name="fxMaterial"/> sert aux fissures.
        /// </summary>
        public static CharacterRig Create(CharacterKind kind, Transform model, Material[] materials, Material fxMaterial)
        {
            for (int i = model.childCount - 1; i >= 0; i--)
                DestroySafe(model.GetChild(i).gameObject);

            CharacterBlueprint bp = FantasyPieceFactory.GetBlueprint(kind);
            var rig = new CharacterRig(kind, model, bp.Scale, bp.Height);
            foreach (BoneDef def in bp.Bones)
            {
                var go = new GameObject(def.Id.ToString());
                Transform parent = def.Parent.HasValue ? rig._bones[(int)def.Parent.Value] : model;
                go.transform.SetParent(parent != null ? parent : model, false);
                go.transform.localPosition = def.Position;
                go.transform.localRotation = def.Rotation;
                int id = (int)def.Id;
                rig._bones[id] = go.transform;
                rig._restPos[id] = def.Position;
                rig._restRot[id] = def.Rotation;
                if (def.Mesh == null) continue;

                go.AddComponent<MeshFilter>().sharedMesh = def.Mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                if (def.Fx)
                {
                    r.sharedMaterial = fxMaterial;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    go.SetActive(false);
                }
                else
                {
                    r.sharedMaterials = materials;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    rig._renderers.Add(r);
                }
            }
            rig.BeginPose();
            rig.Apply();
            return rig;
        }

        static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        // ------------------------------------------------------------------ accès

        /// <summary>Transformation d'un os, ou null s'il n'existe pas pour ce personnage.</summary>
        public Transform Bone(RigBone id) => _bones[(int)id];

        public bool Has(RigBone id) => _bones[(int)id] != null;

        // ------------------------------------------------------------------ couches de pose

        /// <summary>Remet tous les décalages à zéro (début d'image).</summary>
        public void BeginPose()
        {
            for (int i = 0; i < N; i++)
            {
                _offPos[i] = Vector3.zero;
                _offEuler[i] = Vector3.zero;
                _scale[i] = Vector3.one;
            }
            _modelPos = Vector3.zero;
            _modelRot = Quaternion.identity;
            _modelScale = Vector3.one;
            _hover = 0f;
        }

        void Rot(RigBone b, float x, float y, float z)
        {
            int i = (int)b;
            _offEuler[i] += new Vector3(x, y, z);
        }

        void Move(RigBone b, Vector3 d) => _offPos[(int)b] += d;

        /// <summary>
        /// Animation de repos (respiration, cristal, ailes, ondulation du dragon).
        /// <paramref name="time"/> en secondes ; désynchroniser en ajoutant une phase par personnage.
        /// </summary>
        public void Idle(float time)
        {
            float breath = Mathf.Sin(time * 1.7f);
            int body = (int)RigBone.Body;
            _scale[body] = new Vector3(1f + 0.008f * breath, 1f + 0.014f * breath, 1f + 0.008f * breath);
            float sway = Mathf.Sin(time * 1.7f + 0.8f);
            Rot(RigBone.ArmR, 2.5f * sway, 0f, 1.5f * sway);
            Rot(RigBone.ArmL, -2.5f * sway, 0f, -1.5f * sway);
            Rot(RigBone.Arms, 1.5f * sway, 0f, 0f);

            switch (Kind)
            {
                case CharacterKind.Mage:
                    Rot(RigBone.Crystal, 0f, time * 95f, 0f);
                    Move(RigBone.Crystal, new Vector3(0f, 0.014f * Mathf.Sin(time * 2.3f), 0f));
                    Rot(RigBone.Body, 0f, 0f, 1.2f * Mathf.Sin(time * 0.9f));
                    break;
                case CharacterKind.Queen:
                    float flutter = Mathf.Sin(time * 9f) * 4f + Mathf.Sin(time * 1.3f) * 3f;
                    Rot(RigBone.WingR, 0f, 0f, flutter);
                    Rot(RigBone.WingL, 0f, 0f, -flutter);
                    break;
                case CharacterKind.Knight:
                    Rot(RigBone.FrontLegs, 2f * Mathf.Max(0f, Mathf.Sin(time * 0.7f)), 0f, 0f);
                    break;
                case CharacterKind.Golem:
                    Rot(RigBone.Body, 0f, 0f, 0.8f * Mathf.Sin(time * 0.8f));
                    break;
                case CharacterKind.Dragon:
                    _modelPos.y += 0.045f * Mathf.Sin(time * 1.5f);
                    Rot(RigBone.Body, 3f * Mathf.Sin(time * 1.5f + 0.4f), 0f, 2f * Mathf.Sin(time * 0.75f));
                    Rot(RigBone.Neck1, 7f * Mathf.Sin(time * 1.5f + 1.0f), 4f * Mathf.Sin(time * 0.75f + 0.5f), 0f);
                    Rot(RigBone.Neck2, 7f * Mathf.Sin(time * 1.5f + 1.7f), 5f * Mathf.Sin(time * 0.75f + 1.2f), 0f);
                    Rot(RigBone.Head, -6f * Mathf.Sin(time * 1.5f + 2.3f), -6f * Mathf.Sin(time * 0.75f + 1.8f), 0f);
                    Rot(RigBone.Tail, 6f * Mathf.Sin(time * 1.5f + 2.6f), 14f * Mathf.Sin(time * 1.1f), 0f);
                    float flap = Mathf.Sin(time * 3.0f) * 16f;
                    Rot(RigBone.WingR, 0f, 0f, flap);
                    Rot(RigBone.WingL, 0f, 0f, -flap);
                    break;
            }
        }

        /// <summary>
        /// Marche / galop : <paramref name="phase"/> en radians (2π = un cycle de deux pas),
        /// <paramref name="weight"/> 0..1 pour fondre avec le repos. Sans effet notable pour la reine
        /// et le dragon (utiliser <see cref="SetFly"/>).
        /// </summary>
        public void SetWalk(float phase, float weight = 1f)
        {
            if (weight <= 0f) return;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float bob = Mathf.Abs(Mathf.Sin(phase));
            switch (Kind)
            {
                case CharacterKind.Knight:
                    Rot(RigBone.FrontLegs, 38f * s * weight, 0f, 0f);
                    Rot(RigBone.HindLegs, -34f * Mathf.Sin(phase + 2.2f) * weight, 0f, 0f);
                    Rot(RigBone.Body, -7f * c * weight, 0f, 0f);
                    _modelPos.y += 0.035f * bob * weight;
                    Rot(RigBone.ArmR, 4f * c * weight, 0f, 0f);
                    break;
                case CharacterKind.Golem:
                    Rot(RigBone.Body, 0f, 4f * s * weight, 8f * s * weight);
                    Rot(RigBone.ArmR, -22f * s * weight, 0f, 0f);
                    Rot(RigBone.ArmL, 22f * s * weight, 0f, 0f);
                    _modelPos.y += 0.035f * bob * weight;
                    break;
                case CharacterKind.Mage:
                    Rot(RigBone.Body, 3f * weight, 0f, 3.5f * s * weight);
                    Rot(RigBone.ArmR, -10f * s * weight, 0f, 0f);
                    _modelPos.y += 0.014f * bob * weight;
                    break;
                case CharacterKind.Queen:
                case CharacterKind.Dragon:
                    SetFly(phase * 2f, weight * 0.6f);
                    break;
                default:
                    Rot(RigBone.LegL, 30f * s * weight, 0f, 0f);
                    Rot(RigBone.LegR, -30f * s * weight, 0f, 0f);
                    Rot(RigBone.ArmL, -16f * s * weight, 0f, 0f);
                    Rot(RigBone.ArmR, 16f * s * weight, 0f, 0f);
                    Rot(RigBone.Arms, 5f * s * weight, 0f, 0f);
                    Rot(RigBone.Body, 4f * weight, 3f * s * weight, 2f * c * weight);
                    _modelPos.y += 0.022f * bob * weight;
                    break;
            }
        }

        /// <summary>Vol : battements d'ailes amples, corps penché vers l'avant (reine, dragon).</summary>
        public void SetFly(float phase, float weight = 1f)
        {
            if (weight <= 0f) return;
            float flap = Mathf.Sin(phase);
            Rot(RigBone.WingR, 0f, 0f, 34f * flap * weight);
            Rot(RigBone.WingL, 0f, 0f, -34f * flap * weight);
            Rot(RigBone.Body, 12f * weight, 0f, 0f);
            Rot(RigBone.ArmR, 18f * weight, 0f, 0f);
            Rot(RigBone.ArmL, 18f * weight, 0f, 0f);
            Rot(RigBone.Tail, -10f * weight + 8f * Mathf.Sin(phase + 1.5f) * weight, 0f, 0f);
            Rot(RigBone.Neck1, -6f * weight, 0f, 0f);
            _modelPos.y += 0.02f * Mathf.Sin(phase + 1.2f) * weight;
        }

        /// <summary>Lévitation (sélection, vol) : décalage vertical de la racine animée.</summary>
        public void SetHover(float height)
        {
            _hover += height;
        }

        /// <summary>Écrasement / inclinaison / décalage supplémentaires de la racine (rebond, recul).</summary>
        public void SetModel(Vector3 offset, Quaternion lean, Vector3 squash)
        {
            _modelPos += offset;
            _modelRot = lean * _modelRot;
            _modelScale = Vector3.Scale(_modelScale, squash);
        }

        /// <summary>Regard : tourne la tête (ou le torse si la tête est fusionnée) de <paramref name="yaw"/> degrés.</summary>
        public void SetLook(float yaw)
        {
            if (Has(RigBone.Head)) Rot(RigBone.Head, 0f, yaw, 0f);
            else Rot(RigBone.Body, 0f, yaw * 0.5f, 0f);
        }

        // ------------------------------------------------------------------ frappes

        /// <summary>Instant (0..1) de l'impact dans la frappe.</summary>
        public static float ImpactTime(StrikeKind kind) => StrikeTable.Get(kind).Impact;

        /// <summary>
        /// Pose d'attaque paramétrée : <paramref name="t"/> 0..1 parcourt anticipation → frappe
        /// (impact à <see cref="ImpactTime"/>) → retour. <paramref name="weight"/> fond la pose.
        /// </summary>
        public void SetStrike(StrikeKind kind, float t, float weight = 1f)
        {
            if (weight <= 0f) return;
            StrikeTable.Def def = StrikeTable.Get(kind);
            foreach (StrikeTable.Track tr in def.Tracks)
            {
                if (!Has(tr.Bone)) continue;
                float v = StrikeTable.Eval(tr.Keys, t) * weight;
                Vector3 e = tr.Axis == 0 ? new Vector3(v, 0f, 0f) : tr.Axis == 1 ? new Vector3(0f, v, 0f) : new Vector3(0f, 0f, v);
                _offEuler[(int)tr.Bone] += e;
            }
        }

        // ------------------------------------------------------------------ écriture

        /// <summary>Écrit la pose calculée dans les transformations.</summary>
        public void Apply()
        {
            // Ailes repliées : rabattues vers l'arrière et vers le bas.
            float fold = 1f - Mathf.Clamp01(WingOpen);
            if (fold > 0f)
            {
                _offEuler[(int)RigBone.WingR] += new Vector3(0f, 70f * fold, -40f * fold);
                _offEuler[(int)RigBone.WingL] += new Vector3(0f, -70f * fold, 40f * fold);
            }

            Model.localPosition = _modelPos + new Vector3(0f, _hover, 0f);
            Model.localRotation = _modelRot * Quaternion.Euler(0f, Yaw, 0f);
            Model.localScale = _modelScale * _baseScale;
            for (int i = 0; i < N; i++)
            {
                Transform t = _bones[i];
                if (t == null) continue;
                t.localPosition = _restPos[i] + _offPos[i];
                t.localRotation = _restRot[i] * Quaternion.Euler(_offEuler[i]);
                t.localScale = _scale[i];
            }
        }
    }

    /// <summary>Courbes clés des frappes (paires temps / angle en degrés, interpolation douce).</summary>
    internal static class StrikeTable
    {
        internal readonly struct Track
        {
            public readonly RigBone Bone;
            public readonly int Axis;
            public readonly float[] Keys;

            public Track(RigBone bone, int axis, params float[] keys)
            {
                Bone = bone;
                Axis = axis;
                Keys = keys;
            }
        }

        internal sealed class Def
        {
            public float Impact;
            public Track[] Tracks;
        }

        const int X = 0, Y = 1, Z = 2;
        static Def[] _defs;

        public static Def Get(StrikeKind kind)
        {
            if (_defs == null) Build();
            return _defs[(int)kind];
        }

        /// <summary>Interpolation douce (smoothstep) entre clés (t, valeur) triées.</summary>
        public static float Eval(float[] k, float t)
        {
            if (t <= k[0]) return k[1];
            for (int i = 2; i < k.Length; i += 2)
            {
                if (t <= k[i])
                {
                    float u = Mathf.InverseLerp(k[i - 2], k[i], t);
                    u = u * u * (3f - 2f * u);
                    return Mathf.Lerp(k[i - 1], k[i + 1], u);
                }
            }
            return k[k.Length - 1];
        }

        static void Build()
        {
            _defs = new Def[7];
            _defs[(int)StrikeKind.Thrust] = new Def
            {
                Impact = 0.5f,
                Tracks = new[]
                {
                    new Track(RigBone.ArmR, X, 0f, 0f, 0.35f, 40f, 0.5f, -50f, 0.66f, -50f, 1f, 0f),
                    new Track(RigBone.ArmL, X, 0f, 0f, 0.35f, -25f, 0.5f, -40f, 0.7f, -35f, 1f, 0f),
                    new Track(RigBone.Body, X, 0f, 0f, 0.35f, -8f, 0.5f, 16f, 0.66f, 12f, 1f, 0f),
                    new Track(RigBone.Body, Y, 0f, 0f, 0.35f, 18f, 0.5f, -12f, 0.7f, -8f, 1f, 0f),
                    new Track(RigBone.LegL, X, 0f, 0f, 0.35f, -10f, 0.5f, -32f, 0.7f, -28f, 1f, 0f),
                    new Track(RigBone.LegR, X, 0f, 0f, 0.35f, 10f, 0.5f, 26f, 0.7f, 22f, 1f, 0f),
                }
            };
            _defs[(int)StrikeKind.LanceCharge] = new Def
            {
                Impact = 0.55f,
                Tracks = new[]
                {
                    new Track(RigBone.Body, X, 0f, 0f, 0.28f, -20f, 0.42f, -18f, 0.55f, 10f, 0.72f, 6f, 1f, 0f),
                    new Track(RigBone.FrontLegs, X, 0f, 0f, 0.28f, -60f, 0.42f, -55f, 0.55f, 30f, 0.72f, 10f, 1f, 0f),
                    new Track(RigBone.HindLegs, X, 0f, 0f, 0.28f, 18f, 0.55f, -30f, 0.72f, -10f, 1f, 0f),
                    new Track(RigBone.ArmR, X, 0f, 0f, 0.28f, -18f, 0.42f, -10f, 0.55f, 14f, 0.72f, 12f, 1f, 0f),
                }
            };
            _defs[(int)StrikeKind.ArcaneBolt] = new Def
            {
                Impact = 0.5f,
                Tracks = new[]
                {
                    new Track(RigBone.ArmR, X, 0f, 0f, 0.4f, -60f, 0.5f, 50f, 0.7f, 45f, 1f, 0f),
                    new Track(RigBone.Body, X, 0f, 0f, 0.4f, -9f, 0.5f, 9f, 0.7f, 6f, 1f, 0f),
                    new Track(RigBone.Body, Y, 0f, 0f, 0.4f, 14f, 0.5f, -6f, 1f, 0f),
                }
            };
            _defs[(int)StrikeKind.Smash] = new Def
            {
                Impact = 0.58f,
                Tracks = new[]
                {
                    new Track(RigBone.ArmL, X, 0f, 0f, 0.45f, -165f, 0.58f, -40f, 0.76f, -40f, 1f, 0f),
                    new Track(RigBone.ArmR, X, 0f, 0f, 0.45f, -165f, 0.58f, -40f, 0.76f, -40f, 1f, 0f),
                    new Track(RigBone.Body, X, 0f, 0f, 0.45f, -10f, 0.58f, 18f, 0.76f, 12f, 1f, 0f),
                }
            };
            _defs[(int)StrikeKind.Whirl] = new Def
            {
                Impact = 0.6f,
                Tracks = new[]
                {
                    new Track(RigBone.Body, Y, 0f, 0f, 0.3f, -35f, 0.6f, 330f, 0.72f, 360f, 1f, 360f),
                    new Track(RigBone.ArmR, Z, 0f, 0f, 0.3f, 55f, 0.6f, 85f, 0.76f, 80f, 1f, 0f),
                    new Track(RigBone.ArmR, X, 0f, 0f, 0.3f, -20f, 0.6f, -10f, 1f, 0f),
                    new Track(RigBone.ArmL, Z, 0f, 0f, 0.3f, -45f, 0.6f, -70f, 0.8f, -60f, 1f, 0f),
                    new Track(RigBone.WingR, Z, 0f, 0f, 0.3f, 25f, 0.6f, -15f, 1f, 0f),
                    new Track(RigBone.WingL, Z, 0f, 0f, 0.3f, -25f, 0.6f, 15f, 1f, 0f),
                }
            };
            _defs[(int)StrikeKind.Cleave] = new Def
            {
                Impact = 0.65f,
                Tracks = new[]
                {
                    new Track(RigBone.Arms, X, 0f, 0f, 0.5f, -150f, 0.65f, 25f, 0.82f, 25f, 1f, 0f),
                    new Track(RigBone.Body, X, 0f, 0f, 0.5f, -10f, 0.65f, 16f, 0.82f, 12f, 1f, 0f),
                    new Track(RigBone.LegL, X, 0f, 0f, 0.5f, -8f, 0.65f, -24f, 0.82f, -20f, 1f, 0f),
                    new Track(RigBone.LegR, X, 0f, 0f, 0.5f, 6f, 0.65f, 16f, 0.82f, 14f, 1f, 0f),
                }
            };
            _defs[(int)StrikeKind.Bite] = new Def
            {
                Impact = 0.5f,
                Tracks = new[]
                {
                    new Track(RigBone.Neck1, X, 0f, 0f, 0.35f, -28f, 0.5f, 42f, 0.64f, 42f, 1f, 0f),
                    new Track(RigBone.Neck2, X, 0f, 0f, 0.35f, -20f, 0.5f, 22f, 0.64f, 22f, 1f, 0f),
                    new Track(RigBone.Head, X, 0f, 0f, 0.35f, -25f, 0.5f, 12f, 0.64f, 12f, 1f, 0f),
                    new Track(RigBone.Body, X, 0f, 0f, 0.35f, -10f, 0.5f, 16f, 0.64f, 14f, 1f, 0f),
                    new Track(RigBone.WingR, Z, 0f, 0f, 0.35f, 40f, 0.5f, -25f, 1f, 0f),
                    new Track(RigBone.WingL, Z, 0f, 0f, 0.35f, -40f, 0.5f, 25f, 1f, 0f),
                    new Track(RigBone.Tail, X, 0f, 0f, 0.35f, 15f, 0.5f, -20f, 1f, 0f),
                }
            };
        }
    }
}
