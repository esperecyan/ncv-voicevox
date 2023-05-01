using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Esperecyan.NCVVoicevox;

internal class ProxyServer
{
    internal class ProxyEventArgs : EventArgs
    {
        internal readonly HttpResponseMessage Response;

        internal ProxyEventArgs(HttpResponseMessage response)
        {
            this.Response = response;
        }
    }

    internal delegate void ProxyEventHandler(object sender, ProxyEventArgs e);

    private static readonly HttpClient Client;

    internal event ProxyEventHandler ProxyEvent = (_, _) => { };

    static ProxyServer()
    {
        ProxyServer.Client = new HttpClient();
    }

    private static async Task<HttpResponseMessage> Proxy(HttpListenerRequest request, Uri uri)
    {
        var requestMessage = new HttpRequestMessage(new HttpMethod(request.HttpMethod), uri);

        foreach (var name in request.Headers.AllKeys)
        {
            if (name == null || new[] { "content-length", "content-type" }.Contains(name.ToLowerInvariant()))
            {
                continue;
            }
            requestMessage.Headers.Add(name, request.Headers[name]);
        }

        using var content = new StreamContent(request.InputStream);
        if (request.ContentType != null)
        {
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
        }
        requestMessage.Content = content;

        return await ProxyServer.Client.SendAsync(requestMessage);
    }

    internal ProxyServer(int port, int destinationPort)
    {
        this.Listen(port, destinationPort);
    }

    private async void Listen(int port, int destinationPort)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        while (true)
        {
            var context = await listener.GetContextAsync();
            try
            {
                var responseMessage = await ProxyServer.Proxy(context.Request, new UriBuilder(context.Request.Url!)
                {
                    Port = destinationPort,
                }.Uri);

                this.ProxyEvent(this, new ProxyEventArgs(responseMessage));

                context.Response.StatusCode = (int)responseMessage.StatusCode;
                var headers = responseMessage.Headers;
                foreach (var (name, values) in headers)
                {
                    foreach (var value in values)
                    {
                        context.Response.AppendHeader(name, value);
                    }
                }
                context.Response.Close(await responseMessage.Content.ReadAsByteArrayAsync(), willBlock: true);
            }
            finally
            {
                context.Response.Close();
            }
        }
    }
}
