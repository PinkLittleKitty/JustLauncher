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
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        
        return client;
    });
    
    /// <summary>
    /// Gets the singleton HttpClient instance.
    /// </summary>
    public static HttpClient Instance => _instance.Value;
}
