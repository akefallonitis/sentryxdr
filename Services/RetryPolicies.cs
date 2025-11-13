using Polly;
using Polly.Retry;
using System.Net.Http;

namespace SentryXDR.Services
{
    public static class RetryPolicies
    {
        /// <summary>
        /// Standard retry policy for transient HTTP errors
        /// Implements exponential backoff: 2, 4, 8 seconds
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => 
                    !r.IsSuccessStatusCode || 
                    r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Console.WriteLine($"Request failed. Waiting {timespan.TotalSeconds}s before retry #{retryCount}");
                    });
        }

        /// <summary>
        /// Circuit breaker policy to prevent cascading failures
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, duration) =>
                    {
                        Console.WriteLine($"Circuit breaker opened for {duration.TotalSeconds}s");
                    },
                    onReset: () =>
                    {
                        Console.WriteLine("Circuit breaker reset");
                    });
        }

        /// <summary>
        /// Combined policy with retry and circuit breaker
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
        {
            return Policy.WrapAsync(GetRetryPolicy(), GetCircuitBreakerPolicy());
        }
    }
}
