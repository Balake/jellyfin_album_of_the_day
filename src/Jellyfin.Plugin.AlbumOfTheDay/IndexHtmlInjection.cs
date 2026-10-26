using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.AlbumOfTheDay;

/// <summary>Inlines the UI script into every jellyfin-web index.html response.</summary>
public class IndexHtmlInjection : IStartupFilter
{
    private static readonly Lazy<string> Script = new(() =>
    {
        using var stream = typeof(IndexHtmlInjection).Assembly
            .GetManifestResourceStream("Jellyfin.Plugin.AlbumOfTheDay.Web.albumOfTheDay.js")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(InjectAsync);
        next(app);
    };

    /// <summary>jellyfin-web serves index.html at /web/ and /web/index.html, behind any configured base URL.</summary>
    public static bool IsIndexHtml(PathString path)
        => path.Value is { } p
            && (p.EndsWith("/web/", StringComparison.OrdinalIgnoreCase) || p.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase));

    public static string Inject(string html)
        => html.Replace("</body>", "<script>" + Script.Value + "</script></body>", StringComparison.Ordinal);

    private static async Task InjectAsync(HttpContext context, RequestDelegate next)
    {
        if (!IsIndexHtml(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Ask for the full uncompressed file: a compressed body can't be edited, and a 304 would let
        // the browser keep a cached copy without the script.
        context.Request.Headers.Remove(HeaderNames.AcceptEncoding);
        context.Request.Headers.Remove(HeaderNames.IfNoneMatch);
        context.Request.Headers.Remove(HeaderNames.IfModifiedSince);

        var body = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = body;
        }

        if (context.Response.StatusCode != StatusCodes.Status200OK)
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(body).ConfigureAwait(false);
            return;
        }

        var html = Encoding.UTF8.GetBytes(Inject(Encoding.UTF8.GetString(buffer.ToArray())));
        context.Response.Headers.Remove(HeaderNames.ETag);
        context.Response.Headers.Remove(HeaderNames.LastModified);
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.ContentLength = html.Length;
        await body.WriteAsync(html).ConfigureAwait(false);
    }
}
