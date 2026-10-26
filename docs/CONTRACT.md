# Album of the Day — design contract

Target: Jellyfin 12.2.0 (NuGet Jellyfin.Controller 12.2.0, net10.0), also runs on 12.4.
No plugin dependencies.

## Rules
- One pick per music library per server-local day, shared by every user who can see that library.
- Pick is lazy: made on the first request of the day.
- No repeats within a cycle: an album is not picked again until every eligible album in that
  library has been picked; then the cycle resets.
- Singles are never picked. Single = 1-3 tracks AND total runtime < 30 minutes
  (Spotify/Apple rule).

## API
`GET /AlbumOfTheDay/{libraryId}` — `[Authorize]`
- 200 `{ "Id": "<album guid>" }`
- 403 if the calling user cannot see the library
- 404 if the library has no eligible album

## UI
An `IStartupFilter` middleware inlines `Web/albumOfTheDay.js` (embedded resource) before `</body>`
in every jellyfin-web `index.html` response (`/web/`, `/web/index.html`, behind any base URL). The script adds "Album of the Day" as the first entry in the
music library view menu (`#library-view-menu`, before "Albums" and "Suggestions"). It is the default
view when a library is opened (URL without `&tab=N`) and shows the album centered with a Play button.
