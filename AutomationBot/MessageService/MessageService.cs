using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal class MessageService
{
    private static int _port;
    private static BotService _bot;
    private TcpListener _listener;

    public MessageService(int port, BotService bot)
    {
        _port = port;
        _bot = bot;
    }

    public async Task StartAsync(CancellationToken token)
    {
        _listener = new TcpListener(IPAddress.Parse("127.0.0.1"), _port);
        _listener.Start();
        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = HandleClientAsync(client, token);
            }
        }
        catch(OperationCanceledException)
        {
        }
        finally
        {
            _listener.Stop();
        }
    }

    public async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string message = await reader.ReadToEndAsync();
            if(string.IsNullOrWhiteSpace(message))
                return;
            await _bot.HandleExternalMessage(message);
        }
    }
}

