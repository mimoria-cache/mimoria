// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Server.Network;

public abstract class AsyncTcpSocketServer : ISocketServer
{
    private readonly ILogger<AsyncTcpSocketServer> logger;
    private readonly ConcurrentDictionary<ulong, TcpConnection> connections;
    
    private Socket? socket;
    private ulong connectionIdCounter;
    private bool running;

    public ulong Connections => (ulong)this.connections.Count;

    protected AsyncTcpSocketServer(ILogger<AsyncTcpSocketServer> logger)
    {
        this.logger = logger;
        this.connections = [];
    }

    public void Start(string ip, ushort port, ushort backlog = 50)
    {
        if (this.running)
        {
            return;
        }

        this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        this.socket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
        this.socket.Listen(backlog);

        _ = this.AcceptAsync();

        this.running = true;
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (this.socket!.IsBound)
            {
                Socket clientSocket = await this.socket.AcceptAsync();
                clientSocket.NoDelay = true;

                ulong connectionId = Interlocked.Increment(ref this.connectionIdCounter);
                var tcpConnection = new TcpConnection(connectionId, this, clientSocket, clientSocket.RemoteEndPoint!);

                bool added = this.connections.TryAdd(connectionId, tcpConnection);
                Debug.Assert(added, $"Unable to add new connection with id '{connectionId}'");

                this.HandleOpenConnection(tcpConnection);

                _ = this.ReceiveAsync(tcpConnection);
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignore
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Unexpected error while accepting connections");
        }
    }

    private async Task ReceiveAsync(TcpConnection tcpConnection)
    {
        try
        {
            while (tcpConnection.Connected)
            {
                int received = await tcpConnection.Socket.ReceiveAsync(tcpConnection.ReceiveBuffer.AsMemory(), SocketFlags.None);
                if (received == 0)
                {
                    tcpConnection.Disconnect();
                    return;
                }

                foreach (var byteBuffer in tcpConnection.LengthPrefixedPacketReader.TryRead(tcpConnection.ReceiveBuffer, received))
                {
                    try
                    {
                        await this.HandlePacketReceivedAsync(tcpConnection, byteBuffer);
                    }
                    finally
                    {
                        byteBuffer.Dispose();
                    }
                }
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            tcpConnection.Disconnect();
        }
        catch (Exception exception)
        {
            tcpConnection.Disconnect();

            this.logger.LogError(exception, "Unexpected error while receiving from connection '{ConnectionId}' ('{ConnectionEndPoint}')", tcpConnection.Id, tcpConnection.RemoteEndPoint);
        }
    }

    protected abstract ValueTask HandlePacketReceivedAsync(TcpConnection tcpConnection, IByteBuffer byteBuffer);

    protected abstract void HandleOpenConnection(TcpConnection tcpConnection);
    protected abstract void HandleCloseConnection(TcpConnection tcpConnection);

    internal void HandleCloseConnectionInternal(TcpConnection tcpConnection)
    {
        bool removed = this.connections.TryRemove(tcpConnection.Id, out _);
        Debug.Assert(removed, $"Unable to remove connection with id '{tcpConnection.Id}'");
        
        this.HandleCloseConnection(tcpConnection);
    }

    public void Stop()
    {
        if (!this.running)
        {
            return;
        }

        this.running = false;

        try
        {
            this.socket!.Shutdown(SocketShutdown.Both);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignored
        }
        finally
        {
            this.socket!.Close();
        }

        this.socket = null;

        foreach (var (_, tcpConnection) in this.connections)
        {
            tcpConnection.Disconnect();
        }
    }
}
