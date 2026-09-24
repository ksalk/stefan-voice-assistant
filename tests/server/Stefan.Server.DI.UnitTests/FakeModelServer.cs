using System.Net;
using System.Net.Sockets;

namespace Stefan.Server.DI.UnitTests;

internal sealed class FakeModelServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Task _worker;

    public string Url { get; }

    private FakeModelServer(HttpListener listener, string url, Task worker)
    {
        _listener = listener;
        Url = url;
        _worker = worker;
    }

    public static FakeModelServer Start(byte[] payload, bool truncate = false)
    {
        var port = ReserveFreePort();
        var listener = new HttpListener();
        var url = $"http://127.0.0.1:{port}/model.bin";
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        return new FakeModelServer(listener, url, Serve(listener, payload, truncate));
    }

    private static async Task Serve(HttpListener listener, byte[] payload, bool truncate)
    {
        var context = await listener.GetContextAsync();
        context.Response.ContentLength64 = payload.Length;
        var length = truncate ? payload.Length / 2 : payload.Length;
        await context.Response.OutputStream.WriteAsync(payload, 0, length);
        if (truncate)
        {
            context.Response.Abort();
        }
        else
        {
            context.Response.Close();
        }
    }

    private static int ReserveFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception)
        {
        }

        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
        }
    }
}
