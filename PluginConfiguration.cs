using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.OfficialRatingBadge
{
    /// <summary>
    /// v1 keeps this deliberately empty — hardcode position/size in the
    /// middleware to start. Add fields here later (corner, font size,
    /// colors, excluded libraries) once the basic version works, and a
    /// config page becomes worth the extra plumbing.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool Enabled { get; set; } = true;
    }
}
