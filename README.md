# Album of the Day (Jellyfin plugin)

Picks one album per music library per day, shared by every user who can see that library.

- No repeats until every eligible album in the library has been picked, then the cycle restarts.
- Singles are skipped: 1-3 tracks and under 30 minutes total.
- Adds "Album of the Day" as the first entry in the music library view menu (above "Albums" and "Suggestions").

Requires Jellyfin 12.2 or newer (tested on 12.4). No other plugins needed.

> **Warning:** not tested alongside plugins that use [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)
> to change the web client (Custom Tabs, Media Bar, Home Screen Sections, ...). File Transformation on its own works.

## Install

Dashboard → Plugins → Repositories → add
`https://raw.githubusercontent.com/Balake/jellyfin_album_of_the_day/main/manifest.json`,
then install "Album of the Day" from the catalog and restart Jellyfin.

## Release

1. Bump `version` and `changelog` in `build.yaml` and merge to `main`.
2. `git tag v<version> && git push origin v<version>`

The Release workflow builds the zip, attaches it to a GitHub release and adds the version to `manifest.json`.

## Build and install manually

```sh
dotnet build -c Release
mkdir -p <jellyfin-config>/plugins/AlbumOfTheDay
cp src/Jellyfin.Plugin.AlbumOfTheDay/bin/Release/net10.0/Jellyfin.Plugin.AlbumOfTheDay.dll <jellyfin-config>/plugins/AlbumOfTheDay/
```

Restart Jellyfin, then hard-refresh the web client.

## API

`GET /AlbumOfTheDay/{libraryId}` → `{ "Id": "<album id>" }`, 403 without library access, 404 if no eligible album.
