using CsAgentUI;
using CsAgentUI.Shared;

namespace CsAgentUI.Endpoints;

public static class ApiEndpoints
{
    // Maximum image upload size: 10 MB
    private const long MaxImageBytes = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapEndpoints(
        this IEndpointRouteBuilder app,
        string memoryFile,
        string? modelOverride = null,
        string? mcpUrl = null,
        RetryPolicy? retry = null)
    {
        // ── Shared per-request confirmation broker ───────────────────────────
        // One broker lives for the lifetime of the server. Only one agent runs
        // at a time per server instance, so a single broker is sufficient.
        var broker = new ConfirmationBroker();

        // ── POST /api/confirm — resolve a pending confirmation ────────────────
        app.MapPost("/api/confirm", async (HttpContext ctx) =>
        {
            using var sr = new StreamReader(ctx.Request.Body);
            var body = await sr.ReadToEndAsync();
            // Body is plain "true" or "false"
            var allow = body.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            var resolved = broker.Resolve(allow);
            ctx.Response.StatusCode = resolved ? 200 : 409; // 409 = nothing pending
            await ctx.Response.WriteAsync(resolved ? "ok" : "no pending confirmation");
        });

        // ── GET /api/chat — text-only prompt (legacy / EventSource path) ──────
        app.MapGet("/api/chat", async (HttpContext ctx, string prompt) =>
        {
            await RunChatAsync(ctx, prompt, imageBase64: null, imageMime: null,
                               memoryFile, modelOverride, mcpUrl, retry, broker);
        });

        // ── POST /api/chat — multipart: prompt + optional image ───────────────
        app.MapPost("/api/chat", async (HttpContext ctx) =>
        {
            // Must be multipart/form-data
            if (!ctx.Request.HasFormContentType)
            {
                ctx.Response.StatusCode = 415;
                await ctx.Response.WriteAsync("Expected multipart/form-data");
                return;
            }

            IFormCollection form;
            try
            {
                form = await ctx.Request.ReadFormAsync();
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync($"Form read error: {ex.Message}");
                return;
            }

            var prompt = form["prompt"].ToString();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Missing 'prompt' field");
                return;
            }

            // Optional image file
            string? imageBase64 = null;
            string? imageMime = null;

            var file = form.Files.GetFile("image");
            if (file is { Length: > 0 })
            {
                if (file.Length > MaxImageBytes)
                {
                    ctx.Response.StatusCode = 413;
                    await ctx.Response.WriteAsync($"Image too large (max {MaxImageBytes / 1024 / 1024} MB)");
                    return;
                }

                // Determine MIME type — trust the extension, fall back to content-type
                imageMime = ResolveMimeType(file.FileName, file.ContentType);

                if (!IsSupportedImageMime(imageMime))
                {
                    ctx.Response.StatusCode = 415;
                    await ctx.Response.WriteAsync($"Unsupported image type: {imageMime}");
                    return;
                }

                // Read and Base64-encode — no JsonSerializer involved, AOT-safe
                using var ms = new MemoryStream((int)file.Length);
                await file.CopyToAsync(ms);
                imageBase64 = Convert.ToBase64String(ms.ToArray());
            }

            await RunChatAsync(ctx, prompt, imageBase64, imageMime,
                               memoryFile, modelOverride, mcpUrl, retry, broker);
        });

        return app;
    }

    // ── Shared agent runner ───────────────────────────────────────────────────

    private static async Task RunChatAsync(
        HttpContext ctx,
        string prompt,
        string? imageBase64,
        string? imageMime,
        string memoryFile,
        string? modelOverride,
        string? mcpUrl,
        RetryPolicy? retry,
        ConfirmationBroker broker)
    {
        ctx.Response.Headers.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";

        var observer = new SseObserver(ctx.Response, broker);

        var apiKey = Environment.GetEnvironmentVariable("ALBERT_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            await observer.OnError("API Key not set.");
            return;
        }

        var msgs = await MemoryStore.LoadAsync(memoryFile);
        if (msgs.Count == 0)
            msgs.Add(CodingAgent.SystemMessage(OperatingSystem.IsWindows()));

        // Build the user message: multimodal when an image is present, plain otherwise
        if (imageBase64 is not null && imageMime is not null)
        {
            msgs.Add(JsonHelpers.MultimodalMessage("user", prompt, imageBase64, imageMime));
        }
        else
        {
            msgs.Add(JsonHelpers.Message("user", prompt));
        }

        // Route to vision model when an image is present in the current prompt OR
        // anywhere in the loaded history — text-only models reject requests whose
        // history contains image_url content blocks.
        var needsVision = imageBase64 is not null || JsonHelpers.HistoryContainsImage(msgs);
        var model = modelOverride
                    ?? (needsVision ? LlmSettings.VisionModel : LlmSettings.Model);

        using var agent = new CodingAgent(
            apiKey,
            LlmSettings.Endpoint,
            model,
            new AgentOptions(Retry: retry),
            observer,
            mcpUrl);

        await agent.RunAsync(msgs, memoryFile);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the MIME type from the file extension first (more reliable than
    /// the browser-supplied Content-Type), then falls back to the supplied type.
    /// No reflection; AOT-safe.
    /// </summary>
    private static string ResolveMimeType(string fileName, string browserContentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => string.IsNullOrWhiteSpace(browserContentType)
                                     ? "application/octet-stream"
                                     : browserContentType
        };
    }

    private static bool IsSupportedImageMime(string mime) =>
        mime is "image/jpeg" or "image/png" or "image/gif" or "image/webp";

}