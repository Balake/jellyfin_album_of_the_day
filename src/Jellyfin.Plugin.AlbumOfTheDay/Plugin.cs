using System;
using Jellyfin.Plugin.AlbumOfTheDay.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AlbumOfTheDay;

/// <summary>Album of the Day plugin.</summary>
public class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Album of the Day";

    public override Guid Id => Guid.Parse("a81e73b7-afc7-4a56-be85-9813586c734a");
}
