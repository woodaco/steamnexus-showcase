using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SteamProject.Services;

namespace NUnit_Tests.ServiceTesting
{
    /// <summary>
    /// Proves the server-side OpenAI summary cache: identical input text yields exactly one underlying
    /// OpenAI call (the rest are cache hits), short/long are cached independently, and failures are not
    /// cached so they can be retried. Deterministic — never calls the real OpenAI API.
    /// </summary>
    public class CachingOpenAiApiServiceTests
    {
        private sealed class CountingOpenAi : IOpenAiApiService
        {
            public int ShortCalls;
            public int LongCalls;
            public bool ReturnNull;

            public Task<string> SummarizeNewsShortAsync(string s)
            {
                ShortCalls++;
                return Task.FromResult(ReturnNull ? null! : $"short:{s}");
            }

            public Task<string> SummarizeNewsLongAsync(string s)
            {
                LongCalls++;
                return Task.FromResult(ReturnNull ? null! : $"long:{s}");
            }
        }

        private static CachingOpenAiApiService Make(IOpenAiApiService inner) =>
            new CachingOpenAiApiService(
                inner,
                new MemoryCache(new MemoryCacheOptions()),
                Mock.Of<ILogger<CachingOpenAiApiService>>());

        [Test]
        public async Task SameInput_CallsOpenAiOnce_AndReturnsTheCachedResult()
        {
            var inner = new CountingOpenAi();
            var svc = Make(inner);

            var first = await svc.SummarizeNewsShortAsync("Patch notes v1.2 — balance changes");
            var second = await svc.SummarizeNewsShortAsync("Patch notes v1.2 — balance changes");

            Assert.That(inner.ShortCalls, Is.EqualTo(1), "second identical summary must hit the cache");
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public async Task DifferentInput_CallsOpenAiEachTime()
        {
            var inner = new CountingOpenAi();
            var svc = Make(inner);

            await svc.SummarizeNewsShortAsync("article A");
            await svc.SummarizeNewsShortAsync("article B");

            Assert.That(inner.ShortCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task ShortAndLong_AreCachedUnderSeparateKeys()
        {
            var inner = new CountingOpenAi();
            var svc = Make(inner);

            await svc.SummarizeNewsShortAsync("same text");
            await svc.SummarizeNewsLongAsync("same text");

            Assert.That(inner.ShortCalls, Is.EqualTo(1));
            Assert.That(inner.LongCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task NullResult_IsNotCached_SoItIsRetried()
        {
            var inner = new CountingOpenAi { ReturnNull = true };
            var svc = Make(inner);

            await svc.SummarizeNewsShortAsync("x");
            await svc.SummarizeNewsShortAsync("x");

            Assert.That(inner.ShortCalls, Is.EqualTo(2), "a failed (null) summary must not be cached");
        }
    }
}
