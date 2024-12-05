// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Net;

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.Network;

public abstract class AsyncTcpSocketClient : ISocketClient
{
    public event ISocketClient.SocketHandler? Disconnected;
    public event ISocketClient.SocketHandler? Connected;

    private readonly Socket socket;
    private readonly byte[] receiveBuffer = GC.AllocateArray<byte>(length: 65527, pinned: true);
    private readonly PooledByteBuffer buffer = new();

    private int expectedPacketLength;
    private int receivedBytes;

    private volatile bool connected;

    public bool IsConnected => this.connected;

    protected AsyncTcpSocketClient()
    {
        // TODO: Configure tcp keep alive
        this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        // TODO: Dual mode
    }

    public async ValueTask ConnectAsync(string hostnameOrIp, int port, CancellationToken cancellationToken = default)
    {
        if (this.connected)
        {
            return;
        }

        IPAddress[] ipAddresses = await Dns.GetHostAddressesAsync(hostnameOrIp, cancellationToken);
        // TODO: Decide which to use. Prefer IPv4 or IPv6 addresses?
        IPAddress? ipAddress = ipAddresses.FirstOrDefault() ?? throw new ArgumentException("Only IPv4 addresses are currently supported");
        await this.socket.ConnectAsync(ipAddress, port, cancellationToken);

        this.connected = true;

        this.Connected?.Invoke();

        _ = ReceiveAsync();
    }

    public async ValueTask SendAsync(IByteBuffer byteBuffer, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await this.socket.SendAsync(new ReadOnlyMemory<byte>(byteBuffer.Bytes, 0, byteBuffer.Size), SocketFlags.None, cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            await this.DisconnectAsync(cancellationToken);
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    protected abstract void OnPacketReceived(IByteBuffer byteBuffer);

    private async Task ReceiveAsync()
    {
        try
        {
            while (this.connected)
            {
                int received = await socket.ReceiveAsync(receiveBuffer.AsMemory(), SocketFlags.None);
                if (received == 0)
                {
                    await this.DisconnectAsync();
                    return;
                }


                this.expectedPacketLength = BinaryPrimitives.ReadInt32BigEndian(receiveBuffer);
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

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!this.connected)
        {
            return;
        }

        try
        {
            await this.socket.DisconnectAsync(reuseSocket: true, cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignored
        }

        this.connected = false;

        this.Disconnected?.Invoke();
    }

    public void Dispose()
    {
        this.socket.Dispose();
        this.buffer.Dispose();
        GC.SuppressFinalize(this);
    }
}
