using System;

namespace Spine.Api
{
    internal sealed class SpineRuntimeFacade : ISpineRuntimeFacade
    {
        internal static readonly SpineRuntimeFacade Instance =
            new SpineRuntimeFacade();

        private static readonly SpineApiDescriptor CurrentDescriptor =
            new SpineApiDescriptor(
                "CoolNether123.Spine",
                new SemanticVersion(1, 0, 0),
                SpineCapability.Revisions |
                SpineCapability.DirtyRegions |
                SpineCapability.BoundedCaches |
                SpineCapability.RenderPipelines |
                SpineCapability.Diagnostics |
                SpineCapability.Settings |
                SpineCapability.HarmonyPatching |
                SpineCapability.FluentTranspilers |
                SpineCapability.TooltipSizing);

        private SpineRuntimeFacade()
        {
        }

        public SpineApiDescriptor Descriptor => CurrentDescriptor;

        public SpineCompatibilityResult Check(
            SpineRequirement requirement)
        {
            var missing = requirement.RequiredCapabilities &
                ~CurrentDescriptor.Capabilities;
            if (CurrentDescriptor.Version < requirement.MinimumVersion)
            {
                return new SpineCompatibilityResult(
                    false,
                    missing,
                    requirement.ConsumerId + " requires Spine API " +
                    requirement.MinimumVersion + " or newer; loaded " +
                    CurrentDescriptor.Version + ".");
            }

            if (missing != SpineCapability.None)
            {
                return new SpineCompatibilityResult(
                    false,
                    missing,
                    requirement.ConsumerId +
                    " requires unavailable Spine capabilities: " +
                    missing + ". Loaded Spine " +
                    CurrentDescriptor.Version + " advertises " +
                    CurrentDescriptor.Capabilities + ".");
            }

            return new SpineCompatibilityResult(
                true,
                SpineCapability.None,
                requirement.ConsumerId + " requirements are satisfied by " +
                "Spine " + CurrentDescriptor.Version + ".");
        }

        public void Require(SpineRequirement requirement)
        {
            var result = Check(requirement);
            if (!result.IsCompatible)
            {
                throw new NotSupportedException(result.Detail);
            }
        }
    }
}
