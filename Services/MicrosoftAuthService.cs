using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using System.IO;
using System.Security.Cryptography;

namespace JustLauncher.Services;

public class MicrosoftAuthService
{
    private static readonly string ClientId = "c36a9fb6-4f2a-41ff-90bd-ae7cc92031eb"; // Using Prism Launcher's ID until I figure out why Microsoft hates me
    
    // JustLauncher appID: 83abb77c-ab6b-447e-8875-e7a579cc628a BUT I CAN'T USE IT CUZ MICROSOFT HATES ME OR SOMETHING, I'M ALREADY AN ID@XBOX DEV AND HAVE ALL THE STUFF SET UP ON AZURE IDK WHAT TO DO
    private static readonly string Scope = "XboxLive.signin offline_access";

    public static MicrosoftAuthService Instance { get; } = new();

    private async Task CheckResponseAsync(HttpResponseMessage response, string step)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Microsoft Auth Failure at {step}: {response.StatusCode} - {content}");
        }
    }

    public class PkcePair
    {
        public string Verifier { get; set; } = "";
        public string Challenge { get; set; } = "";
    }

    public PkcePair GeneratePkcePair()
    {
        var verifier = GenerateRandomString(64);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));
        var challenge = Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new PkcePair { Verifier = verifier, Challenge = challenge };
    }

    private string GenerateRandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public string GetAuthorizationUrl(string challenge, string redirectUri)
    {
        var query = new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "response_type", "code" },
            { "redirect_uri", redirectUri },
            { "response_mode", "query" },
            { "scope", Scope },
            { "code_challenge", challenge },
            { "code_challenge_method", "S256" }
        };

        var queryString = string.Join("&", query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return $"https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?{queryString}";
    }

    public async Task<string> ListenForCodeAsync(int port)
    {
        var listener = new System.Net.HttpListener();
        var redirectUri = $"http://localhost:{port}/";
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        try
        {
            var context = await listener.GetContextAsync();
            var code = context.Request.QueryString["code"];

            using var writer = new System.IO.StreamWriter(context.Response.OutputStream);
            if (!string.IsNullOrEmpty(code))
            {
                await writer.WriteAsync("<html><body><h1>Success!</h1><p>You can close this tab now and return to JustLauncher.</p></body></html>");
                context.Response.StatusCode = 200;
            }
            else
            {
                await writer.WriteAsync("<html><body><h1>Error</h1><p>No authorization code found in the request.</p></body></html>");
                context.Response.StatusCode = 400;
            }
            await writer.FlushAsync();
            context.Response.Close();

            if (string.IsNullOrEmpty(code))
                throw new Exception("Authorization failed: No code received.");

            return code;
        }
        finally
        {
            listener.Stop();
        }
    }

    public async Task<MsTokenResponse> ExchangeCodeForTokenAsync(string code, string verifier, string redirectUri)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "scope", Scope },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "code_verifier", verifier }
        });

        var response = await HttpClientManager.Instance.PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", content);
        await CheckResponseAsync(response, "ExchangeCode");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MsTokenResponse>(json)!;
    }

    public async Task<MsTokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "scope", Scope },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        });

        var response = await HttpClientManager.Instance.PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", content);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MsTokenResponse>(json);
        }
        return null;
    }

    public async Task<XboxAuthResponse> AuthenticateWithXboxAsync(string msToken)
    {
        var payload = new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={msToken}"
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await HttpClientManager.Instance.PostAsync("https://user.auth.xboxlive.com/user/authenticate", content);
        await CheckResponseAsync(response, "XboxAuthenticate");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<XboxAuthResponse>(json)!;
    }

    public async Task<XboxAuthResponse> AuthenticateWithXstsAsync(string xboxToken)
    {
        var payload = new
        {
            Properties = new
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { xboxToken }
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await HttpClientManager.Instance.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize", content);
        await CheckResponseAsync(response, "XstsAuthorize");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<XboxAuthResponse>(json)!;
    }

    public async Task<MinecraftAuthResponse> AuthenticateWithMinecraftAsync(string xstsToken, string uhs)
    {
        var payload = new
        {
            identityToken = $"XBL3.0 x={uhs};{xstsToken}"
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await HttpClientManager.Instance.PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox", content);
        await CheckResponseAsync(response, "MinecraftLogin");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MinecraftAuthResponse>(json)!;
    }

    public async Task<MinecraftProfileResponse> GetMinecraftProfileAsync(string mcToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mcToken);

        var response = await HttpClientManager.Instance.SendAsync(request);
        await CheckResponseAsync(response, "GetMinecraftProfile");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MinecraftProfileResponse>(json)!;
    }
}
