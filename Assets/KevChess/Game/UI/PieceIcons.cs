using System.Collections.Generic;
using KevChess.Core;
using KevChess.Core.Ux;
using KevChess.Game.Visuals;
using UnityEngine;

namespace KevChess.Game.UI
{
    /// <summary>Vignettes de pièces : silhouettes des maillages procéduraux du plateau, blanches (teintées par Image.color).</summary>
    public static class PieceIcons
    {
        const int Size = 128;
        static readonly Dictionary<PieceType, Sprite> cache = new Dictionary<PieceType, Sprite>();

        public static Sprite For(PieceType type)
        {
            if (cache.TryGetValue(type, out var cached) && cached != null) return cached;
            var kind = Kind(type);
            var mesh = PieceMeshFactory.Get(kind);
            var verts = mesh.vertices;
            var u = new float[verts.Length];
            var v = new float[verts.Length];
            // Le cavalier est extrudé selon x : on le regarde de profil (z, y) ; les pièces tournées se voient pareil de tous côtés.
            bool profile = kind == PieceKind.Knight;
            for (int i = 0; i < verts.Length; i++)
            {
                u[i] = profile ? verts[i].z : verts[i].x;
                v[i] = verts[i].y;
            }
            var alpha = Silhouette.Rasterize(u, v, mesh.triangles, Size);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, alpha[i]);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            cache[type] = sprite;
            return sprite;
        }

        static PieceKind Kind(PieceType type)
        {
            switch (type)
            {
                case PieceType.Knight: return PieceKind.Knight;
                case PieceType.Bishop: return PieceKind.Bishop;
                case PieceType.Rook: return PieceKind.Rook;
                case PieceType.King: return PieceKind.King;
                case PieceType.Pawn: return PieceKind.Pawn;
                default: return PieceKind.Queen;
            }
        }
    }
}
