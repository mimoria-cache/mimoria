// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.Network;

public abstract class AsyncTcpSocketClient : ISocketClient
{
    // TODO: Review
    private const int DefaultTcpKeepAliveTime = 60;
    private const int DefaultTcpKeepAliveInterval = 30;
    private const int DefaultTcpKeepAliveRetryCount = 3;

    public event ISocketClient.SocketHandler? Disconnected;
    public event ISocketClient.SocketHandler? Connected;

    private Socket? socket;
    private readonly byte[] receiveBuffer = GC.AllocateArray<byte>(length: 65527, pinned: true);
    private readonly PooledByteBuffer buffer = new();

    private int expectedPacketLength;
    private int receivedBytes;

    private volatile bool connected;

    public bool IsConnected => this.connected;

    public async ValueTask ConnectAsync(string hostnameOrIp, int port, CancellationToken cancellationToken = default)
    {
        if (this.connected)
        {
            return;
        }

        IPAddress[] ipAddresses = await Dns.GetHostAddressesAsync(hostnameOrIp, cancellationToken);
        // TODO: Decide which to use. Prefer IPv4 or IPv6 addresses?
        IPAddress? ipAddress = ipAddresses.FirstOrDefault()
            ?? throw new ArgumentException("Only IPv4 addresses are currently supported");

        try
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, DefaultTcpKeepAliveTime);
            this.socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, DefaultTcpKeepAliveInterval);
            this.socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, DefaultTcpKeepAliveRetryCount);

            await this.socket.ConnectAsync(ipAddress, port, cancellationToken);
        }
        catch (Exception)
        {
            this.socket?.Close();
            throw;
        }

        this.buffer.Clear();

        this.connected = true;

        this.Connected?.Invoke();

        _ = this.ReceiveAsync();
    }

    public async ValueTask SendAsync(IByteBuffer byteBuffer, CancellationToken cancellationToken = default)
    {
        try
        {
            await this.socket!.SendAllAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size), cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            await this.DisconnectAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    protected abstract void OnPacketReceived(IByteBuffer byteBuffer);
    protected abstract void HandleDisconnect(bool force);

    private async Task ReceiveAsync()
    {
        try
        {
            while (this.connected)
            {
                int received = await this.socket!.ReceiveAsync(this.receiveBuffer.AsMemory(), SocketFlags.None);
                if (received == 0)
                {
                    await this.DisconnectAsync();
                    return;
                }

                this.expectedPacketLength = BinaryPrimitives.ReadInt32BigEndian(this.receiveBuffer);
                this.receivedBytes = received - 4;
                this.buffer.WriteBytes(this.receiveBuffer.AsSpan(4, received - 4));

                while (this.receivedBytes < this.expectedPacketLength)
                {
                    int bytesToReceive = Math.Min(this.expectedPacketLength - this.receivedBytes, this.receiveBuffer.Length);
                    received = await this.socket.ReceiveAsync(this.receiveBuffer.AsMemory(0, bytesToReceive), SocketFlags.None);
                    if (received == 0)
                    {
                        await this.DisconnectAsync();
                        return;
                    }

                    this.receivedBytes += received;
                    this.buffer.WriteBytes(this.receiveBuffer.AsSpan(0, received));
                }

                IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
                byteBuffer.WriteBytes(this.buffer.Bytes.AsSpan(0, this.expectedPacketLength));

                this.OnPacketReceived(byteBuffer);

                this.buffer.Clear();
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            await this.DisconnectAsync();
        }
    }

    public ValueTask DisconnectAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!this.connected)
        {
            return ValueTask.CompletedTask;
        }

        this.connected = false;

        try
        {
            this.socket?.Shutdown(SocketShutdown.Both);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignored
        }
        finally
        {
            this.socket?.Close();
        }

        this.HandleDisconnect(force);

        if (!force)
        { 
            this.Disconnected?.Invoke();
        }
        else
        {
            this.socket?.Dispose();
            this.buffer.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await this.DisconnectAsync(force: true);
        
        GC.SuppressFinalize(this);
    }
}
