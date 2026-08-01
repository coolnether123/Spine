using Spine.UI.Tooltips;

namespace Spine.Api
{
    /// <summary>
    /// Stable entry point for Spine runtime negotiation and opt-in services.
    /// </summary>
    public static class SpineApi
    {
        public static ISpineRuntimeFacade Runtime =>
            SpineRuntimeFacade.Instance;

        public static ITooltipSizingFacade Tooltips =>
            StableTooltipSizing.Instance;
    }
}
