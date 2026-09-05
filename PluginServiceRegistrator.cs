using System;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.OfficialRatingBadge
{
    /// <summary>
    /// Jellyfin calls RegisterServices on every plugin at startup. This is
    /// where we hook our middleware into the same ASP.NET Core pipeline
    /// Jellyfin uses to serve everything, including /Items/{id}/Images/*.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<IStartupFilter, OfficialRatingBadgeStartupFilter>();
        }
    }

    /// <summary>
    /// IStartupFilter is the standard ASP.NET Core extension point for a
    /// class library (which is what a Jellyfin plugin is) to insert its
    /// own middleware into the host's request pipeline without owning
    /// Startup.cs itself.
    /// </summary>
    public class OfficialRatingBadgeStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseMiddleware<OfficialRatingBadgeMiddleware>();
                next(app);
            };
        }
    }
}
