using System;
using System.Net.Http;

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
}
