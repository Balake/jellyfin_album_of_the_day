using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AlbumOfTheDay;

/// <summary>Registers the index.html transformation with the File Transformation plugin on startup.</summary>
public class FileTransformationRegistration : IHostedService
{
    private readonly ILogger<FileTransformationRegistration> _logger;

    public FileTransformationRegistration(ILogger<FileTransformationRegistration> logger)
    {
        _logger = logger;
    }

    /// <summary>File Transformation callback: inlines the UI script before &lt;/body&gt;.</summary>
    public static string? IndexHtml(PatchRequestPayload payload)
    {
        using var stream = typeof(FileTransformationRegistration).Assembly
            .GetManifestResourceStream("Jellyfin.Plugin.AlbumOfTheDay.Web.albumOfTheDay.js")!;
        using var reader = new StreamReader(stream);
        return payload.Contents?.Replace("</body>", "<script>" + reader.ReadToEnd() + "</script></body>", StringComparison.Ordinal);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var register = AssemblyLoadContext.All
            .SelectMany(c => c.Assemblies)
            .FirstOrDefault(a => a.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) ?? false)
            ?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface")
            ?.GetMethod("RegisterTransformation");
        if (register is null)
        {
            _logger.LogWarning("File Transformation plugin is not installed; Album of the Day tab will not be injected");
            return Task.CompletedTask;
        }

        var json = JsonSerializer.Serialize(new
        {
            id = "e1003e4a-0d20-4da7-ba10-3c3eb3825c02",
            fileNamePattern = "index.html",
            callbackAssembly = GetType().Assembly.FullName,
            callbackClass = GetType().FullName,
            callbackMethod = nameof(IndexHtml)
        });

        // Build the Newtonsoft JObject from File Transformation's own parameter type: each plugin has its
        // own AssemblyLoadContext, so a JObject from our own Newtonsoft copy would not match.
        // A failure here must not stop Jellyfin from booting; only the UI entry is lost.
        try
        {
            var jObjectType = register.GetParameters()[0].ParameterType;
            var payload = jObjectType.GetMethod("Parse", [typeof(string)])!.Invoke(null, [json]);
            register.Invoke(null, [payload]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Album of the Day with File Transformation");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Payload File Transformation passes to the callback (deserialized by it with Newtonsoft).</summary>
public class PatchRequestPayload
{
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }
}
