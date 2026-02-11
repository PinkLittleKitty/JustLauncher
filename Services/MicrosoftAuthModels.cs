using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JustLauncher.Services;

public class DeviceCodeResponse
{
    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = default!;
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = default!;
    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; set; } = default!;
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    [JsonPropertyName("interval")]
    public int Interval { get; set; }
}

public class MsTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = default!;
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = default!;
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public class XboxAuthResponse
{
    [JsonPropertyName("Token")]
    public string Token { get; set; } = default!;
    [JsonPropertyName("DisplayClaims")]
    public XboxDisplayClaims DisplayClaims { get; set; } = new();
}

public class XboxDisplayClaims
{
    [JsonPropertyName("xui")]
    public List<XboxXuiClaim> Xui { get; set; } = new();
}

public class XboxXuiClaim
{
    [JsonPropertyName("uhs")]
    public string Uhs { get; set; } = default!;
}

public class MinecraftAuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = default!;
    [JsonPropertyName("username")]
    public string Username { get; set; } = default!;
}

public class MinecraftProfileResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
}
