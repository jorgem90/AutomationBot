using AutomationBot.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Telegram.Bot.Types;


public class MessageService
{
    private static int _port;
    private TcpListener _listener;
    private readonly ConcurrentDictionary<Guid, TcpClient> _clients = new();

    public event Action<TCPMessage> OnMessageReceived;


    public MessageService(int port)
    {
        _port = port;
    }

    public void StartTcpServer(CancellationToken token)
    {
        Task.Run(() => StartAsync(token));
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
                _clients[Guid.NewGuid()] = client;
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
            var message = await ReceiveMessageAsync(stream);
            OnMessageReceived.Invoke(message);
        }
    }

    private async Task<TCPMessage> ReceiveMessageAsync(NetworkStream stream)
    {
        try
        {
            byte[] lengthBuffer = new byte[4];
            int bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4);
            if (bytesRead == 0)
                return null;

            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBuffer);
            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

            byte[] messageBuffer = new byte[messageLength];
            int totalRead = 0;
            while (totalRead < messageLength)
            {
                bytesRead = await stream.ReadAsync(
                    messageBuffer,
                    totalRead,
                    messageLength - totalRead
                );
                if (bytesRead == 0)
                    return null;
                totalRead += bytesRead;
            }

            string json = Encoding.UTF8.GetString(messageBuffer);
            return JsonSerializer.Deserialize<TCPMessage>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving message: {ex.Message}");
            return null;
        }
    }

    public async Task SendMessage(NetworkStream stream, TCPMessage message)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] data = Encoding.UTF8.GetBytes(json);

        byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthPrefix);

        await stream.WriteAsync(lengthPrefix, 0, 4);
        await stream.WriteAsync(data, 0, data.Length);
        await stream.FlushAsync();
    }

    public async Task BroadcastMessageAsync(TCPMessage message)
    {
        foreach (var client in _clients.Values)
        {
            if (client.Connected)
            {
                var stream = client.GetStream();
                await SendMessage(stream, message);
            }
        }
    }

}

