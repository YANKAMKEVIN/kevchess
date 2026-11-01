using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>Palette active (suit <see cref="VisualTheme"/>).</summary>
    public static class NeonPalette
    {
        /// <summary>Lueur des pièces blanches (cyan froid dans le thème Néon).</summary>
        public static Color WhiteGlow => VisualTheme.Palette.WhiteGlow;
        /// <summary>Lueur des pièces noires (magenta dans le thème Néon).</summary>
        public static Color BlackGlow => VisualTheme.Palette.BlackGlow;
        public static Color Selection => VisualTheme.Palette.Selection;
        public static Color LegalMove => VisualTheme.Palette.LegalMove;
        public static Color Capture => VisualTheme.Palette.Capture;
        public static Color LastMove => VisualTheme.Palette.LastMove;
        public static Color Check => VisualTheme.Palette.Check;
        public static Color Indigo => VisualTheme.Palette.Side;
        public static readonly Color Fog = new Color(0.028f, 0.018f, 0.065f);

        public static Color Glow(bool white) => white ? WhiteGlow : BlackGlow;
    }

    /// <summary>
    /// Création des matériaux URP par code. Les matériaux renvoyés par les propriétés sont
    /// partagés (ne pas les détruire) ; utiliser <c>new Material(m)</c> pour une instance animée.
    ///
    /// Stripping en build : si la <see cref="NeonShaderLibrary"/> existe dans Resources,
    /// les matériaux sont clonés depuis ses gabarits (shader + mots-clés garantis dans le build).
    /// Sinon on retombe sur <see cref="Shader.Find"/> (fonctionne dans l'éditeur, risqué en build).
    /// </summary>
    public static class NeonMaterials
    {
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string ParticleUnlitShaderName = "Universal Render Pipeline/Particles/Unlit";
        public const string SkyboxShaderName = "Skybox/Cubemap";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static readonly int BlendId = Shader.PropertyToID("_Blend");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static readonly int WorkflowId = Shader.PropertyToID("_WorkflowMode");
        static readonly int PreserveSpecId = Shader.PropertyToID("_BlendModePreserveSpecular");
        static readonly int ReceiveShadowsId = Shader.PropertyToID("_ReceiveShadows");
        static readonly int EnvReflectionsId = Shader.PropertyToID("_EnvironmentReflections");
        static readonly int SpecHighlightsId = Shader.PropertyToID("_SpecularHighlights");
        static readonly int SkyTexId = Shader.PropertyToID("_Tex");
        static readonly int SkyTintId = Shader.PropertyToID("_Tint");
        static readonly int SkyExposureId = Shader.PropertyToID("_Exposure");

        /// <summary>Identifiant de la propriété d'émission (pour animer une instance).</summary>
        public static int EmissionColorProperty => EmissionColorId;
        /// <summary>Identifiant de la couleur de base.</summary>
        public static int BaseColorProperty => BaseColorId;

        // Niveaux d'émission (HDR) : > 1 pour déclencher le bloom.
        const float AccentIntensity = 3.2f;

        static Material _sparks, _shards, _rings, _motes, _trail, _frame, _skybox;

        // Matériaux teintés par le thème : créés à la demande, recolorés par ApplyTheme.
        enum Slot
        {
            WhiteBody, BlackBody, WhiteAccent, BlackAccent,
            BoardDark, BoardLight, BoardGrid, FrameSouth, FrameNorth, FrameSide, FrameLip, Abyss, Labels,
            Selection, Legal, Capture, LastMove, Check, Count
        }

        static readonly Material[] _themed = new Material[(int)Slot.Count];

        // ------------------------------------------------------------------ pièces

        /// <summary>Corps cristal d'une pièce (blanc = cristal clair, noir = cristal fumé).</summary>
        public static Material PieceBody(bool white) => Get(white ? Slot.WhiteBody : Slot.BlackBody);

        /// <summary>Accents néon d'une pièce (sous-maillage 1).</summary>
        public static Material PieceAccent(bool white) => Get(white ? Slot.WhiteAccent : Slot.BlackAccent);

        // ------------------------------------------------------------------ plateau

        /// <summary>Cases sombres : verre fumé / obsidienne, semi-transparent et très brillant.</summary>
        public static Material BoardDark => Get(Slot.BoardDark);

        /// <summary>Cases claires : verre givré, plus opaque.</summary>
        public static Material BoardLight => Get(Slot.BoardLight);

        /// <summary>Fines lignes néon entre les cases.</summary>
        public static Material BoardGrid => Get(Slot.BoardGrid);

        /// <summary>Cadre : métal sombre poli (commun à tous les thèmes).</summary>
        public static Material Frame => _frame != null ? _frame : (_frame = CreateLitOpaque("KevChess_Frame",
            new Color(0.035f, 0.035f, 0.055f), 0.85f, 0.88f, Color.black));

        /// <summary>Liseré néon du côté des blancs.</summary>
        public static Material FrameNeonSouth => Get(Slot.FrameSouth);

        /// <summary>Liseré néon du côté des noirs.</summary>
        public static Material FrameNeonNorth => Get(Slot.FrameNorth);

        /// <summary>Liseré néon des côtés latéraux.</summary>
        public static Material FrameNeonSide => Get(Slot.FrameSide);

        /// <summary>Lèvre intérieure lumineuse qui entoure l'aire de jeu.</summary>
        public static Material FrameLip => Get(Slot.FrameLip);

        /// <summary>Sol de l'abîme sous le verre : obsidienne + grille lumineuse lointaine.</summary>
        public static Material Abyss => Get(Slot.Abyss);

        /// <summary>Coordonnées a–h / 1–8 discrètes.</summary>
        public static Material Labels => Get(Slot.Labels);

        // ------------------------------------------------------------------ surlignages (additifs)

        /// <summary>Case sélectionnée : cadre lumineux.</summary>
        public static Material SelectionHighlight => Get(Slot.Selection);

        /// <summary>Coup légal sur case vide : point lumineux.</summary>
        public static Material LegalMove => Get(Slot.Legal);

        /// <summary>Coup légal avec prise : anneau lumineux.</summary>
        public static Material LegalCapture => Get(Slot.Capture);

        /// <summary>Cases du dernier coup : voile discret.</summary>
        public static Material LastMove => Get(Slot.LastMove);

        /// <summary>Roi en échec : halo rouge (sa couleur est pulsée par <see cref="BoardHighlights"/>).</summary>
        public static Material Check => Get(Slot.Check);

        // ------------------------------------------------------------------ thème

        /// <summary>Recolore les matériaux partagés déjà créés selon <see cref="VisualTheme"/>.</summary>
        internal static void ApplyTheme()
        {
            for (int i = 0; i < _themed.Length; i++)
                if (_themed[i] != null) Paint((Slot)i, _themed[i]);
        }

        static Material Get(Slot slot)
        {
            var m = _themed[(int)slot];
            if (m != null) return m;
            m = Create(slot);
            Paint(slot, m);
            _themed[(int)slot] = m;
            return m;
        }

        static Material Create(Slot slot)
        {
            switch (slot)
            {
                case Slot.WhiteBody: return CreateLitOpaque("KevChess_WhiteCrystal", Color.white, 0.35f, 0.93f, Color.black);
                case Slot.BlackBody: return CreateLitOpaque("KevChess_BlackCrystal", Color.black, 0.55f, 0.95f, Color.black);
                case Slot.WhiteAccent: return CreateLitOpaque("KevChess_WhiteNeon", Color.white, 0f, 0.9f, Color.black);
                case Slot.BlackAccent: return CreateLitOpaque("KevChess_BlackNeon", Color.white, 0f, 0.9f, Color.black);
                case Slot.BoardDark: return CreateLitTransparent("KevChess_BoardDark", Color.black, 0.45f, 0.97f, Color.black);
                case Slot.BoardLight: return CreateLitTransparent("KevChess_BoardLight", Color.black, 0.2f, 0.92f, Color.black);
                case Slot.BoardGrid: return CreateLitOpaque("KevChess_BoardGrid", Color.black, 0f, 0.5f, Color.black);
                case Slot.FrameSouth: return CreateLitOpaque("KevChess_FrameNeonSouth", Color.black, 0f, 0.8f, Color.black);
                case Slot.FrameNorth: return CreateLitOpaque("KevChess_FrameNeonNorth", Color.black, 0f, 0.8f, Color.black);
                case Slot.FrameSide: return CreateLitOpaque("KevChess_FrameNeonSide", Color.black, 0f, 0.8f, Color.black);
                case Slot.FrameLip: return CreateLitOpaque("KevChess_FrameLip", Color.black, 0f, 0.8f, Color.black);
                case Slot.Abyss:
                    var abyss = CreateLitOpaque("KevChess_Abyss", new Color(0.01f, 0.008f, 0.02f), 0.2f, 0.7f, Color.black);
                    abyss.SetTexture(EmissionMapId, NeonTextures.Grid);
                    return abyss;
                case Slot.Labels: return CreateLitOpaque("KevChess_Labels", Color.black, 0f, 0.5f, Color.black);
                case Slot.Selection: return CreateAdditive("KevChess_Selection", NeonTextures.SquareFrame, Color.white, 20);
                case Slot.Legal: return CreateAdditive("KevChess_LegalMove", NeonTextures.RadialGlow, Color.white, 21);
                case Slot.Capture: return CreateAdditive("KevChess_LegalCapture", NeonTextures.Ring, Color.white, 21);
                case Slot.LastMove: return CreateAdditive("KevChess_LastMove", NeonTextures.SoftSquare, Color.white, 19);
                default: return CreateAdditive("KevChess_Check", NeonTextures.RadialGlow, Color.white, 22);
            }
        }

        /// <summary>Couleurs d'un emplacement selon le thème et le mode lisibilité.</summary>
        static void Paint(Slot slot, Material m)
        {
            var p = VisualTheme.Palette;
            bool hc = VisualTheme.HighContrast;
            float accent = AccentIntensity * (hc ? 1.45f : 1f);
            float body = hc ? 1.8f : 1f;
            float line = hc ? 1.8f : 1f;
            switch (slot)
            {
                case Slot.WhiteBody: Lit(m, p.WhiteBody, p.WhiteBodyEmission * body); break;
                case Slot.BlackBody: Lit(m, p.BlackBody, p.BlackBodyEmission * body); break;
                case Slot.WhiteAccent: Lit(m, Color.Lerp(p.WhiteGlow, Color.white, 0.4f), p.WhiteGlow * accent); break;
                case Slot.BlackAccent: Lit(m, Color.Lerp(p.BlackGlow, Color.white, 0.4f), p.BlackGlow * accent); break;
                case Slot.BoardDark: Lit(m, p.BoardDark, p.BoardDarkEmission); break;
                case Slot.BoardLight:
                    // Lisibilité : cases claires plus claires, pour mieux détacher les pièces sombres.
                    Lit(m, hc ? Color.Lerp(p.BoardLight, new Color(1f, 1f, 1f, p.BoardLight.a), 0.18f) : p.BoardLight,
                        p.BoardLightEmission * (hc ? 1.6f : 1f));
                    break;
                case Slot.BoardGrid: Lit(m, Color.black, p.Grid * 1.1f * line); break;
                case Slot.FrameSouth: Lit(m, Color.black, p.WhiteGlow * 2.6f * line); break;
                case Slot.FrameNorth: Lit(m, Color.black, p.BlackGlow * 2.6f * line); break;
                case Slot.FrameSide: Lit(m, Color.black, p.Side * 2.3f * line); break;
                case Slot.FrameLip: Lit(m, Color.black, p.Lip * 1.5f * line); break;
                case Slot.Abyss: m.SetColor(EmissionColorId, p.Abyss * 0.9f); break;
                case Slot.Labels: Lit(m, Color.black, p.Labels * (hc ? 2.4f : 0.75f)); break;
                case Slot.Selection: m.SetColor(BaseColorId, p.Selection * 2.2f * (hc ? 1.3f : 1f)); break;
                case Slot.Legal: m.SetColor(BaseColorId, p.LegalMove * 1.8f * (hc ? 1.4f : 1f)); break;
                case Slot.Capture: m.SetColor(BaseColorId, p.Capture * 2.2f * (hc ? 1.3f : 1f)); break;
                case Slot.LastMove: m.SetColor(BaseColorId, p.LastMove * 0.55f * (hc ? 1.5f : 1f)); break;
                case Slot.Check: m.SetColor(BaseColorId, p.Check * 2.5f); break;
            }
        }

        static void Lit(Material m, Color baseColor, Color emission)
        {
            m.SetColor(BaseColorId, baseColor);
            m.SetColor(EmissionColorId, emission);
        }

        // ------------------------------------------------------------------ effets

        /// <summary>Étincelles / halos (particules billboard).</summary>
        public static Material Sparks => _sparks != null ? _sparks : (_sparks = CreateAdditive("KevChess_Sparks", NeonTextures.RadialGlow, Color.white * 4f, 40));

        /// <summary>Éclats de cristal (particules maillage).</summary>
        public static Material Shards => _shards != null ? _shards : (_shards = CreateAdditive("KevChess_Shards", null, Color.white * 2.5f, 39));

        /// <summary>Anneaux d'onde au sol.</summary>
        public static Material Rings => _rings != null ? _rings : (_rings = CreateAdditive("KevChess_Rings", NeonTextures.Ring, Color.white * 2.5f, 38));

        /// <summary>Traînée lumineuse d'une pièce qui glisse, flèche d'indice (texture : bande douce).</summary>
        public static Material Trail => _trail != null ? _trail : (_trail = CreateAdditive("KevChess_Trail", NeonTextures.SoftLine, Color.white * 2.4f, 37));

        /// <summary>Poussières lumineuses d'ambiance.</summary>
        public static Material Motes => _motes != null ? _motes : (_motes = CreateAdditive("KevChess_Motes", NeonTextures.RadialGlow, Color.white * 1.6f, 30));

        /// <summary>Skybox nébuleuse générée par code.</summary>
        public static Material Skybox
        {
            get
            {
                if (_skybox != null) return _skybox;
                var lib = NeonShaderLibrary.Load();
                _skybox = lib != null && lib.skyboxCubemap != null
                    ? new Material(lib.skyboxCubemap)
                    : new Material(FindShader(SkyboxShaderName));
                _skybox.name = "KevChess_Skybox";
                _skybox.SetTexture(SkyTexId, NeonTextures.NebulaSky);
                _skybox.SetColor(SkyTintId, new Color(0.5f, 0.5f, 0.5f, 1f));
                _skybox.SetFloat(SkyExposureId, 1.6f);
                return _skybox;
            }
        }

        // ------------------------------------------------------------------ fabriques

        /// <summary>URP/Lit opaque émissif.</summary>
        public static Material CreateLitOpaque(string name, Color baseColor, float metallic, float smoothness, Color emission)
        {
            var lib = NeonShaderLibrary.Load();
            Material m = lib != null && lib.litOpaqueEmissive != null ? new Material(lib.litOpaqueEmissive) : new Material(FindShader(LitShaderName));
            ConfigureLitOpaqueEmissive(m);
            m.name = name;
            m.SetColor(BaseColorId, baseColor);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(SmoothnessId, smoothness);
            m.SetColor(EmissionColorId, emission);
            return m;
        }

        /// <summary>URP/Lit transparent (alpha, spéculaire et reflets préservés) émissif.</summary>
        public static Material CreateLitTransparent(string name, Color baseColor, float metallic, float smoothness, Color emission)
        {
            var lib = NeonShaderLibrary.Load();
            Material m = lib != null && lib.litTransparentEmissive != null ? new Material(lib.litTransparentEmissive) : new Material(FindShader(LitShaderName));
            ConfigureLitTransparentEmissive(m);
            m.name = name;
            m.SetColor(BaseColorId, baseColor);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(SmoothnessId, smoothness);
            m.SetColor(EmissionColorId, emission);
            return m;
        }

        /// <summary>URP/Particles/Unlit additif (couleur HDR × couleur de sommet × texture).</summary>
        public static Material CreateAdditive(string name, Texture texture, Color color, int queueOffset)
        {
            var lib = NeonShaderLibrary.Load();
            Material m = lib != null && lib.particleAdditive != null ? new Material(lib.particleAdditive) : new Material(FindShader(ParticleUnlitShaderName));
            ConfigureParticleAdditive(m);
            m.name = name;
            m.SetColor(BaseColorId, color);
            if (texture != null) m.SetTexture(BaseMapId, texture);
            m.renderQueue = (int)RenderQueue.Transparent + queueOffset;
            return m;
        }

        // ------------------------------------------------------------------ configuration des gabarits
        // Ces fonctions sont aussi utilisées par le générateur d'assets de l'éditeur, pour que
        // les gabarits du build aient exactement les mêmes mots-clés que les matériaux runtime.

        public static void ConfigureLitOpaqueEmissive(Material m)
        {
            m.SetFloat(WorkflowId, 1f);
            m.SetFloat(SurfaceId, 0f);
            m.SetFloat(BlendId, 0f);
            m.SetFloat(SrcBlendId, (float)BlendMode.One);
            m.SetFloat(DstBlendId, (float)BlendMode.Zero);
            m.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            m.SetFloat(DstBlendAlphaId, (float)BlendMode.Zero);
            m.SetFloat(ZWriteId, 1f);
            m.SetFloat(CullId, (float)CullMode.Back);
            m.SetFloat(ReceiveShadowsId, 1f);
            m.SetFloat(EnvReflectionsId, 1f);
            m.SetFloat(SpecHighlightsId, 1f);
            m.SetOverrideTag("RenderType", "Opaque");
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.renderQueue = (int)RenderQueue.Geometry;
        }

        public static void ConfigureLitTransparentEmissive(Material m)
        {
            m.SetFloat(WorkflowId, 1f);
            m.SetFloat(SurfaceId, 1f);
            m.SetFloat(BlendId, 0f); // Alpha
            m.SetFloat(PreserveSpecId, 1f);
            // Spéculaire préservé : la couleur est prémultipliée dans le shader.
            m.SetFloat(SrcBlendId, (float)BlendMode.One);
            m.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            m.SetFloat(DstBlendAlphaId, (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(ZWriteId, 0f);
            m.SetFloat(CullId, (float)CullMode.Back);
            m.SetFloat(ReceiveShadowsId, 1f);
            m.SetFloat(EnvReflectionsId, 1f);
            m.SetFloat(SpecHighlightsId, 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        public static void ConfigureParticleAdditive(Material m)
        {
            m.SetFloat(SurfaceId, 1f);
            m.SetFloat(BlendId, 2f); // Additive
            m.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            m.SetFloat(DstBlendId, (float)BlendMode.One);
            m.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            m.SetFloat(DstBlendAlphaId, (float)BlendMode.One);
            m.SetFloat(ZWriteId, 0f);
            m.SetFloat(CullId, (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.DisableKeyword("_EMISSION");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static Shader FindShader(string name)
        {
            Shader s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogError("[KevChess] Shader introuvable : " + name +
                               ". Vérifiez que URP est actif et que la NeonShaderLibrary est générée (menu KevChess > Visuals).");
                s = Shader.Find("Hidden/InternalErrorShader");
            }
            return s;
        }
    }
}
