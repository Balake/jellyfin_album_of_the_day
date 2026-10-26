using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AlbumOfTheDay.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.AlbumOfTheDay;

/// <summary>Picks one album per music library per server-local day.</summary>
public class AlbumOfTheDayService
{
    private static readonly long SingleMaxTicks = TimeSpan.FromMinutes(30).Ticks;

    private readonly ILibraryManager _libraryManager;
    private readonly Lock _lock = new();

    public AlbumOfTheDayService(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>Single = 1-3 tracks AND total runtime under 30 minutes.</summary>
    public static bool IsSingle(int trackCount, long totalRunTimeTicks)
        => trackCount is >= 1 and <= 3 && totalRunTimeTicks < SingleMaxTicks;

    /// <summary>
    /// Picks a random eligible candidate not yet in <paramref name="picked"/> and adds it there.
    /// When every eligible candidate was already picked, starts a new cycle, avoiding the previous pick
    /// unless it is the only eligible album. History is left untouched when nothing is eligible.
    /// </summary>
    public static Guid? ChooseNext(IReadOnlyCollection<Guid> candidates, List<Guid> picked, Func<Guid, bool> isEligible)
    {
        var pickedSet = picked.ToHashSet();
        var next = FirstEligible(candidates.Where(id => !pickedSet.Contains(id)), isEligible);
        if (next is null && picked.Count > 0)
        {
            var previous = picked[^1];
            next = FirstEligible(candidates.Where(id => id != previous), isEligible)
                ?? (candidates.Contains(previous) && isEligible(previous) ? previous : null);
            if (next is not null)
            {
                picked.Clear();
            }
        }

        if (next is not null)
        {
            picked.Add(next.Value);
        }

        return next;
    }

    private static Guid? FirstEligible(IEnumerable<Guid> ids, Func<Guid, bool> isEligible)
    {
        var shuffled = ids.ToArray();
        Random.Shared.Shuffle(shuffled);
        foreach (var id in shuffled)
        {
            if (isEligible(id))
            {
                return id;
            }
        }

        return null;
    }

    public Guid? GetAlbumOfTheDay(Guid libraryId)
    {
        var plugin = Plugin.Instance!;
        lock (_lock)
        {
            // Read inside the lock so a request that waited across midnight can't write an older date.
            var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var pick = plugin.Configuration.Picks.Find(p => p.LibraryId == libraryId);
            if (pick is null)
            {
                pick = new LibraryPick { LibraryId = libraryId };
                plugin.Configuration.Picks.Add(pick);
            }
            else if (pick.Date == today && _libraryManager.GetItemById(pick.AlbumId) is MusicAlbum)
            {
                return pick.AlbumId;
            }

            var albums = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.MusicAlbum],
                Recursive = true,
                ParentId = libraryId
            }).OfType<MusicAlbum>().ToDictionary(a => a.Id);

            var chosen = ChooseNext(albums.Keys, pick.PickedAlbumIds, id => IsEligible(albums[id]));
            if (chosen is null)
            {
                return null;
            }

            pick.Date = today;
            pick.AlbumId = chosen.Value;
            plugin.SaveConfiguration();
            return chosen;
        }
    }

    private static bool IsEligible(MusicAlbum album)
    {
        var tracks = album.Tracks.ToList();
        return tracks.Count > 0 && !IsSingle(tracks.Count, tracks.Sum(t => t.RunTimeTicks ?? 0));
    }
}
