using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.AlbumOfTheDay.Tests;

public class AlbumOfTheDayServiceTests
{
    private static readonly long Minute = TimeSpan.FromMinutes(1).Ticks;

    [Theory]
    [InlineData(1, 4, true)]
    [InlineData(3, 29, true)]
    [InlineData(3, 30, false)] // runtime not under 30 min
    [InlineData(4, 12, false)] // EP-length track count
    [InlineData(0, 0, false)]
    [InlineData(12, 45, false)]
    public void IsSingle(int tracks, int minutes, bool expected)
        => Assert.Equal(expected, AlbumOfTheDayService.IsSingle(tracks, minutes * Minute));

    [Fact]
    public void ChooseNext_NoRepeatsWithinCycle_SkipsIneligible_ThenResets()
    {
        var candidates = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        var singles = candidates.Take(3).ToHashSet();
        var picked = new List<Guid>();

        var cycle = Enumerable.Range(0, 7)
            .Select(_ => AlbumOfTheDayService.ChooseNext(candidates, picked, id => !singles.Contains(id))!.Value)
            .ToList();

        Assert.Equal(7, cycle.Distinct().Count());
        Assert.DoesNotContain(cycle, singles.Contains);

        // every eligible album picked -> cycle resets, next pick starts a fresh list
        var next = AlbumOfTheDayService.ChooseNext(candidates, picked, id => !singles.Contains(id));
        Assert.NotNull(next);
        Assert.DoesNotContain(next.Value, singles);
        Assert.Equal(new[] { next.Value }, picked);
    }

    [Fact]
    public void ChooseNext_ReturnsNull_WhenNothingEligible()
    {
        var candidates = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var picked = new List<Guid> { candidates[0] };
        Assert.Null(AlbumOfTheDayService.ChooseNext(candidates, picked, _ => false));
        Assert.Null(AlbumOfTheDayService.ChooseNext(Array.Empty<Guid>(), new List<Guid>(), _ => true));
    }

    [Fact]
    public void ChooseNext_KeepsHistory_WhenNothingEligible()
    {
        var candidates = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var picked = new List<Guid> { candidates[0] };
        AlbumOfTheDayService.ChooseNext(candidates, picked, _ => false);
        Assert.Equal(new[] { candidates[0] }, picked);
    }

    [Fact]
    public void ChooseNext_NewCycle_DoesNotRepeatPreviousPick()
    {
        var candidates = new[] { Guid.NewGuid(), Guid.NewGuid() };
        for (var i = 0; i < 50; i++)
        {
            var picked = new List<Guid> { candidates[1], candidates[0] };
            Assert.Equal(candidates[1], AlbumOfTheDayService.ChooseNext(candidates, picked, _ => true));
        }

        var only = new List<Guid> { candidates[0] };
        Assert.Equal(candidates[0], AlbumOfTheDayService.ChooseNext(new[] { candidates[0] }, only, _ => true));
    }
}

public class IndexHtmlInjectionTests
{
    [Fact]
    public void Inject_InlinesEmbeddedScriptBeforeBodyClose()
        => Assert.Matches("(?s)^<html><body><div></div><script>.+</script></body></html>$",
            IndexHtmlInjection.Inject("<html><body><div></div></body></html>"));

    [Theory]
    [InlineData("/web/", true)]
    [InlineData("/web/index.html", true)]
    [InlineData("/jellyfin/web/", true)] // base URL configured
    [InlineData("/Web/Index.html", true)]
    [InlineData("/web/main.bundle.js", false)]
    [InlineData("/web", false)] // Jellyfin redirects this to /web/
    [InlineData("/Items/123", false)]
    public void IsIndexHtml(string path, bool expected)
        => Assert.Equal(expected, IndexHtmlInjection.IsIndexHtml(path));
}
