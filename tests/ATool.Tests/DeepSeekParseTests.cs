using System.Net;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>用官方文档示例 JSON 与伪造响应验证解析与错误归类。</summary>
public class DeepSeekParseTests
{
    // 官方文档示例：https://api-docs.deepseek.com/zh-cn/api/get-user-balance
    private const string OfficialSample =
        """{"is_available": true, "balance_infos": [{"currency": "CNY", "total_balance": "110.00", "granted_balance": "10.00", "topped_up_balance": "100.00"}]}""";

    [Fact]
    public void ParseResponse_官方样例_成功且金额为decimal()
    {
        var r = DeepSeekClient.ParseResponse(200, OfficialSample);
        Assert.True(r.Success);
        Assert.Equal(110.00m, r.TotalBalance);
        Assert.Equal(10.00m, r.Granted);
        Assert.Equal(100.00m, r.ToppedUp);
        Assert.Equal("CNY", r.Currency);
        Assert.Equal("", r.Error);
    }

    [Fact]
    public void ParseResponse_401_归类为HTTP错误()
    {
        var r = DeepSeekClient.ParseResponse(401, """{"error":{"message":"Authentication Fails"}}""");
        Assert.False(r.Success);
        Assert.Contains("401", r.Error);
    }

    [Fact]
    public void ParseResponse_空balance_infos_归类格式异常()
    {
        var r = DeepSeekClient.ParseResponse(200, """{"is_available": true, "balance_infos": []}""");
        Assert.False(r.Success);
        Assert.Contains("格式异常", r.Error);
    }

    [Fact]
    public void ParseResponse_金额非数字_归类格式异常()
    {
        var r = DeepSeekClient.ParseResponse(200, """{"is_available": true, "balance_infos": [{"currency": "CNY", "total_balance": "abc"}]}""");
        Assert.False(r.Success);
        Assert.Contains("格式异常", r.Error);
    }

    [Fact]
    public void ParseResponse_无granted字段_可空处理()
    {
        var r = DeepSeekClient.ParseResponse(200, """{"is_available": true, "balance_infos": [{"currency": "USD", "total_balance": "5.50"}]}""");
        Assert.True(r.Success);
        Assert.Equal(5.50m, r.TotalBalance);
        Assert.Null(r.Granted);
        Assert.Null(r.ToppedUp);
        Assert.Equal("USD", r.Currency);
    }

    [Fact]
    public void GetBalanceAsync_超时_返回超时错误不重试()
    {
        var handler = new TimeoutHandler(); // 永不完成 → HttpClient.Timeout 触发
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(200) };
        var client = new DeepSeekClient(http);
        var r = client.GetBalanceAsync("sk-test", CancellationToken.None).GetAwaiter().GetResult();
        Assert.False(r.Success);
        Assert.Contains("超时", r.Error);
    }

    [Fact]
    public void GetBalanceAsync_成功响应_走完整链路()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, OfficialSample);
        using var http = new HttpClient(handler);
        var client = new DeepSeekClient(http);
        var r = client.GetBalanceAsync("sk-test", CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(r.Success);
        Assert.Equal(110.00m, r.TotalBalance);
        // 验证请求带 Bearer 头
        Assert.Equal("Bearer sk-test", handler.LastRequest?.Headers.Authorization?.ToString());
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); // 永不完成 → HttpClient.Timeout 触发
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
