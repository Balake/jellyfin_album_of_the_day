using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AlbumOfTheDay.Configuration;

/// <summary>Persisted pick state, one entry per music library.</summary>
public class PluginConfiguration : BasePluginConfiguration
{
    public List<LibraryPick> Picks { get; set; } = new();
}

/// <summary>Pick state of one library.</summary>
public class LibraryPick
{
    public Guid LibraryId { get; set; }

    /// <summary>Server-local date of the current pick, yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;

    public Guid AlbumId { get; set; }

    /// <summary>Albums already picked in the current cycle.</summary>
    public List<Guid> PickedAlbumIds { get; set; } = new();

    /// <summary>Earlier picks, newest first.</summary>
    public List<PastPick> History { get; set; } = new();
}

/// <summary>An earlier album of the day.</summary>
public class PastPick
{
    /// <summary>Server-local date it was picked, yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;

    public Guid AlbumId { get; set; }
}
