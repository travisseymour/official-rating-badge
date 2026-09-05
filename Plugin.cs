using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.OfficialRatingBadge
{
    /// <summary>
    /// Plugin entry point. Registers plugin identity/metadata with Jellyfin.
    /// The actual work happens in OfficialRatingBadgeMiddleware, wired up
    /// via PluginServiceRegistrator.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin? Instance { get; private set; }

        public override string Name => "Official Rating Badge";

        // Generate your own GUID once (e.g. via `uuidgen` or Visual Studio's
        // "Create GUID" tool) and hardcode it here. It must never change
        // across releases, or Jellyfin will treat future versions as a
        // different plugin.
        public override Guid Id => Guid.Parse("15d31f10-8471-4f42-92b0-97b24902fc1e");

        public override string Description =>
            "Draws the item's OfficialRating (PG-13, R, TV-MA, etc.) as a small " +
            "badge onto poster images, server-side, so it shows on every client.";
    }
}
