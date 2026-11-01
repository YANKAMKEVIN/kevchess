using System.Collections.Generic;
using UnityEngine;

namespace KevChess.Game.Visuals.Fantasy
{
    /// <summary>Description d'un os : parent, pose de repos et maillage éventuel.</summary>
    internal sealed class BoneDef
    {
        public RigBone Id;
        public RigBone? Parent;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public Mesh Mesh;
        /// <summary>Maillage d'effet (matériau additif, caché par défaut).</summary>
        public bool Fx;
    }

    /// <summary>Plan d'un personnage : os (ordre parent avant enfant), échelle globale, hauteur.</summary>
    internal sealed class CharacterBlueprint
    {
        public CharacterKind Kind;
        public float Scale = 1f;
        public float Height;
        public int Triangles;
        public readonly List<BoneDef> Bones = new List<BoneDef>();

        public BoneDef Add(RigBone id, RigBone? parent, Vector3 position, Quaternion rotation, ShapeBuilder shape, bool fx = false)
        {
            var def = new BoneDef { Id = id, Parent = parent, Position = position, Rotation = rotation, Fx = fx };
            if (shape != null)
            {
                def.Mesh = shape.ToMesh("KevChess_" + Kind + "_" + id);
                if (!fx) Triangles += shape.TriangleCount;
            }
            Bones.Add(def);
            return def;
        }

        public BoneDef Add(RigBone id, RigBone? parent, Vector3 position, ShapeBuilder shape) =>
            Add(id, parent, position, Quaternion.identity, shape);
    }

    /// <summary>
    /// Construit par code les sept personnages du mode Légendes, en style cristal taillé
    /// (facettes plates, sous-maillage 0 = verre, 1 = accents néon, comme <see cref="PieceMeshFactory"/>).
    /// Les parties fixes de chaque personnage sont fusionnées en un seul maillage ; seuls les membres
    /// animés sont séparés (3 à 5 renderers, 7 pour le dragon). Les maillages sont partagés et mis en cache
    /// par type. Repère : pivot au sol au centre de la case, le personnage regarde vers +Z.
    /// </summary>
    public static class FantasyPieceFactory
    {
        const int Body = ShapeBuilder.Body;
        const int Accent = ShapeBuilder.Accent;

        static readonly Dictionary<CharacterKind, CharacterBlueprint> Cache = new Dictionary<CharacterKind, CharacterBlueprint>();
        static Mesh _shield, _ice;

        /// <summary>Hauteur du personnage au repos (une case = 1). Le roi est le plus grand, le dragon ≈ 1,4 × le roi.</summary>
        public static float Height(CharacterKind kind)
        {
            switch (kind)
            {
                case CharacterKind.Sentinel: return 0.66f;
                case CharacterKind.Knight: return 0.88f;
                case CharacterKind.Mage: return 0.95f;
                case CharacterKind.Golem: return 0.82f;
                case CharacterKind.Queen: return 1.07f;
                case CharacterKind.King: return 1.15f;
                default: return 1.6f;
            }
        }

        /// <summary>Nombre de triangles des maillages visibles d'un personnage (contrôle de budget).</summary>
        public static int TriangleCount(CharacterKind kind) => GetBlueprint(kind).Triangles;

        /// <summary>Vide le cache (les objets existants gardent leurs maillages).</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            _shield = _ice = null;
        }

        internal static CharacterBlueprint GetBlueprint(CharacterKind kind)
        {
            if (Cache.TryGetValue(kind, out CharacterBlueprint bp) && bp.Bones.Count > 0 && bp.Bones[0].Mesh != null)
                return bp;
            bp = new CharacterBlueprint { Kind = kind, Height = Height(kind) };
            switch (kind)
            {
                case CharacterKind.Sentinel: BuildSentinel(bp); break;
                case CharacterKind.Knight: BuildKnight(bp); break;
                case CharacterKind.Mage: BuildMage(bp); break;
                case CharacterKind.Golem: BuildGolem(bp); break;
                case CharacterKind.Queen: BuildQueen(bp); break;
                case CharacterKind.King: BuildKing(bp); break;
                default: BuildDragon(bp); break;
            }
            Cache[kind] = bp;
            return bp;
        }

        static ProfilePoint P(float r, float y, float twist = 0f, int sub = Body) => new ProfilePoint(r, y, twist, sub);

        static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        // ================================================================== Sentinelle (pion)

        static void BuildSentinel(CharacterBlueprint bp)
        {
            bp.Scale = 1f;
            const float hip = 0.17f;

            var body = new ShapeBuilder();
            // Torse trapu avec ceinture lumineuse.
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, -0.01f), P(0.10f, -0.01f), P(0.118f, 0.03f), P(0.128f, 0.05f, 0f, Accent), P(0.128f, 0.072f),
                P(0.145f, 0.13f, 0.5f), P(0.15f, 0.19f), P(0.122f, 0.235f, 0.5f), P(0.06f, 0.255f), P(0f, 0.26f),
            }, 8);
            // Épaulières.
            body.Ellipsoid(Body, new Vector3(0.13f, 0.215f, 0f), new Vector3(0.062f, 0.045f, 0.062f), 6, 3);
            body.Ellipsoid(Body, new Vector3(-0.13f, 0.215f, 0f), new Vector3(0.062f, 0.045f, 0.062f), 6, 3);
            // Casque arrondi.
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.072f, 0f), P(0.097f, 0.03f), P(0.107f, 0.08f, 0.5f), P(0.102f, 0.12f),
                P(0.078f, 0.16f, 0.5f), P(0.036f, 0.18f), P(0f, 0.186f),
            }, 8, new Vector3(0f, 0.25f, 0f), Quaternion.identity, Vector3.one);
            // Visière lumineuse.
            body.Box(Accent, new Vector3(0f, 0.335f, 0.097f), new Vector3(0.12f, 0.024f, 0.03f));
            // Crête (lisible de dessus : une ligne lumineuse dans l'axe du regard).
            body.ExtrudeSagittal(Accent, new[]
            {
                new Vector2(-0.10f, 0.37f), new Vector2(-0.13f, 0.44f), new Vector2(-0.075f, 0.49f),
                new Vector2(0.02f, 0.495f), new Vector2(0.075f, 0.455f), new Vector2(0.035f, 0.425f), new Vector2(-0.02f, 0.43f),
            }, 0.02f, Vector3.zero);
            bp.Add(RigBone.Body, null, new Vector3(0f, hip, 0f), body);

            // Jambes.
            var leg = new ShapeBuilder();
            leg.Limb(Body, Vector3.zero, new Vector3(0f, -0.13f, 0f), 0.048f, 0.04f, 6);
            leg.Box(Body, new Vector3(0f, -0.148f, 0.018f), new Vector3(0.078f, 0.045f, 0.105f));
            leg.Gem(Accent, new Vector3(0f, -0.07f, 0.042f), 0.016f, 0.02f, 4);
            bp.Add(RigBone.LegR, null, new Vector3(0.062f, hip, 0f), leg);
            bp.Add(RigBone.LegL, null, new Vector3(-0.062f, hip, 0f), leg);

            // Bras droit + épée courte.
            var armR = new ShapeBuilder();
            armR.Limb(Body, Vector3.zero, new Vector3(0f, -0.13f, 0f), 0.04f, 0.035f, 6);
            Vector3 hand = new Vector3(0f, -0.145f, 0f);
            armR.Ellipsoid(Body, hand, new Vector3(0.036f, 0.036f, 0.036f), 6, 3);
            Vector3 d = new Vector3(0f, -0.55f, 0.83f).normalized;
            armR.Box(Body, hand + d * 0.035f, new Vector3(0.10f, 0.02f, 0.022f), Quaternion.LookRotation(d));
            armR.Blade(Accent, hand + d * 0.04f, d, 0.25f, 0.05f, Vector3.right, 0.3f);
            armR.Gem(Accent, hand - d * 0.04f, 0.018f, 0.018f, 4);
            bp.Add(RigBone.ArmR, RigBone.Body, new Vector3(0.155f, 0.205f, 0f), Quaternion.Euler(-20f, 0f, 0f), armR);

            // Bras gauche + bouclier rond (bord lumineux, umbo).
            var armL = new ShapeBuilder();
            armL.Limb(Body, Vector3.zero, new Vector3(0f, -0.12f, 0f), 0.04f, 0.035f, 6);
            Vector3 n = new Vector3(-0.45f, 0.15f, 0.88f).normalized;
            armL.Lathe(new List<ProfilePoint>
            {
                P(0f, -0.012f), P(0.118f, -0.012f, 0f, Accent), P(0.118f, 0.014f), P(0.075f, 0.03f, 0.5f), P(0f, 0.036f),
            }, 10, new Vector3(-0.02f, -0.10f, 0.055f), Quaternion.FromToRotation(Vector3.up, n), Vector3.one);
            armL.Gem(Accent, new Vector3(-0.02f, -0.10f, 0.055f) + n * 0.04f, 0.026f, 0.02f, 4);
            bp.Add(RigBone.ArmL, RigBone.Body, new Vector3(-0.155f, 0.205f, 0f), Quaternion.Euler(-25f, 0f, 0f), armL);
        }

        // ================================================================== Chevalier monté (cavalier)

        static void BuildKnight(CharacterBlueprint bp)
        {
            bp.Scale = 1.15f;
            var body = new ShapeBuilder();
            // Tronc du destrier, allongé selon Z.
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, -0.21f), P(0.06f, -0.21f), P(0.095f, -0.15f, 0.5f), P(0.105f, -0.05f), P(0.11f, 0.05f, 0.5f),
                P(0.10f, 0.14f), P(0.07f, 0.20f, 0.5f), P(0f, 0.22f),
            }, 8, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.85f, 1f, 1f));
            // Tapis de selle lumineux (bande sur le dos).
            body.Box(Accent, new Vector3(0f, 0.095f, -0.03f), new Vector3(0.20f, 0.02f, 0.13f), Quaternion.Euler(-4f, 0f, 0f));
            // Encolure et tête.
            body.Limb(Body, new Vector3(0f, 0.04f, 0.15f), new Vector3(0f, 0.23f, 0.25f), 0.068f, 0.05f, 6);
            body.Limb(Body, new Vector3(0f, 0.25f, 0.235f), new Vector3(0f, 0.17f, 0.37f), 0.056f, 0.034f, 6);
            body.Spike(Body, new Vector3(0.025f, 0.27f, 0.22f), new Vector3(0.035f, 0.33f, 0.2f), 0.014f);
            body.Spike(Body, new Vector3(-0.025f, 0.27f, 0.22f), new Vector3(-0.035f, 0.33f, 0.2f), 0.014f);
            body.Gem(Accent, new Vector3(0.046f, 0.235f, 0.29f), 0.013f, 0.012f, 4);
            body.Gem(Accent, new Vector3(-0.046f, 0.235f, 0.29f), 0.013f, 0.012f, 4);
            // Crinière lumineuse.
            body.ExtrudeSagittal(Accent, new[]
            {
                new Vector2(0.10f, 0.11f), new Vector2(0.14f, 0.20f), new Vector2(0.19f, 0.28f), new Vector2(0.235f, 0.315f),
                new Vector2(0.215f, 0.26f), new Vector2(0.17f, 0.17f), new Vector2(0.13f, 0.09f),
            }, 0.016f, Vector3.zero);
            // Queue lumineuse.
            body.Limb(Accent, new Vector3(0f, 0.05f, -0.19f), new Vector3(0f, -0.13f, -0.31f), 0.03f, 0.008f, 4);

            // Cavalier : jambes, torse, casque à panache, bras gauche aux rênes.
            body.Limb(Body, new Vector3(0.065f, 0.12f, -0.03f), new Vector3(0.10f, -0.02f, 0.04f), 0.032f, 0.026f, 5);
            body.Limb(Body, new Vector3(-0.065f, 0.12f, -0.03f), new Vector3(-0.10f, -0.02f, 0.04f), 0.032f, 0.026f, 5);
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.07f, 0f), P(0.08f, 0.035f, 0f, Accent), P(0.082f, 0.055f), P(0.09f, 0.12f, 0.5f),
                P(0.074f, 0.16f), P(0.04f, 0.175f, 0.5f), P(0f, 0.18f),
            }, 8, new Vector3(0f, 0.10f, -0.03f), Quaternion.identity, Vector3.one);
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.05f, 0f), P(0.066f, 0.03f), P(0.066f, 0.07f, 0.5f), P(0.045f, 0.10f), P(0f, 0.11f),
            }, 8, new Vector3(0f, 0.275f, -0.03f), Quaternion.identity, Vector3.one);
            body.Box(Accent, new Vector3(0f, 0.33f, 0.032f), new Vector3(0.08f, 0.016f, 0.02f));
            body.ExtrudeSagittal(Accent, new[]
            {
                new Vector2(-0.02f, 0.37f), new Vector2(0.02f, 0.40f), new Vector2(-0.04f, 0.45f),
                new Vector2(-0.15f, 0.43f), new Vector2(-0.10f, 0.395f),
            }, 0.013f, Vector3.zero);
            body.Limb(Body, new Vector3(-0.09f, 0.24f, -0.03f), new Vector3(-0.06f, 0.15f, 0.08f), 0.028f, 0.024f, 5);
            bp.Add(RigBone.Body, null, new Vector3(0f, 0.30f, 0f), body);

            // Jambes avant (paire, pivot à l'épaule) et arrière (jarret).
            var front = new ShapeBuilder();
            var hind = new ShapeBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                float x = 0.05f * s;
                front.Limb(Body, new Vector3(x, 0.02f, 0f), new Vector3(x, -0.15f, 0.015f), 0.034f, 0.026f, 5);
                front.Limb(Body, new Vector3(x, -0.15f, 0.015f), new Vector3(x, -0.265f, 0.02f), 0.026f, 0.022f, 5);
                front.Box(Accent, new Vector3(x, -0.272f, 0.03f), new Vector3(0.045f, 0.02f, 0.055f));
                hind.Limb(Body, new Vector3(x, 0.02f, 0f), new Vector3(x, -0.13f, -0.045f), 0.042f, 0.03f, 5);
                hind.Limb(Body, new Vector3(x, -0.13f, -0.045f), new Vector3(x, -0.265f, 0f), 0.028f, 0.022f, 5);
                hind.Box(Accent, new Vector3(x, -0.272f, 0.01f), new Vector3(0.045f, 0.02f, 0.055f));
            }
            bp.Add(RigBone.FrontLegs, RigBone.Body, new Vector3(0f, -0.02f, 0.13f), front);
            bp.Add(RigBone.HindLegs, RigBone.Body, new Vector3(0f, -0.02f, -0.14f), hind);

            // Bras droit + lance couchée.
            var arm = new ShapeBuilder();
            arm.Limb(Body, Vector3.zero, new Vector3(0f, -0.09f, 0.05f), 0.03f, 0.026f, 5);
            Vector3 hand = new Vector3(0f, -0.10f, 0.06f);
            arm.Ellipsoid(Body, hand, new Vector3(0.03f, 0.03f, 0.03f), 6, 3);
            Vector3 dir = new Vector3(0f, 0.06f, 1f).normalized;
            arm.Limb(Body, hand - dir * 0.13f, hand + dir * 0.34f, 0.018f, 0.007f, 5);
            arm.Lathe(new List<ProfilePoint> { P(0f, 0f, 0f, Accent), P(0.045f, 0.01f, 0f, Accent), P(0.018f, 0.07f, 0f, Accent), P(0f, 0.07f, 0f, Accent) },
                6, hand + dir * 0.015f, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one);
            arm.GemAlong(Accent, hand + dir * 0.37f, dir, 0.018f, 0.04f, 4);
            bp.Add(RigBone.ArmR, RigBone.Body, new Vector3(0.09f, 0.24f, -0.03f), arm);
        }

        // ================================================================== Mage (fou)

        static void BuildMage(CharacterBlueprint bp)
        {
            bp.Scale = 1.05f;
            var body = new ShapeBuilder();
            // Robe conique à ourlet lumineux.
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.21f, 0f), P(0.215f, 0.022f, 0f, Accent), P(0.212f, 0.045f), P(0.17f, 0.15f, 0.5f),
                P(0.135f, 0.27f), P(0.137f, 0.285f, 0f, Accent), P(0.133f, 0.30f), P(0.11f, 0.40f, 0.5f), P(0.125f, 0.47f),
                P(0.10f, 0.52f, 0.5f), P(0.05f, 0.55f), P(0f, 0.555f),
            }, 10);
            // Capuche : base arrondie puis pointe rejetée en arrière.
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.09f, 0f), P(0.115f, 0.06f, 0.5f), P(0.112f, 0.12f), P(0.085f, 0.17f, 0.5f), P(0f, 0.18f),
            }, 8, new Vector3(0f, 0.50f, 0f), Quaternion.identity, Vector3.one);
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.08f, 0f), P(0.05f, 0.10f, 0.5f), P(0.02f, 0.19f), P(0f, 0.24f),
            }, 6, new Vector3(0f, 0.655f, -0.012f), Quaternion.Euler(-22f, 0f, 0f), Vector3.one);
            // Visage dans l'ombre : deux yeux lumineux, rune sur la poitrine.
            body.Box(Body, new Vector3(0f, 0.59f, 0.085f), new Vector3(0.11f, 0.08f, 0.04f));
            body.Gem(Accent, new Vector3(0.033f, 0.6f, 0.107f), 0.014f, 0.011f, 4);
            body.Gem(Accent, new Vector3(-0.033f, 0.6f, 0.107f), 0.014f, 0.011f, 4);
            body.GemAlong(Accent, new Vector3(0f, 0.41f, 0.112f), Vector3.up, 0.03f, 0.045f, 4);
            // Bras gauche fusionné (manche évasée, main qui irradie).
            body.Limb(Body, new Vector3(-0.12f, 0.46f, 0f), new Vector3(-0.10f, 0.30f, 0.09f), 0.045f, 0.058f, 6);
            body.Gem(Accent, new Vector3(-0.10f, 0.28f, 0.11f), 0.028f, 0.028f, 4);
            bp.Add(RigBone.Body, null, Vector3.zero, body);

            // Bras droit + bâton (le cristal est un os séparé qui tourne).
            var arm = new ShapeBuilder();
            arm.Limb(Body, Vector3.zero, new Vector3(0.01f, -0.15f, 0.07f), 0.045f, 0.058f, 6);
            Vector3 hand = new Vector3(0.01f, -0.17f, 0.08f);
            arm.Ellipsoid(Body, hand, new Vector3(0.03f, 0.03f, 0.03f), 6, 3);
            arm.Limb(Body, hand + new Vector3(0f, -0.28f, 0f), hand + new Vector3(0f, 0.46f, 0f), 0.014f, 0.016f, 5);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                arm.Spike(Accent, hand + new Vector3(0f, 0.45f, 0f) + o * 0.01f, hand + new Vector3(0f, 0.53f, 0f) + o * 0.045f, 0.008f, 3);
            }
            bp.Add(RigBone.ArmR, RigBone.Body, new Vector3(0.125f, 0.45f, 0f), arm);

            var crystal = new ShapeBuilder();
            crystal.Gem(Accent, Vector3.zero, 0.042f, 0.075f, 6);
            bp.Add(RigBone.Crystal, RigBone.ArmR, hand + new Vector3(0f, 0.585f, 0f), crystal);
        }

        // ================================================================== Golem-tour (tour)

        static void BuildGolem(CharacterBlueprint bp)
        {
            bp.Scale = 1f;
            var body = new ShapeBuilder();
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.25f, 0f), P(0.25f, 0.03f, 0f, Accent), P(0.25f, 0.06f), P(0.215f, 0.10f, 0.5f),
                P(0.20f, 0.25f), P(0.195f, 0.42f, 0.5f), P(0.215f, 0.52f), P(0.25f, 0.56f), P(0.255f, 0.58f, 0f, Accent),
                P(0.255f, 0.60f), P(0.255f, 0.68f), P(0.18f, 0.68f), P(0.18f, 0.64f, 0f, Accent), P(0f, 0.64f),
            }, 12);
            // Créneaux.
            const int merlons = 6;
            float sector = Mathf.PI * 2f / merlons;
            float half = sector * 0.30f;
            for (int i = 0; i < merlons; i++)
            {
                float a = i * sector + sector * 0.5f;
                float a0 = a - half, a1 = a + half, t0 = a - half * 0.82f, t1 = a + half * 0.82f;
                body.Hexahedron(Body, new[]
                {
                    Polar(0.18f, a0, 0.679f), Polar(0.26f, a0, 0.679f), Polar(0.26f, a1, 0.679f), Polar(0.18f, a1, 0.679f),
                    Polar(0.19f, t0, 0.80f), Polar(0.25f, t0, 0.80f), Polar(0.25f, t1, 0.80f), Polar(0.19f, t1, 0.80f),
                });
            }
            // Visage : arcade, deux yeux luisants, bouche fendue.
            body.Box(Body, new Vector3(0f, 0.53f, 0.205f), new Vector3(0.24f, 0.04f, 0.06f), Quaternion.Euler(-8f, 0f, 0f));
            body.GemAlong(Accent, new Vector3(0.075f, 0.48f, 0.198f), Vector3.forward, 0.034f, 0.026f, 6);
            body.GemAlong(Accent, new Vector3(-0.075f, 0.48f, 0.198f), Vector3.forward, 0.034f, 0.026f, 6);
            body.Box(Accent, new Vector3(0f, 0.37f, 0.198f), new Vector3(0.11f, 0.016f, 0.02f));
            // Quelques pierres en saillie.
            body.Box(Body, Polar(0.205f, 2.4f, 0.22f), new Vector3(0.08f, 0.05f, 0.05f), Quaternion.Euler(0f, -2.4f * Mathf.Rad2Deg + 90f, 0f));
            body.Box(Body, Polar(0.2f, 4.0f, 0.33f), new Vector3(0.09f, 0.05f, 0.05f), Quaternion.Euler(0f, -4.0f * Mathf.Rad2Deg + 90f, 0f));
            body.Box(Body, Polar(0.205f, 5.3f, 0.16f), new Vector3(0.07f, 0.05f, 0.05f), Quaternion.Euler(0f, -5.3f * Mathf.Rad2Deg + 90f, 0f));
            bp.Add(RigBone.Body, null, Vector3.zero, body);

            // Bras de pierre (le gauche est le miroir du droit).
            ShapeBuilder ArmShape(bool left)
            {
                var a = new ShapeBuilder();
                if (left) a.PushMirror();
                a.Ellipsoid(Body, Vector3.zero, new Vector3(0.08f, 0.07f, 0.08f), 6, 3);
                a.Box(Body, new Vector3(0.03f, -0.07f, 0f), new Vector3(0.10f, 0.13f, 0.10f), Quaternion.Euler(0f, 0f, 6f));
                a.Box(Body, new Vector3(0.045f, -0.20f, 0.02f), new Vector3(0.11f, 0.14f, 0.11f), Quaternion.Euler(-8f, 0f, 0f));
                a.Box(Body, new Vector3(0.05f, -0.335f, 0.035f), new Vector3(0.155f, 0.13f, 0.155f), Quaternion.Euler(0f, 0f, 0f));
                a.Box(Accent, new Vector3(0.05f, -0.32f, 0.114f), new Vector3(0.125f, 0.026f, 0.012f));
                a.Box(Accent, new Vector3(0.05f, -0.28f, 0.035f), new Vector3(0.162f, 0.014f, 0.162f));
                if (left) a.Pop();
                return a;
            }
            bp.Add(RigBone.ArmR, RigBone.Body, new Vector3(0.255f, 0.47f, 0f), ArmShape(false));
            bp.Add(RigBone.ArmL, RigBone.Body, new Vector3(-0.255f, 0.47f, 0f), ArmShape(true));

            // Fissures lumineuses (invocation du dragon), cachées par défaut.
            bp.Add(RigBone.Cracks, RigBone.Body, Vector3.zero, Quaternion.identity, BuildCracks(), true);
        }

        static Vector3 Polar(float r, float angle, float y) => new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);

        /// <summary>Craquelures en zigzag sur le fût de la tour (rubans fins légèrement en saillie).</summary>
        static ShapeBuilder BuildCracks()
        {
            var b = new ShapeBuilder(1);
            var rng = new System.Random(1717);
            float Rand(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            for (int k = 0; k < 9; k++)
            {
                float ang = k * Mathf.PI * 2f / 9f + Rand(-0.2f, 0.2f);
                float y = Rand(0.12f, 0.5f);
                int dirY = rng.Next(2) == 0 ? 1 : -1;
                for (int s = 0; s < 5; s++)
                {
                    float ang2 = ang + Rand(-0.28f, 0.28f);
                    float y2 = Mathf.Clamp(y + dirY * Rand(0.04f, 0.1f), 0.07f, 0.6f);
                    float w = 0.012f * (1f - s * 0.15f);
                    Vector3 p0 = Polar(0.214f, ang, y), p1 = Polar(0.214f, ang2, y2);
                    Vector3 outward = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 side = Vector3.Cross(p1 - p0, outward).normalized * w;
                    b.QuadDouble(0, p0 - side, p1 - side, p1 + side, p0 + side);
                    ang = ang2;
                    y = y2;
                }
            }
            return b;
        }

        // ================================================================== Reine guerrière (dame)

        static void BuildQueen(CharacterBlueprint bp)
        {
            bp.Scale = 1.2f;
            var body = new ShapeBuilder();
            // Jupe d'armure, taille fine, buste.
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.19f, 0f), P(0.195f, 0.022f, 0f, Accent), P(0.19f, 0.045f), P(0.15f, 0.17f, 0.5f),
                P(0.11f, 0.32f), P(0.085f, 0.42f, 0.5f), P(0.092f, 0.435f, 0f, Accent), P(0.092f, 0.455f), P(0.11f, 0.54f, 0.5f),
                P(0.115f, 0.60f), P(0.08f, 0.645f, 0.5f), P(0.035f, 0.66f), P(0f, 0.665f),
            }, 12);
            // Épaulières fines à gemme.
            for (int s = -1; s <= 1; s += 2)
            {
                body.Ellipsoid(Body, new Vector3(0.1f * s, 0.62f, 0f), new Vector3(0.05f, 0.032f, 0.05f), 6, 3);
                body.Gem(Accent, new Vector3(0.105f * s, 0.655f, 0f), 0.015f, 0.015f, 4);
            }
            body.Limb(Body, new Vector3(0f, 0.64f, 0f), new Vector3(0f, 0.70f, 0f), 0.03f, 0.028f, 6);
            body.Ellipsoid(Body, new Vector3(0f, 0.75f, 0.005f), new Vector3(0.055f, 0.066f, 0.06f), 8, 4);
            // Chevelure tombant dans le dos.
            body.Limb(Body, new Vector3(0f, 0.78f, -0.03f), new Vector3(0f, 0.6f, -0.075f), 0.05f, 0.03f, 6);
            body.Gem(Accent, new Vector3(0.022f, 0.755f, 0.058f), 0.009f, 0.008f, 4);
            body.Gem(Accent, new Vector3(-0.022f, 0.755f, 0.058f), 0.009f, 0.008f, 4);
            // Diadème à pointes.
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI * 0.5f + (i - 2) * 0.55f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.058f, 0.80f, Mathf.Sin(a) * 0.058f);
                float h = i == 2 ? 0.05f : 0.03f;
                body.Spike(Accent, p, p + new Vector3(0f, h, 0f) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.01f, 0.012f, 4);
            }
            body.Lathe(new List<ProfilePoint> { P(0.06f, 0.79f, 0f, Accent), P(0.06f, 0.805f, 0f, Accent) }, 10, Vector3.zero, Quaternion.identity, Vector3.one);
            bp.Add(RigBone.Body, null, Vector3.zero, body);

            // Bras droit + épée longue lumineuse.
            var armR = new ShapeBuilder();
            armR.Limb(Body, Vector3.zero, new Vector3(0f, -0.20f, 0.02f), 0.03f, 0.025f, 6);
            Vector3 hand = new Vector3(0f, -0.215f, 0.025f);
            armR.Ellipsoid(Body, hand, new Vector3(0.028f, 0.028f, 0.028f), 6, 3);
            Vector3 d = new Vector3(0.1f, -0.55f, 0.83f).normalized;
            armR.Box(Body, hand + d * 0.03f, new Vector3(0.09f, 0.016f, 0.02f), Quaternion.LookRotation(d));
            armR.Blade(Accent, hand + d * 0.035f, d, 0.40f, 0.045f, Vector3.right, 0.28f);
            bp.Add(RigBone.ArmR, RigBone.Body, new Vector3(0.115f, 0.60f, 0f), Quaternion.Euler(-12f, 0f, 6f), armR);

            // Bras gauche + orbe.
            var armL = new ShapeBuilder();
            armL.Limb(Body, Vector3.zero, new Vector3(0f, -0.19f, 0.02f), 0.03f, 0.025f, 6);
            armL.Ellipsoid(Body, new Vector3(0f, -0.205f, 0.025f), new Vector3(0.028f, 0.028f, 0.028f), 6, 3);
            armL.Ellipsoid(Accent, new Vector3(0f, -0.215f, 0.075f), new Vector3(0.045f, 0.045f, 0.045f), 8, 4);
            bp.Add(RigBone.ArmL, RigBone.Body, new Vector3(-0.115f, 0.60f, 0f), Quaternion.Euler(-45f, 0f, -4f), armL);

            // Ailes de lumière : trois rémiges en lame, levées et rejetées en arrière.
            ShapeBuilder Wing(bool left)
            {
                var w = new ShapeBuilder();
                if (left) w.PushMirror();
                float[] lengths = { 0.40f, 0.34f, 0.26f };
                float[] angles = { 38f, 18f, -2f };
                for (int i = 0; i < 3; i++)
                {
                    float a = angles[i] * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float L = lengths[i], wd = 0.045f;
                    w.Extrude(Accent, new[]
                    {
                        Vector2.zero, dir * (L * 0.45f) - perp * wd, dir * L, dir * (L * 0.6f) + perp * wd * 0.8f,
                    }, 0.006f);
                }
                w.Limb(Body, Vector3.zero, new Vector3(0.12f, 0.08f, 0f), 0.02f, 0.012f, 5);
                if (left) w.Pop();
                return w;
            }
            Quaternion wingRot = Quaternion.Euler(0f, 30f, 12f);
            bp.Add(RigBone.WingR, RigBone.Body, new Vector3(0.04f, 0.58f, -0.08f), wingRot, Wing(false));
            bp.Add(RigBone.WingL, RigBone.Body, new Vector3(-0.04f, 0.58f, -0.08f), Mirror(wingRot), Wing(true));
        }

        // ================================================================== Roi titan (roi)

        static void BuildKing(CharacterBlueprint bp)
        {
            bp.Scale = 1.27f;
            const float hip = 0.30f;
            var body = new ShapeBuilder();
            // Tassette évasée sous la ceinture, torse large.
            body.Lathe(new List<ProfilePoint> { P(0.175f, -0.12f), P(0.155f, -0.04f, 0.5f), P(0.13f, 0.0f) }, 10, Vector3.zero, Quaternion.identity, Vector3.one);
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, -0.02f), P(0.13f, -0.02f), P(0.145f, 0.03f), P(0.152f, 0.05f, 0f, Accent), P(0.152f, 0.075f),
                P(0.17f, 0.17f, 0.5f), P(0.19f, 0.27f), P(0.17f, 0.33f, 0.5f), P(0.08f, 0.37f), P(0f, 0.375f),
            }, 10);
            for (int s = -1; s <= 1; s += 2)
            {
                body.Ellipsoid(Body, new Vector3(0.19f * s, 0.31f, 0f), new Vector3(0.088f, 0.062f, 0.088f), 7, 3);
                body.Gem(Accent, new Vector3(0.19f * s, 0.365f, 0f), 0.028f, 0.022f, 4);
            }
            // Tête, barbe, yeux.
            body.Ellipsoid(Body, new Vector3(0f, 0.44f, 0.01f), new Vector3(0.07f, 0.08f, 0.075f), 8, 4);
            body.GemAlong(Body, new Vector3(0f, 0.39f, 0.058f), new Vector3(0f, -1f, 0.35f), 0.042f, 0.055f, 5);
            body.Gem(Accent, new Vector3(0.027f, 0.452f, 0.075f), 0.011f, 0.009f, 4);
            body.Gem(Accent, new Vector3(-0.027f, 0.452f, 0.075f), 0.011f, 0.009f, 4);
            // Couronne à pointes.
            body.Lathe(new List<ProfilePoint> { P(0.074f, 0.48f, 0f, Accent), P(0.08f, 0.515f, 0f, Accent) }, 10, Vector3.zero, Quaternion.identity, Vector3.one);
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 7f;
                Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                body.Spike(Accent, o * 0.076f + Vector3.up * 0.51f, o * 0.088f + Vector3.up * (i == 0 ? 0.6f : 0.575f), 0.017f, 4);
            }
            // Cape : panneau courbe derrière les épaules, qui s'évase jusqu'au sol, ourlet lumineux.
            const int seg = 8;
            const float capeTop = 0.33f, capeBottom = -0.27f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.Lerp(-170f, -10f, i / (float)seg) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(-170f, -10f, (i + 1) / (float)seg) * Mathf.Deg2Rad;
                Vector3 t0 = Polar(0.19f, a0, capeTop) + Vector3.back * 0.02f, t1 = Polar(0.19f, a1, capeTop) + Vector3.back * 0.02f;
                Vector3 m0 = Polar(0.21f, a0, 0.02f) + Vector3.back * 0.05f, m1 = Polar(0.21f, a1, 0.02f) + Vector3.back * 0.05f;
                Vector3 b0 = Polar(0.25f, a0, capeBottom) + Vector3.back * 0.06f, b1 = Polar(0.25f, a1, capeBottom) + Vector3.back * 0.06f;
                Vector3 h0 = Polar(0.252f, a0, capeBottom - 0.025f) + Vector3.back * 0.06f, h1 = Polar(0.252f, a1, capeBottom - 0.025f) + Vector3.back * 0.06f;
                body.QuadDouble(Body, t0, t1, m1, m0);
                body.QuadDouble(Body, m0, m1, b1, b0);
                body.QuadDouble(Accent, b0, b1, h1, h0);
            }
            bp.Add(RigBone.Body, null, new Vector3(0f, hip, 0f), body);

            // Jambes en armure.
            var leg = new ShapeBuilder();
            leg.Limb(Body, Vector3.zero, new Vector3(0f, -0.27f, 0f), 0.058f, 0.05f, 6);
            leg.Gem(Accent, new Vector3(0f, -0.14f, 0.055f), 0.02f, 0.024f, 4);
            leg.Box(Body, new Vector3(0f, -0.285f, 0.02f), new Vector3(0.09f, 0.04f, 0.125f));
            bp.Add(RigBone.LegR, null, new Vector3(0.075f, hip, 0f), leg);
            bp.Add(RigBone.LegL, null, new Vector3(-0.075f, hip, 0f), leg);

            // Bras + grande épée plantée devant lui (bloc unique pivotant sur la ligne des épaules).
            var arms = new ShapeBuilder();
            Vector3 hands = new Vector3(0f, -0.18f, 0.15f);
            arms.Limb(Body, new Vector3(0.19f, 0f, 0f), new Vector3(0.05f, -0.17f, 0.14f), 0.048f, 0.04f, 6);
            arms.Limb(Body, new Vector3(-0.19f, 0f, 0f), new Vector3(-0.05f, -0.17f, 0.14f), 0.048f, 0.04f, 6);
            arms.Ellipsoid(Body, hands, new Vector3(0.065f, 0.042f, 0.042f), 6, 3);
            arms.Limb(Body, hands + new Vector3(0f, 0.06f, 0f), hands + new Vector3(0f, -0.06f, 0f), 0.016f, 0.016f, 5);
            arms.Gem(Accent, hands + new Vector3(0f, 0.075f, 0f), 0.022f, 0.022f, 4);
            arms.Box(Accent, hands + new Vector3(0f, -0.075f, 0f), new Vector3(0.22f, 0.026f, 0.032f));
            arms.Blade(Accent, hands + new Vector3(0f, -0.085f, 0f), new Vector3(0f, -1f, 0.04f), 0.335f, 0.065f, Vector3.right, 0.25f);
            bp.Add(RigBone.Arms, RigBone.Body, new Vector3(0f, 0.30f, 0f), arms);
        }

        // ================================================================== Dragon

        static void BuildDragon(CharacterBlueprint bp)
        {
            bp.Scale = 1.15f;
            var body = new ShapeBuilder();
            // Tronc serpentin, crête dorsale lumineuse, ventre lumineux.
            body.Lathe(new List<ProfilePoint>
            {
                P(0f, -0.22f), P(0.075f, -0.2f), P(0.11f, -0.12f, 0.5f), P(0.13f, 0f), P(0.12f, 0.10f, 0.5f),
                P(0.09f, 0.18f), P(0f, 0.22f),
            }, 8, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.9f, 1f, 1.05f));
            for (int i = 0; i < 4; i++)
                body.Spike(Accent, new Vector3(0f, 0.11f, -0.13f + i * 0.085f), new Vector3(0f, 0.18f, -0.17f + i * 0.085f), 0.022f, 4);
            body.Box(Accent, new Vector3(0f, -0.115f, 0f), new Vector3(0.09f, 0.02f, 0.22f));
            // Pattes griffues repliées.
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 a = new Vector3(0.08f * sx, -0.06f, 0.09f * sz);
                    Vector3 knee = a + new Vector3(0.025f * sx, -0.05f, -0.04f);
                    Vector3 foot = knee + new Vector3(0f, -0.035f, 0.035f);
                    body.Limb(Body, a, knee, 0.034f, 0.024f, 5);
                    body.Limb(Body, knee, foot, 0.024f, 0.016f, 5);
                    body.Spike(Accent, foot, foot + new Vector3(0f, -0.015f, 0.035f), 0.011f, 3);
                }
            bp.Add(RigBone.Body, null, new Vector3(0f, 0.80f, 0f), body);

            // Queue en S, pointe en fer de lance lumineux.
            var tail = new ShapeBuilder();
            tail.Chain(Body, new[]
            {
                Vector3.zero, new Vector3(0f, -0.08f, -0.12f), new Vector3(0.035f, -0.2f, -0.16f),
                new Vector3(0.02f, -0.32f, -0.1f), new Vector3(-0.03f, -0.4f, 0.0f),
            }, new[] { 0.08f, 0.062f, 0.046f, 0.03f, 0.016f }, 6);
            tail.ExtrudeSagittal(Accent, new[]
            {
                new Vector2(-0.01f, -0.39f), new Vector2(0.07f, -0.36f), new Vector2(0.13f, -0.43f), new Vector2(0.04f, -0.45f),
            }, 0.012f, new Vector3(-0.03f, 0f, 0f));
            bp.Add(RigBone.Tail, RigBone.Body, new Vector3(0f, -0.02f, -0.19f), tail);

            // Cou en deux segments, puis la tête.
            var neck1 = new ShapeBuilder();
            neck1.Limb(Body, Vector3.zero, new Vector3(0f, 0f, 0.2f), 0.078f, 0.062f, 6);
            neck1.Spike(Accent, new Vector3(0f, 0.055f, 0.1f), new Vector3(0f, 0.11f, 0.06f), 0.018f, 4);
            bp.Add(RigBone.Neck1, RigBone.Body, new Vector3(0f, 0.06f, 0.17f), Quaternion.Euler(-48f, 0f, 0f), neck1);

            var neck2 = new ShapeBuilder();
            neck2.Limb(Body, Vector3.zero, new Vector3(0f, 0f, 0.18f), 0.062f, 0.05f, 6);
            neck2.Spike(Accent, new Vector3(0f, 0.045f, 0.09f), new Vector3(0f, 0.095f, 0.05f), 0.015f, 4);
            bp.Add(RigBone.Neck2, RigBone.Neck1, new Vector3(0f, 0f, 0.19f), Quaternion.Euler(-12f, 0f, 0f), neck2);

            // Tête agrandie (lisibilité vue de dessus), gueule ouverte lumineuse.
            var head = new ShapeBuilder();
            head.Push(Vector3.zero, Quaternion.identity, Vector3.one * 1.3f);
            BuildDragonHead(head);
            head.Pop();
            bp.Add(RigBone.Head, RigBone.Neck2, new Vector3(0f, 0f, 0.19f), Quaternion.Euler(72f, 0f, 0f), head);

            // Ailes membraneuses : membrane en verre, doigts lumineux.
            ShapeBuilder Wing(bool left)
            {
                var w = new ShapeBuilder();
                if (left) w.PushMirror();
                Vector2 elbow = new Vector2(0.28f, 0.17f), tip = new Vector2(0.52f, 0.08f);
                Vector2[] fingers = { new Vector2(0.43f, -0.10f), new Vector2(0.32f, -0.15f), new Vector2(0.18f, -0.17f) };
                w.Extrude(Body, new[]
                {
                    Vector2.zero, elbow, tip, new Vector2(0.44f, -0.03f), fingers[0], new Vector2(0.35f, -0.07f), fingers[1],
                    new Vector2(0.24f, -0.10f), fingers[2], new Vector2(0.08f, -0.08f), new Vector2(0f, -0.10f),
                }, 0.005f);
                w.Limb(Accent, Vector3.zero, elbow, 0.022f, 0.016f, 5);
                w.Limb(Accent, elbow, tip, 0.016f, 0.005f, 4);
                foreach (Vector2 f in fingers)
                    w.Limb(Accent, elbow, f, 0.008f, 0.004f, 4);
                w.Spike(Accent, elbow, elbow + new Vector2(0.01f, 0.06f), 0.012f, 3);
                if (left) w.Pop();
                return w;
            }
            Quaternion wingRot = Quaternion.Euler(0f, 30f, 36f);
            bp.Add(RigBone.WingR, RigBone.Body, new Vector3(0.07f, 0.09f, 0.04f), wingRot, Wing(false));
            bp.Add(RigBone.WingL, RigBone.Body, new Vector3(-0.07f, 0.09f, 0.04f), Mirror(wingRot), Wing(true));
        }

        static void BuildDragonHead(ShapeBuilder b)
        {
            b.Lathe(new List<ProfilePoint>
            {
                P(0f, -0.05f), P(0.055f, -0.04f), P(0.068f, 0.02f, 0.5f), P(0.052f, 0.09f), P(0.032f, 0.15f, 0.5f), P(0f, 0.17f),
            }, 7, new Vector3(0f, 0.02f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, 0.75f));
            // Mâchoire inférieure ouverte, gueule lumineuse.
            b.Box(Body, new Vector3(0f, -0.045f, 0.08f), new Vector3(0.075f, 0.024f, 0.14f), Quaternion.Euler(18f, 0f, 0f));
            b.Ellipsoid(Accent, new Vector3(0f, -0.018f, 0.075f), new Vector3(0.042f, 0.022f, 0.075f), 6, 3);
            for (int s = -1; s <= 1; s += 2)
            {
                b.GemAlong(Accent, new Vector3(0.048f * s, 0.045f, 0.05f), new Vector3(0.3f * s, 0.1f, 1f), 0.014f, 0.026f, 4);
                b.Spike(Body, new Vector3(0.035f * s, 0.06f, -0.02f), new Vector3(0.07f * s, 0.13f, -0.12f), 0.02f, 4);
                b.Spike(Accent, new Vector3(0.022f * s, -0.01f, 0.15f), new Vector3(0.022f * s, -0.04f, 0.16f), 0.008f, 3);
            }
        }

        // ================================================================== statuts

        /// <summary>
        /// Dôme hexagonal unitaire (rayon 1, base y = 0) : polyèdre de Goldberg (dual d'un icosaèdre
        /// subdivisé). Sous-maillage 0 = remplissage discret, 1 = arêtes lumineuses. Pour matériau additif.
        /// </summary>
        public static Mesh ShieldDome()
        {
            if (_shield != null) return _shield;
            // Icosaèdre subdivisé une fois.
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int m)) return m;
                verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                mid[key] = verts.Count - 1;
                return verts.Count - 1;
            }
            var tris = new List<int>();
            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            // Dual : chaque sommet devient une cellule (pentagone ou hexagone) des centres des faces voisines.
            var cells = new List<int>[verts.Count];
            for (int i = 0; i < verts.Count; i++) cells[i] = new List<int>(6);
            var centers = new List<Vector3>(tris.Count / 3);
            for (int f = 0; f < tris.Count / 3; f++)
            {
                centers.Add(((verts[tris[f * 3]] + verts[tris[f * 3 + 1]] + verts[tris[f * 3 + 2]]) / 3f).normalized);
                for (int k = 0; k < 3; k++) cells[tris[f * 3 + k]].Add(f);
            }
            var b2 = new ShapeBuilder();
            for (int v = 0; v < verts.Count; v++)
            {
                Vector3 c = verts[v];
                if (c.y < -0.12f) continue; // dôme : on ne garde que le haut
                Vector3 axisU = Vector3.Cross(c, Mathf.Abs(c.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 axisV = Vector3.Cross(c, axisU);
                List<int> ring = cells[v];
                ring.Sort((p, q) =>
                {
                    Vector3 dp = centers[p] - c, dq = centers[q] - c;
                    return Mathf.Atan2(Vector3.Dot(dp, axisV), Vector3.Dot(dp, axisU)).CompareTo(Mathf.Atan2(Vector3.Dot(dq, axisV), Vector3.Dot(dq, axisU)));
                });
                int n = ring.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p0 = centers[ring[i]], p1 = centers[ring[(i + 1) % n]];
                    Vector3 i0 = Vector3.Lerp(p0, c, 0.16f), i1 = Vector3.Lerp(p1, c, 0.16f);
                    b2.Tri(Body, c, i0, i1);
                    b2.QuadDouble(Accent, p0, p1, i1, i0);
                }
            }
            _shield = b2.ToMesh("KevChess_ShieldDome");
            return _shield;
        }

        /// <summary>Gangue de glace unitaire (rayon ≈ 0,45, hauteur 1) : cocon facetté hérissé de pointes.</summary>
        public static Mesh IceShell()
        {
            if (_ice != null) return _ice;
            var b = new ShapeBuilder(1);
            b.Lathe(new List<ProfilePoint>
            {
                P(0f, 0f), P(0.40f, 0f), P(0.45f, 0.14f, 0.5f), P(0.43f, 0.38f), P(0.40f, 0.62f, 0.5f),
                P(0.30f, 0.83f), P(0.15f, 0.96f, 0.5f), P(0f, 1.0f),
            }, 7);
            var rng = new System.Random(4242);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float y = 0.1f + (float)rng.NextDouble() * 0.75f;
                float r = Mathf.Lerp(0.43f, 0.3f, (y - 0.1f) / 0.75f);
                Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.Spike(0, o * (r - 0.04f) + Vector3.up * y, o * (r + 0.12f) + Vector3.up * (y + 0.1f), 0.06f, 4);
            }
            _ice = b.ToMesh("KevChess_IceShell");
            return _ice;
        }
    }
}
