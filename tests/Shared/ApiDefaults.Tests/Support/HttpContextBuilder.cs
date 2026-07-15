using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace ApiDefaults.Tests.Support;

internal sealed class HttpContextBuilder
{
    private readonly HeaderDictionary _headers = [];
    private readonly List<Claim> _claims = [];
    private readonly ServiceCollection _services = [];
    private Func<Stream> _requestBodyFactory = () => Stream.Null;
    private CancellationToken _requestAborted;
    private string _method = HttpMethods.Get;
    private string _path = "/";
    private string? _contentType;
    private long? _contentLength;
    private bool _https;

    public HttpContextBuilder WithMethod(string method)
    {
        _method = method;
        return this;
    }

    public HttpContextBuilder WithPath(string path)
    {
        _path = path;
        return this;
    }

    public HttpContextBuilder WithHttps(bool enabled = true)
    {
        _https = enabled;
        return this;
    }

    public HttpContextBuilder WithBody(string body, string? contentType = "application/json")
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        _requestBodyFactory = () => new MemoryStream(bytes);
        _contentLength = bytes.Length;
        _contentType = contentType;
        return this;
    }

    public HttpContextBuilder WithBody(Stream body, long? contentLength = null, string? contentType = "application/json")
    {
        _requestBodyFactory = () => body;
        _contentLength = contentLength;
        _contentType = contentType;
        return this;
    }

    public HttpContextBuilder WithContentLength(long? contentLength)
    {
        _contentLength = contentLength;
        return this;
    }

    public HttpContextBuilder WithContentType(string contentType)
    {
        _contentType = contentType;
        return this;
    }

    public HttpContextBuilder WithHeader(string name, StringValues value)
    {
        _headers[name] = value;
        return this;
    }

    public HttpContextBuilder WithClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    public HttpContextBuilder WithService<TService>(TService instance)
        where TService : class
    {
        _services.AddSingleton(instance);
        return this;
    }

    public HttpContextBuilder WithServices(Action<IServiceCollection> configure)
    {
        configure(_services);
        return this;
    }

    public HttpContextBuilder WithCancellationToken(CancellationToken cancellationToken)
    {
        _requestAborted = cancellationToken;
        return this;
    }

    public DefaultHttpContext Build()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = _services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(_claims, authenticationType: _claims.Count > 0 ? "Test" : null)),
            TraceIdentifier = "trace-test"
        };
        context.Request.Method = _method;
        context.Request.Path = _path;
        context.Request.Body = _requestBodyFactory();
        context.Request.ContentType = _contentType;
        context.Request.ContentLength = _contentLength;
        context.Request.Scheme = _https ? "https" : "http";
        context.RequestAborted = _requestAborted;
        context.Response.Body = new MemoryStream();

        foreach (KeyValuePair<string, StringValues> header in _headers)
        {
            context.Request.Headers[header.Key] = header.Value;
        }

        return context;
    }
}
