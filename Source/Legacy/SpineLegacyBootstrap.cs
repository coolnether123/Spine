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
            Log.Message("[Spine] Loaded legacy compatibility assembly.");
        }
    }
}
