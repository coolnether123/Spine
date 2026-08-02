using System;

namespace Spine.UI.SettingsFramework
{
    /// <summary>
    /// Supported widget types for rendering settings entries.
    /// </summary>
    public enum SettingType
    {
        /// <summary>Checkbox toggle for boolean values.</summary>
        Bool,

        /// <summary>Color swatch with picker dialog.</summary>
        Color,

        /// <summary>Dropdown selection for enum values.</summary>
        Enum,

        /// <summary>Clickable action button.</summary>
        Button,

        /// <summary>Non-interactive section header.</summary>
        Header,

        /// <summary>Custom immediate-mode row supplied by a setting definition.</summary>
        Custom
    }
}
