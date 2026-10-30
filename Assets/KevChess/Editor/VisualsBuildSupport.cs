using KevChess.Game.Visuals.Editor;

namespace KevChess.EditorTools
{
    /// <summary>Garantit que les shaders et variantes créés par code partent dans le build.</summary>
    public static class VisualsBuildSupport
    {
        public static void Apply()
        {
            NeonAssetGenerator.Generate(true);
            NeonAssetGenerator.KeepFogVariants();
        }
    }
}
