using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEngine;

namespace UIToolkitMcpPreviewServer.Server
{
    internal sealed class LoopbackServer : IDisposable
    {
        private const int MaximumRequestCharacters = 4 * 1024 * 1024;
        private readonly string _token;
        private readonly Func<RequestEnvelope, ResponseEnvelope> _handler;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private TcpListener _listener;
        private Task _acceptTask;

        internal int Port { get; private set; }

        internal LoopbackServer(string token, Func<RequestEnvelope, ResponseEnvelope> handler)
        {
            _token = token;
            _handler = handler;
        }

        internal void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start(8);
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptTask = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch (Exception) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, new UTF8Encoding(false), false, 8192, true))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 8192, true) { AutoFlush = true })
            {
                ResponseEnvelope response;
                try
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null || line.Length > MaximumRequestCharacters)
                    {
                        response = ResponseEnvelope.Failure(null, "invalid_request", "Request is empty or exceeds the 4 MiB limit.");
                    }
                    else
                    {
                        var request = Json.Deserialize<RequestEnvelope>(line);
                        if (!FixedTimeEquals(request.token, _token))
                        {
                            response = ResponseEnvelope.Failure(request.id, "unauthorized", "The endpoint token is invalid.");
                        }
                        else
                        {
                            response = await AwaitWithTimeout(request);
                        }
                    }
                }
                catch (Exception exception)
                {
                    response = ResponseEnvelope.Failure(null, "transport_error", exception.Message, exception.ToString());
                }

                await writer.WriteLineAsync(Json.Serialize(response));
            }
        }

        private async Task<ResponseEnvelope> AwaitWithTimeout(RequestEnvelope request)
        {
            var work = MainThreadDispatcher.Enqueue(() => _handler(request));
            var timeout = Task.Delay(TimeSpan.FromSeconds(30));
            var completed = await Task.WhenAny(work, timeout);
            if (completed != work)
                return ResponseEnvelope.Failure(request.id, "unity_timeout", "Unity did not complete the request within 30 seconds.");

            try
            {
                return (ResponseEnvelope)await work;
            }
            catch (Exception exception)
            {
                return ResponseEnvelope.Failure(request.id, "unity_error", exception.Message, exception.ToString());
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            try
            {
                _listener?.Stop();
                _acceptTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"UI Toolkit MCP Preview Server shutdown warning: {exception.Message}");
            }
            _cancellation.Dispose();
        }
    }
}

