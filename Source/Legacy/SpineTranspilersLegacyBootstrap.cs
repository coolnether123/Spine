#if RWT_LEGACY_BOOTSTRAP
using Verse;

namespace Spine.Legacy
{
#if !RWT_RIMWORLD_ALPHA4
    [StaticConstructorOnStartup]
#endif
    public static class SpineTranspilersLegacyBootstrap
    {
        static SpineTranspilersLegacyBootstrap()
        {
            Log.Message("[Spine] Loaded legacy transpiler compatibility assembly.");
        }
    }
}
#endif
