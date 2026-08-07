using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace ATool.Services;

public sealed record BalanceResult(
    bool Success, decimal TotalBalance, decimal? Granted, decimal? ToppedUp, string Currency, string Error)
{
    public static BalanceResult Ok(decimal total, decimal? granted, decimal? topped, string currency)
        => new(true, total, granted, topped, currency, "");
    public static BalanceResult Fail(string error) => new(false, 0, null, null, "", error);
}

/// <summary>
/// DeepSeek 余额客户端。单请求 30 秒超时、超时后不重试（界面显示超时错误）。
/// 金额字段为 string（如 "110.00"），需 InvariantCulture 解析。
/// </summary>
public sealed class DeepSeekClient
{
    private static readonly HttpClient DefaultHttp = new() { Timeout = TimeSpan.FromSeconds(30) };
    public const string BalanceEndpoint = "https://api.deepseek.com/user/balance";

    private readonly HttpClient _http;

    public DeepSeekClient() : this(DefaultHttp) { }

    public DeepSeekClient(HttpClient http) => _http = http;

    public async Task<BalanceResult> GetBalanceAsync(string apiKey, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, BalanceEndpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            return ParseResponse((int)resp.StatusCode, json);
        }
        catch (OperationCanceledException)
        {
            return BalanceResult.Fail("超时（30秒）"); // HttpClient.Timeout 兜底 + 外部 ct 双保险，超时不重试
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DeepSeek 余额请求异常");
            return BalanceResult.Fail($"请求失败: {ex.Message}");
        }
    }

    internal static BalanceResult ParseResponse(int statusCode, string json)
    {
        if (statusCode is < 200 or >= 300)
            return BalanceResult.Fail($"HTTP {statusCode} {Truncate(json, 200)}");
        BalanceDto? dto;
        try { dto = JsonSerializer.Deserialize<BalanceDto>(json); }
        catch (JsonException)
        {
            return BalanceResult.Fail("响应格式异常: " + Truncate(json, 200));
        }
        if (dto?.BalanceInfos is not { Count: > 0 } infos
            || !decimal.TryParse(infos[0].TotalBalance, NumberStyles.Float, CultureInfo.InvariantCulture, out var total))
            return BalanceResult.Fail("响应格式异常: " + Truncate(json, 200));
        return BalanceResult.Ok(total, TryParse(infos[0].GrantedBalance), TryParse(infos[0].ToppedUpBalance), infos[0].Currency ?? "CNY");
    }

    private static decimal? TryParse(string? s) =>
        s is not null && decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];

    private sealed class BalanceDto
    {
        [JsonPropertyName("is_available")] public bool IsAvailable { get; set; }
        [JsonPropertyName("balance_infos")] public List<BalanceInfoDto>? BalanceInfos { get; set; }
    }

    private sealed class BalanceInfoDto
    {
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("total_balance")] public string? TotalBalance { get; set; }
        [JsonPropertyName("granted_balance")] public string? GrantedBalance { get; set; }
        [JsonPropertyName("topped_up_balance")] public string? ToppedUpBalance { get; set; }
    }
}
