using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JustLauncher.Services;

public class YggdrasilAuthService
{
    public static YggdrasilAuthService Instance { get; } = new();

    public async Task<YggdrasilAuthenticateResponse> AuthenticateAsync(string username, string password, string baseUrl)
    {
        var payload = new YggdrasilAuthenticateRequest
        {
            Username = username,
            Password = password,
            ClientToken = Guid.NewGuid().ToString(),
            RequestUser = true
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        string url = baseUrl.TrimEnd('/') + "/authenticate";
        
        var response = await HttpClientManager.Instance.PostAsync(url, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            try 
            {
                var error = JsonSerializer.Deserialize<YggdrasilError>(responseJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                if (error != null && !string.IsNullOrEmpty(error.ErrorMessage))
                {
                    throw new Exception(error.ErrorMessage);
                }
            }
            catch (Exception ex) when (ex is not Exception)
            {
            }

            throw new Exception($"Login failed ({response.StatusCode}). Please verify your credentials. If you have 2FA enabled, use 'password:code' as your password.");
        }

        return JsonSerializer.Deserialize<YggdrasilAuthenticateResponse>(responseJson, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        })!;
    }
}

public class YggdrasilAuthenticateRequest
{
    public YggdrasilAgent Agent { get; set; } = new();
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string ClientToken { get; set; } = default!;
    public bool RequestUser { get; set; }
}

public class YggdrasilAgent
{
    public string Name { get; set; } = "Minecraft";
    public int Version { get; set; } = 1;
}

public class YggdrasilAuthenticateResponse
{
    public string AccessToken { get; set; } = default!;
    public string ClientToken { get; set; } = default!;
    public YggdrasilProfile SelectedProfile { get; set; } = default!;
    public List<YggdrasilProfile> AvailableProfiles { get; set; } = new();
}

public class YggdrasilProfile
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}

public class YggdrasilError
{
    public string Error { get; set; } = default!;
    public string ErrorMessage { get; set; } = default!;
    public string Cause { get; set; } = default!;
}
