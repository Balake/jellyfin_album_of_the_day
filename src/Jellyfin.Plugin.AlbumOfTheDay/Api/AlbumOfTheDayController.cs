using System;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AlbumOfTheDay.Api;

/// <summary>Album of the Day API.</summary>
[ApiController]
[Authorize]
public class AlbumOfTheDayController : ControllerBase
{
    // Jellyfin.Api.Constants.InternalClaimTypes.UserId; Jellyfin.Api is not referenceable from plugins.
    private const string UserIdClaim = "Jellyfin-UserId";

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly AlbumOfTheDayService _service;

    public AlbumOfTheDayController(ILibraryManager libraryManager, IUserManager userManager, AlbumOfTheDayService service)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _service = service;
    }

    [HttpGet("AlbumOfTheDay/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetAlbumOfTheDay([FromRoute] Guid libraryId)
    {
        var claim = User.Claims.FirstOrDefault(c => string.Equals(c.Type, UserIdClaim, StringComparison.OrdinalIgnoreCase));
        var user = Guid.TryParse(claim?.Value, out var userId) ? _userManager.GetUserById(userId) : null;
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var folder = _libraryManager.GetItemById(libraryId);
        if (folder is null)
        {
            return NotFound();
        }

        if (!folder.IsVisible(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (folder is not CollectionFolder { CollectionType: CollectionType.music })
        {
            return NotFound();
        }

        var id = _service.GetAlbumOfTheDay(libraryId);
        // The pick is shared; hide it from users whose parental/tag limits block that album.
        if (id is null || _libraryManager.GetItemById(id.Value)?.IsVisible(user) != true)
        {
            return NotFound();
        }

        // Albums since deleted or hidden from this user are left out.
        var previous = _service.GetPrevious(libraryId)
            .Where(p => _libraryManager.GetItemById(p.AlbumId) is { } album && album.IsVisible(user))
            .Select(p => new { Id = p.AlbumId, p.Date });
        return Ok(new { Id = id.Value, Previous = previous });
    }
}
