using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace SteamProject.Services
{
    /// <summary>
    /// Caches OpenAI news summaries server-side, keyed by a hash of the exact input text (+ summary
    /// length). Identical article content -> one OpenAI call, shared across all users and sessions
    /// (the client-side localStorage cache only helped a single browser). Cuts cost and latency.
    /// Failures (null/blank results) are never cached, so a transient error can be retried.
    /// </summary>
    public class CachingOpenAiApiService : IOpenAiApiService
    {
        private readonly IOpenAiApiService _inner;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachingOpenAiApiService> _logger;

        private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

        public CachingOpenAiApiService(IOpenAiApiService inner, IMemoryCache cache, ILogger<CachingOpenAiApiService> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public Task<string> SummarizeNewsShortAsync(string inputString)
            => GetOrCreateAsync("short", inputString, _inner.SummarizeNewsShortAsync);

        public Task<string> SummarizeNewsLongAsync(string inputString)
            => GetOrCreateAsync("long", inputString, _inner.SummarizeNewsLongAsync);

        private async Task<string> GetOrCreateAsync(string kind, string input, Func<string, Task<string>> factory)
        {
            if (string.IsNullOrWhiteSpace(input))
                return await factory(input);

            var key = $"openai-summary:{kind}:{Hash(input)}";
            if (_cache.TryGetValue(key, out string? cached) && !string.IsNullOrEmpty(cached))
            {
                _logger.LogDebug("OpenAI summary cache HIT ({Kind})", kind);
                return cached;
            }

            _logger.LogInformation("OpenAI summary cache MISS ({Kind}) - calling OpenAI", kind);
            var result = await factory(input);

            // Only cache real results; never cache a null/blank failure so it can be retried.
            if (!string.IsNullOrWhiteSpace(result))
                _cache.Set(key, result, Ttl);

            return result;
        }

        private static string Hash(string s)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes);
        }
    }
}
