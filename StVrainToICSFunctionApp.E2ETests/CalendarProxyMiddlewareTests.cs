using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StVrainToICSFunctionApp.Middleware;
using StVrainToICSFunctionApp.Options;
using StVrainToICSFunctionApp.Services;
using Xunit;

namespace StVrainToICSFunctionApp.E2ETests;

public sealed class CalendarProxyMiddlewareTests
{
    [Fact]
    public async Task Forwards_path_and_query_to_upstream()
    {
        RecordingHandler handler = new();
        CalendarProxyMiddleware middleware = new(_ => Task.CompletedTask);
        CalendarProxyService proxy = new(
            new StubHttpClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(new ProxyOptions
            {
                Enabled = true,
                UpstreamBaseUrl = "https://lunchmenu.debugthings.com",
            }),
            NullLogger<CalendarProxyService>.Instance);
        DefaultHttpContext context = new();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/Lunchmenu.ics";
        context.Request.QueryString = new QueryString("?buildingId=abc&districtId=xyz");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, proxy);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.StartsWith("text/calendar", context.Response.ContentType);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("https://lunchmenu.debugthings.com/api/Lunchmenu.ics?buildingId=abc&districtId=xyz", handler.LastRequestUri!.AbsoluteUri);
        context.Response.Body.Position = 0;
        string body = new StreamReader(context.Response.Body).ReadToEnd();
        Assert.Equal("BEGIN:VCALENDAR", body);
    }

    [Fact]
    public async Task Forwards_district_building_path_to_upstream()
    {
        RecordingHandler handler = new();
        CalendarProxyMiddleware middleware = new(_ => Task.CompletedTask);
        CalendarProxyService proxy = new(
            new StubHttpClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(new ProxyOptions
            {
                Enabled = true,
                UpstreamBaseUrl = "https://lunchmenu.debugthings.com",
            }),
            NullLogger<CalendarProxyService>.Instance);
        DefaultHttpContext context = new();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/55485575-09b2-ed11-8e69-f29174b2df22/3805e0fd-bdbe-ed11-82b1-880d996bcdd8/lunchmenu.ics";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, proxy);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal(
            "https://lunchmenu.debugthings.com/55485575-09b2-ed11-8e69-f29174b2df22/3805e0fd-bdbe-ed11-82b1-880d996bcdd8/lunchmenu.ics",
            handler.LastRequestUri!.AbsoluteUri);
        context.Response.Body.Position = 0;
        string body = new StreamReader(context.Response.Body).ReadToEnd();
        Assert.Equal("BEGIN:VCALENDAR", body);
    }

    [Fact]
    public async Task Forwards_display_time_path_to_upstream()
    {
        RecordingHandler handler = new();
        CalendarProxyMiddleware middleware = new(_ => Task.CompletedTask);
        CalendarProxyService proxy = new(
            new StubHttpClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(new ProxyOptions
            {
                Enabled = true,
                UpstreamBaseUrl = "https://lunchmenu.debugthings.com",
            }),
            NullLogger<CalendarProxyService>.Instance);
        DefaultHttpContext context = new();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/55485575-09b2-ed11-8e69-f29174b2df22/3805e0fd-bdbe-ed11-82b1-880d996bcdd8/1200/lunchmenu.ics";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, proxy);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal(
            "https://lunchmenu.debugthings.com/55485575-09b2-ed11-8e69-f29174b2df22/3805e0fd-bdbe-ed11-82b1-880d996bcdd8/1200/lunchmenu.ics",
            handler.LastRequestUri!.AbsoluteUri);
        context.Response.Body.Position = 0;
        string displayBody = new StreamReader(context.Response.Body).ReadToEnd();
        Assert.Equal("BEGIN:VCALENDAR", displayBody);
    }

    [Fact]
    public async Task Forwards_school_shortcut_path_to_upstream()
    {
        RecordingHandler handler = new();
        CalendarProxyMiddleware middleware = new(_ => Task.CompletedTask);
        CalendarProxyService proxy = new(
            new StubHttpClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(new ProxyOptions
            {
                Enabled = true,
                UpstreamBaseUrl = "https://lunchmenu.debugthings.com",
            }),
            NullLogger<CalendarProxyService>.Instance);
        DefaultHttpContext context = new();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/rhe/lunchmenu";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, proxy);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("https://lunchmenu.debugthings.com/rhe/lunchmenu", handler.LastRequestUri!.AbsoluteUri);
        context.Response.Body.Position = 0;
        string body = new StreamReader(context.Response.Body).ReadToEnd();
        Assert.Equal("BEGIN:VCALENDAR", body);
    }

    [Fact]
    public async Task Forwards_fastlink_path_to_upstream()
    {
        RecordingHandler handler = new();
        CalendarProxyMiddleware middleware = new(_ => Task.CompletedTask);
        CalendarProxyService proxy = new(
            new StubHttpClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(new ProxyOptions
            {
                Enabled = true,
                UpstreamBaseUrl = "https://lunchmenu.debugthings.com",
            }),
            NullLogger<CalendarProxyService>.Instance);
        DefaultHttpContext context = new();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/fast-lynx";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, proxy);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("https://lunchmenu.debugthings.com/fast-lynx", handler.LastRequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Non_ics_paths_are_passed_through()
    {
        bool nextCalled = false;
        CalendarProxyMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        CalendarProxyService proxy = new(
            new StubHttpClientFactory(new RecordingHandler()),
            Microsoft.Extensions.Options.Options.Create(new ProxyOptions { Enabled = true, UpstreamBaseUrl = "https://lunchmenu.debugthings.com" }),
            NullLogger<CalendarProxyService>.Instance);
        DefaultHttpContext context = new();
        context.Request.Path = "/healthz";

        await middleware.InvokeAsync(context, proxy);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Upstream_failure_returns_502()
    {
        CalendarProxyMiddleware middleware = new(_ => Task.CompletedTask);
        CalendarProxyService proxy = new(
            new StubHttpClientFactory(new ThrowingHandler()),
            Microsoft.Extensions.Options.Options.Create(new ProxyOptions { Enabled = true, UpstreamBaseUrl = "https://lunchmenu.debugthings.com" }),
            NullLogger<CalendarProxyService>.Instance);
        DefaultHttpContext context = new();
        context.Request.Path = "/Lunchmenu.ics";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, proxy);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent("BEGIN:VCALENDAR", Encoding.UTF8, "text/calendar"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("origin down");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
