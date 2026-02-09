using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace JustLauncher;

/// <summary>
/// Provides a singleton HttpClient instance to avoid socket exhaustion.
/// Creating multiple HttpClient instances can lead to port exhaustion and DNS issues.
/// </summary>
public static class HttpClientManager
{
    private static readonly Lazy<HttpClient> _instance = new(() =>
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        // Set default headers
        client.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");
        
        return client;
    });
    
    /// <summary>
    /// Gets the singleton HttpClient instance.
    /// </summary>
    public static HttpClient Instance => _instance.Value;

    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request,
        int maxRetries = 3,
        int baseDelayMs = 1000)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt <= maxRetries)
        {
            try
            {
                var response = await Instance.SendAsync(request);
                
                if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                {
                    if (attempt < maxRetries)
                    {
                        var delay = CalculateDelay(attempt, baseDelayMs);
                        await Task.Delay(delay);
                        attempt++;
                        continue;
                    }
                }
                
                return response;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                lastException = ex;
                var delay = CalculateDelay(attempt, baseDelayMs);
                await Task.Delay(delay);
                attempt++;
            }
            catch (TaskCanceledException ex) when (attempt < maxRetries && !ex.CancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                var delay = CalculateDelay(attempt, baseDelayMs);
                await Task.Delay(delay);
                attempt++;
            }
        }

        throw lastException ?? new HttpRequestException("Request failed after all retry attempts");
    }

    private static int CalculateDelay(int attempt, int baseDelayMs)
    {
        var delay = baseDelayMs * Math.Pow(2, attempt);
        return (int)Math.Min(delay, 30000);
    }
}
