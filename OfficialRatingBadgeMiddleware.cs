using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.OfficialRatingBadge
{
    /// <summary>
    /// Intercepts responses to Jellyfin's own image endpoints
    /// (/Items/{itemId}/Images/{imageType}...), and if the item has an
    /// OfficialRating and the request is for a Primary (poster) image,
    /// draws a small rating badge onto the bottom-left corner using
    /// SkiaSharp, then returns the modified bytes instead of the original.
    ///
    /// This never touches the original image file on disk — it only
    /// modifies what gets sent over the wire for that one response, same
    /// pattern JellyTag and Jellyfin-Quality-Overlay use.
    /// </summary>
    public class OfficialRatingBadgeMiddleware
    {
        // Match multiple URL patterns used by different clients:
        // - /Items/{id}/Images/Primary (standard)
        // - /Items/{id}/Images/Primary/0 (with index)
        // - /emby/Items/{id}/Images/Primary (Emby compatibility mode)
        private static readonly Regex ImageRouteRegex =
            new(@"^(?:/emby)?/Items/(?<itemId>[0-9a-fA-F-]{32,36})/Images/Primary",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly RequestDelegate _next;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<OfficialRatingBadgeMiddleware> _logger;

        public OfficialRatingBadgeMiddleware(RequestDelegate next, ILibraryManager libraryManager, ILogger<OfficialRatingBadgeMiddleware> logger)
        {
            _next = next;
            _libraryManager = libraryManager;
            _logger = logger;
            _logger.LogInformation("OfficialRatingBadgeMiddleware initialized");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (Plugin.Instance == null)
            {
                _logger.LogWarning("OfficialRatingBadge: Plugin.Instance is null");
                await _next(context);
                return;
            }

            if (!Plugin.Instance.Configuration.Enabled)
            {
                await _next(context);
                return;
            }

            var match = ImageRouteRegex.Match(path);
            if (!match.Success)
            {
                // Log unmatched image requests to help diagnose client-specific URL patterns
                if (path.Contains("/Images/Primary", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("OfficialRatingBadge: Unmatched Primary image path: {Path}", path);
                }

                await _next(context);
                return;
            }

            var userAgent = context.Request.Headers.UserAgent.ToString();
            _logger.LogDebug("OfficialRatingBadge: Matched path {Path}, User-Agent: {UserAgent}", path, userAgent);

            if (!Guid.TryParse(match.Groups["itemId"].Value, out var itemId))
            {
                _logger.LogWarning("OfficialRatingBadge: Failed to parse itemId from {Path}", path);
                await _next(context);
                return;
            }

            var item = _libraryManager.GetItemById(itemId);
            var rating = item?.OfficialRating;

            _logger.LogDebug("OfficialRatingBadge: Item {ItemId} has rating '{Rating}'", itemId, rating ?? "(null)");

            if (string.IsNullOrWhiteSpace(rating))
            {
                await _next(context);
                return;
            }

            // Swap in a buffer so we can capture and rewrite the response
            // body before it's flushed to the client.
            var originalBody = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await _next(context);
            }
            finally
            {
                context.Response.Body = originalBody;
            }

            buffer.Seek(0, SeekOrigin.Begin);

            if (context.Response.StatusCode != StatusCodes.Status200OK || buffer.Length == 0)
            {
                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);
                return;
            }

            byte[] badged;
            try
            {
                _logger.LogInformation("OfficialRatingBadge: Drawing badge '{Rating}' on item {ItemId}", rating, itemId);
                badged = DrawBadge(buffer.ToArray(), rating!);
                _logger.LogDebug("OfficialRatingBadge: Badge drawn successfully, {Bytes} bytes", badged.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OfficialRatingBadge: Failed to draw badge");
                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);
                return;
            }

            // Ensure Content-Type matches the re-encoded format (always JPEG)
            context.Response.ContentType = "image/jpeg";
            context.Response.ContentLength = badged.Length;
            await originalBody.WriteAsync(badged);
        }

        private static byte[] DrawBadge(byte[] originalImageBytes, string rating)
        {
            using var original = SKBitmap.Decode(originalImageBytes);
            if (original == null)
            {
                return originalImageBytes;
            }

            using var surface = SKSurface.Create(new SKImageInfo(original.Width, original.Height));
            var canvas = surface.Canvas;
            canvas.DrawBitmap(original, 0, 0);

            // Badge sizing scales with the poster so it looks consistent
            // across different poster resolutions.
            var fontSize = original.Width * 0.055f;
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };

            var textWidth = textPaint.MeasureText(rating);
            var paddingX = fontSize * 0.5f;
            var paddingY = fontSize * 0.35f;
            var badgeWidth = textWidth + paddingX * 2;
            var badgeHeight = fontSize + paddingY * 2;
            var margin = original.Width * 0.03f;

            var badgeRect = new SKRect(
                margin,
                original.Height - margin - badgeHeight,
                margin + badgeWidth,
                original.Height - margin);

            using var badgePaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 217), // ~0.85 alpha, matches Jellyfin Enhanced's badge style
                IsAntialias = true
            };
            canvas.DrawRoundRect(badgeRect, 6, 6, badgePaint);

            canvas.DrawText(
                rating,
                badgeRect.Left + paddingX,
                badgeRect.Bottom - paddingY,
                textPaint);

            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            return encoded.ToArray();
        }
    }
}
