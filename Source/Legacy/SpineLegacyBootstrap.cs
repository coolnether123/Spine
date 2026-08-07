#if RWT_LEGACY_BOOTSTRAP
using Verse;

namespace Spine.Legacy
{
#if !RWT_RIMWORLD_ALPHA4
    [StaticConstructorOnStartup]
#endif
    public static class SpineLegacyBootstrap
    {
        static SpineLegacyBootstrap()
        {
            Spine.Harmony.SpineLegacyApi.Initialize();
        }
    }
}
#endif
