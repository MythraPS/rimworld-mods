using Verse;

namespace SukunaPsycasts
{
    /// <summary>Draws only while Facial Animation is on: without it the face tattoo already shows these.</summary>
    public class PawnRenderNodeWorker_OverFacialAnimation : PawnRenderNodeWorker
    {
        private static bool? fa;
        public static bool FacialAnimation => fa ?? (fa = ModsConfig.IsActive("nals.facialanimation") || ModsConfig.IsActive("nals.facialanimationexperimentals")).Value;

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) => FacialAnimation && base.CanDrawNow(node, parms);
    }
}
