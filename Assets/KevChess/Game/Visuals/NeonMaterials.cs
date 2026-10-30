using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>Palette de la direction artistique « cristal / néon ».</summary>
    public static class NeonPalette
    {
        /// <summary>Lueur des pièces blanches (cyan froid).</summary>
        public static readonly Color WhiteGlow = new Color(0.35f, 0.92f, 1.00f);
        /// <summary>Lueur des pièces noires (magenta / violet).</summary>
        public static readonly Color BlackGlow = new Color(1.00f, 0.22f, 0.85f);
        public static readonly Color Selection = new Color(0.45f, 1.00f, 0.95f);
        public static readonly Color LegalMove = new Color(0.55f, 0.85f, 1.00f);
        public static readonly Color Capture = new Color(1.00f, 0.38f, 0.55f);
        public static readonly Color LastMove = new Color(1.00f, 0.72f, 0.30f);
        public static readonly Color Check = new Color(1.00f, 0.08f, 0.12f);
        public static readonly Color Indigo = new Color(0.50f, 0.42f, 1.00f);
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

        static Material _whiteBody, _blackBody, _whiteAccent, _blackAccent;
        static Material _boardDark, _boardLight, _boardGrid, _frame, _frameSouth, _frameNorth, _frameSide, _frameLip, _abyss, _labels;
        static Material _selection, _legal, _capture, _lastMove, _check, _sparks, _shards, _rings, _motes, _skybox;

        // ------------------------------------------------------------------ pièces

        /// <summary>Corps cristal d'une pièce (blanc = cristal clair, noir = cristal fumé).</summary>
        public static Material PieceBody(bool white)
        {
            if (white)
                return _whiteBody != null ? _whiteBody : (_whiteBody = CreateLitOpaque("KevChess_WhiteCrystal",
                    new Color(0.70f, 0.84f, 0.97f), 0.35f, 0.93f, new Color(0.05f, 0.16f, 0.22f)));
            return _blackBody != null ? _blackBody : (_blackBody = CreateLitOpaque("KevChess_BlackCrystal",
                new Color(0.045f, 0.03f, 0.075f), 0.55f, 0.95f, new Color(0.10f, 0.015f, 0.13f)));
        }

        /// <summary>Accents néon d'une pièce (sous-maillage 1).</summary>
        public static Material PieceAccent(bool white)
        {
            if (white)
                return _whiteAccent != null ? _whiteAccent : (_whiteAccent = CreateLitOpaque("KevChess_WhiteNeon",
                    new Color(0.6f, 0.95f, 1f), 0f, 0.9f, NeonPalette.WhiteGlow * AccentIntensity));
            return _blackAccent != null ? _blackAccent : (_blackAccent = CreateLitOpaque("KevChess_BlackNeon",
                new Color(1f, 0.5f, 0.9f), 0f, 0.9f, NeonPalette.BlackGlow * AccentIntensity));
        }

        // ------------------------------------------------------------------ plateau

        /// <summary>Cases sombres : verre fumé / obsidienne, semi-transparent et très brillant.</summary>
        public static Material BoardDark => _boardDark != null ? _boardDark : (_boardDark = CreateLitTransparent("KevChess_BoardDark",
            new Color(0.012f, 0.010f, 0.028f, 0.66f), 0.45f, 0.97f, new Color(0.004f, 0.002f, 0.012f)));

        /// <summary>Cases claires : verre givré bleuté, plus opaque.</summary>
        public static Material BoardLight => _boardLight != null ? _boardLight : (_boardLight = CreateLitTransparent("KevChess_BoardLight",
            new Color(0.28f, 0.31f, 0.44f, 0.80f), 0.2f, 0.92f, new Color(0.025f, 0.035f, 0.065f)));

        /// <summary>Fines lignes néon entre les cases.</summary>
        public static Material BoardGrid => _boardGrid != null ? _boardGrid : (_boardGrid = CreateLitOpaque("KevChess_BoardGrid",
            Color.black, 0f, 0.5f, new Color(0.30f, 0.55f, 1.0f) * 1.1f));

        /// <summary>Cadre : métal sombre poli.</summary>
        public static Material Frame => _frame != null ? _frame : (_frame = CreateLitOpaque("KevChess_Frame",
            new Color(0.035f, 0.035f, 0.055f), 0.85f, 0.88f, Color.black));

        /// <summary>Liseré néon du côté des blancs.</summary>
        public static Material FrameNeonSouth => _frameSouth != null ? _frameSouth : (_frameSouth = CreateLitOpaque("KevChess_FrameNeonSouth",
            Color.black, 0f, 0.8f, NeonPalette.WhiteGlow * 2.6f));

        /// <summary>Liseré néon du côté des noirs.</summary>
        public static Material FrameNeonNorth => _frameNorth != null ? _frameNorth : (_frameNorth = CreateLitOpaque("KevChess_FrameNeonNorth",
            Color.black, 0f, 0.8f, NeonPalette.BlackGlow * 2.6f));

        /// <summary>Liseré néon des côtés latéraux.</summary>
        public static Material FrameNeonSide => _frameSide != null ? _frameSide : (_frameSide = CreateLitOpaque("KevChess_FrameNeonSide",
            Color.black, 0f, 0.8f, NeonPalette.Indigo * 2.3f));

        /// <summary>Lèvre intérieure lumineuse qui entoure l'aire de jeu.</summary>
        public static Material FrameLip => _frameLip != null ? _frameLip : (_frameLip = CreateLitOpaque("KevChess_FrameLip",
            Color.black, 0f, 0.8f, new Color(0.40f, 0.62f, 1.0f) * 1.5f));

        /// <summary>Sol de l'abîme sous le verre : obsidienne + grille lumineuse lointaine.</summary>
        public static Material Abyss
        {
            get
            {
                if (_abyss != null) return _abyss;
                _abyss = CreateLitOpaque("KevChess_Abyss", new Color(0.01f, 0.008f, 0.02f), 0.2f, 0.7f, new Color(0.22f, 0.14f, 0.60f) * 0.9f);
                _abyss.SetTexture(EmissionMapId, NeonTextures.Grid);
                return _abyss;
            }
        }

        /// <summary>Coordonnées a–h / 1–8 discrètes.</summary>
        public static Material Labels => _labels != null ? _labels : (_labels = CreateLitOpaque("KevChess_Labels",
            Color.black, 0f, 0.5f, new Color(0.45f, 0.70f, 1.0f) * 0.75f));

        // ------------------------------------------------------------------ surlignages (additifs)

        /// <summary>Case sélectionnée : cadre lumineux.</summary>
        public static Material SelectionHighlight => _selection != null ? _selection : (_selection = CreateAdditive("KevChess_Selection", NeonTextures.SquareFrame, NeonPalette.Selection * 2.2f, 20));

        /// <summary>Coup légal sur case vide : point lumineux.</summary>
        public static Material LegalMove => _legal != null ? _legal : (_legal = CreateAdditive("KevChess_LegalMove", NeonTextures.RadialGlow, NeonPalette.LegalMove * 1.8f, 21));

        /// <summary>Coup légal avec prise : anneau lumineux.</summary>
        public static Material LegalCapture => _capture != null ? _capture : (_capture = CreateAdditive("KevChess_LegalCapture", NeonTextures.Ring, NeonPalette.Capture * 2.2f, 21));

        /// <summary>Cases du dernier coup : voile ambré discret.</summary>
        public static Material LastMove => _lastMove != null ? _lastMove : (_lastMove = CreateAdditive("KevChess_LastMove", NeonTextures.SoftSquare, NeonPalette.LastMove * 0.55f, 19));

        /// <summary>Roi en échec : halo rouge (sa couleur est pulsée par <see cref="BoardHighlights"/>).</summary>
        public static Material Check => _check != null ? _check : (_check = CreateAdditive("KevChess_Check", NeonTextures.RadialGlow, NeonPalette.Check * 2.5f, 22));

        // ------------------------------------------------------------------ effets

        /// <summary>Étincelles / halos (particules billboard).</summary>
        public static Material Sparks => _sparks != null ? _sparks : (_sparks = CreateAdditive("KevChess_Sparks", NeonTextures.RadialGlow, Color.white * 4f, 40));

        /// <summary>Éclats de cristal (particules maillage).</summary>
        public static Material Shards => _shards != null ? _shards : (_shards = CreateAdditive("KevChess_Shards", null, Color.white * 2.5f, 39));

        /// <summary>Anneaux d'onde au sol.</summary>
        public static Material Rings => _rings != null ? _rings : (_rings = CreateAdditive("KevChess_Rings", NeonTextures.Ring, Color.white * 2.5f, 38));

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
