using System.Collections.Generic;
using KevChess.Core;
using KevChess.Core.Fantasy;
using KevChess.Game.UI;
using KevChess.Game.Visuals;
using KevChess.Game.Visuals.Fantasy;
using UnityEngine;

namespace KevChess.Game.Legends
{
    /// <summary>
    /// Rendu du plateau vivant, généré par code : fissures qui s'étendent (compte à rebours), gouffres,
    /// portails qui tournent, gisement de cristal. Se synchronise sur l'état de la position.
    /// </summary>
    public sealed class LivingBoard : MonoBehaviour
    {
        static readonly Color FissureColor = new Color(1f, 0.22f, 0.12f);
        static readonly Color ChasmRim = new Color(1f, 0.45f, 0.15f);
        static readonly Color PortalColor = new Color(0.72f, 0.45f, 1f);
        static readonly Color CrystalColor = new Color(1f, 0.82f, 0.35f);

        sealed class Feature
        {
            public GameObject Root;
            public Material Material;
            public TextMesh Countdown;
            public Transform Spin;
            public int Square;
            public int Kind; // 1 fissure, 2 gouffre, 3 portail, 4 gisement
        }

        readonly Dictionary<int, Feature> features = new Dictionary<int, Feature>();
        static Texture2D crackTexture;
        static Mesh shardMesh;
        Material crystalMaterial;
        float time;

        public void Sync(FantasyPosition pos)
        {
            var wanted = new Dictionary<int, int>();
            for (int i = 0; i < FantasyPosition.HoleSlotCount; i++)
            {
                var st = pos.HoleStateAt(i);
                if (st != HoleState.None) wanted[pos.HoleSquare(i)] = st == HoleState.Fissure ? 1 : 2;
            }
            if (pos.PortalState != PortalState.None) { wanted[pos.PortalA] = 3; wanted[pos.PortalB] = 3; }
            if (pos.CrystalSquare != Square.None) wanted[pos.CrystalSquare] = 4;

            var remove = new List<int>();
            foreach (var kv in features)
                if (!wanted.TryGetValue(kv.Key, out int k) || k != kv.Value.Kind) remove.Add(kv.Key);
            foreach (int sq in remove)
            {
                Destroy(features[sq].Root);
                features.Remove(sq);
            }
            foreach (var kv in wanted)
                if (!features.ContainsKey(kv.Key)) features[kv.Key] = Create(kv.Key, kv.Value);

            // Comptes à rebours et intensités.
            for (int i = 0; i < FantasyPosition.HoleSlotCount; i++)
            {
                var st = pos.HoleStateAt(i);
                if (st == HoleState.None || !features.TryGetValue(pos.HoleSquare(i), out var f)) continue;
                if (f.Countdown != null) f.Countdown.text = pos.HoleTimer(i).ToString();
                if (st == HoleState.Fissure)
                {
                    float danger = Mathf.Clamp01(1f - (pos.HoleTimer(i) - 1) / 4f);
                    f.Root.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, danger);
                    f.Material.SetColor(NeonMaterials.BaseColorProperty, FissureColor * Mathf.Lerp(1.2f, 3.2f, danger));
                }
            }
            if (pos.PortalState != PortalState.None)
            {
                bool open = pos.PortalState == PortalState.Open;
                foreach (int sq in new[] { pos.PortalA, pos.PortalB })
                    if (features.TryGetValue(sq, out var f))
                    {
                        f.Material.SetColor(NeonMaterials.BaseColorProperty, PortalColor * (open ? 1.5f : 0.6f));
                        if (f.Countdown != null) f.Countdown.text = pos.PortalTimer.ToString();
                    }
            }
        }

        public void Collapse(int square)
        {
            var p = BoardBuilder.SquareToWorld(Square.File(square), Square.Rank(square));
            NeonEffects.Shatter(p, 0.3f, ChasmRim);
            FantasyFx.Burst(p, ChasmRim, 30, 2f);
        }

        public void CollectCrystal(int square)
        {
            var p = BoardBuilder.SquareToWorld(Square.File(square), Square.Rank(square));
            FantasyFx.Burst(p + Vector3.up * 0.4f, CrystalColor, 40, 2.4f);
            FantasyFx.Filaments(p, 0.8f, CrystalColor, true, 30);
            if (features.TryGetValue(square, out var f)) { Destroy(f.Root); features.Remove(square); }
        }

        public void Announce(BoardEvent e)
        {
            int sq = e.SquareA;
            if (sq < 0) return;
            var p = BoardBuilder.SquareToWorld(Square.File(sq), Square.Rank(sq));
            Color c = e.Kind == BoardEventKind.Fissure ? FissureColor : e.Kind == BoardEventKind.Portals ? PortalColor : CrystalColor;
            NeonEffects.LandingPulse(p, c);
            if (e.SquareB >= 0) NeonEffects.LandingPulse(BoardBuilder.SquareToWorld(Square.File(e.SquareB), Square.Rank(e.SquareB)), c);
        }

        Feature Create(int square, int kind)
        {
            var f = new Feature { Square = square, Kind = kind };
            f.Root = new GameObject("Feature_" + Square.Name(square));
            f.Root.transform.SetParent(transform, false);
            f.Root.transform.position = BoardBuilder.SquareToWorld(Square.File(square), Square.Rank(square));

            switch (kind)
            {
                case 1:
                    f.Material = NeonMaterials.CreateAdditive("KevChess_Fissure", CrackTexture, FissureColor * 1.5f, 18);
                    Quad(f.Root.transform, f.Material, 0.98f, 0.025f);
                    f.Countdown = Label(f.Root.transform, FissureColor);
                    break;
                case 2:
                    var dark = NeonMaterials.CreateLitOpaque("KevChess_Chasm", new Color(0.005f, 0.004f, 0.01f), 0f, 0.1f, Color.black);
                    Quad(f.Root.transform, dark, 0.96f, 0.02f);
                    // Bord brisé discret : un vide sombre, pas une case sélectionnée.
                    f.Material = NeonMaterials.CreateAdditive("KevChess_ChasmRim", CrackTexture, ChasmRim * 0.9f, 18);
                    Quad(f.Root.transform, f.Material, 1.0f, 0.03f);
                    f.Countdown = Label(f.Root.transform, ChasmRim);
                    break;
                case 3:
                    f.Material = NeonMaterials.CreateAdditive("KevChess_Portal", NeonTextures.Ring, PortalColor * 1.3f, 18);
                    var spin = new GameObject("Spin").transform;
                    spin.SetParent(f.Root.transform, false);
                    Quad(spin, f.Material, 0.95f, 0.035f);
                    var inner = NeonMaterials.CreateAdditive("KevChess_PortalCore", NeonTextures.RadialGlow, PortalColor * 0.45f, 17);
                    Quad(spin, inner, 0.55f, 0.03f);
                    f.Spin = spin;
                    f.Countdown = Label(f.Root.transform, PortalColor);
                    break;
                default:
                    if (crystalMaterial == null)
                        crystalMaterial = NeonMaterials.CreateLitTransparent("KevChess_Crystal", new Color(1f, 0.85f, 0.4f, 0.55f), 0.1f, 0.95f, CrystalColor * 2.2f);
                    var cluster = new GameObject("Cluster").transform;
                    cluster.SetParent(f.Root.transform, false);
                    cluster.localScale = Vector3.one * 0.55f; // bouquet contenu dans sa case
                    for (int i = 0; i < 5; i++)
                    {
                        var shard = new GameObject("Shard" + i);
                        shard.transform.SetParent(cluster, false);
                        shard.AddComponent<MeshFilter>().sharedMesh = ShardMesh;
                        shard.AddComponent<MeshRenderer>().sharedMaterial = crystalMaterial;
                        float ang = i * 72f;
                        float r = i == 0 ? 0f : 0.16f;
                        shard.transform.localPosition = Quaternion.Euler(0, ang, 0) * new Vector3(r, 0f, 0f);
                        shard.transform.localRotation = Quaternion.Euler(i == 0 ? 0 : 18, ang, 0);
                        float s = i == 0 ? 1.4f : 0.9f;
                        shard.transform.localScale = new Vector3(s * 0.6f, s, s * 0.6f);
                    }
                    f.Spin = cluster;
                    f.Material = crystalMaterial;
                    break;
            }
            NeonEffects.LandingPulse(f.Root.transform.position, kind == 1 ? FissureColor : kind == 2 ? ChasmRim : kind == 3 ? PortalColor : CrystalColor);
            return f;
        }

        static void Quad(Transform parent, Material m, float size, float y)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = new Vector3(0f, y, 0f);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = Vector3.one * size;
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static TextMesh Label(Transform parent, Color color)
        {
            var go = new GameObject("Countdown");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0.36f, 0.05f, -0.36f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var t = go.AddComponent<TextMesh>();
            t.font = UIKit.Font;
            t.fontSize = 64;
            t.characterSize = 0.045f;
            t.anchor = TextAnchor.MiddleCenter;
            t.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
            return t;
        }

        static Mesh ShardMesh => shardMesh != null ? shardMesh : (shardMesh = PieceMeshFactory.Shard());

        /// <summary>Craquelures générées : quelques lignes brisées partant du centre.</summary>
        static Texture2D CrackTexture
        {
            get
            {
                if (crackTexture != null) return crackTexture;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "KevChess_Cracks", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                var rng = new System.Random(11);
                for (int branch = 0; branch < 7; branch++)
                {
                    float x = n / 2f, y = n / 2f;
                    float ang = (float)(branch * Mathf.PI * 2 / 7 + rng.NextDouble() * 0.5);
                    float width = 3.2f;
                    for (int step = 0; step < 60; step++)
                    {
                        ang += (float)(rng.NextDouble() - 0.5) * 0.7f;
                        x += Mathf.Cos(ang) * 1.1f;
                        y += Mathf.Sin(ang) * 1.1f;
                        width = Mathf.Max(0.8f, width - 0.04f);
                        for (int dy = -3; dy <= 3; dy++)
                            for (int dx = -3; dx <= 3; dx++)
                            {
                                int ix = (int)x + dx, iy = (int)y + dy;
                                if (ix < 0 || iy < 0 || ix >= n || iy >= n) continue;
                                float d = Mathf.Sqrt(dx * dx + dy * dy);
                                if (d > width) continue;
                                byte a = (byte)Mathf.Clamp(255 * (1f - d / (width + 0.5f)), 0, 255);
                                int idx = iy * n + ix;
                                if (a > px[idx].a) px[idx] = new Color32(255, 255, 255, a);
                            }
                        if (x < 2 || y < 2 || x > n - 3 || y > n - 3) break;
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                crackTexture = tex;
                return tex;
            }
        }

        void Update()
        {
            time += Time.deltaTime;
            foreach (var f in features.Values)
            {
                if (f.Spin == null) continue;
                if (f.Kind == 3) f.Spin.localRotation = Quaternion.Euler(0f, time * 70f, 0f);
                else
                {
                    f.Spin.localRotation = Quaternion.Euler(0f, time * 25f, 0f);
                    f.Spin.localPosition = new Vector3(0f, 0.04f + 0.03f * Mathf.Sin(time * 2.2f), 0f);
                }
            }
        }
    }
}
