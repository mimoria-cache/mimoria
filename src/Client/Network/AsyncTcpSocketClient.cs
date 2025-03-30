// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Sockets;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core.Network;

namespace Varelen.Mimoria.Client.Network;

/// <summary>
/// A socket client implementation that communicates asynchronously with a server using TCP sockets.
/// </summary>
public abstract class AsyncTcpSocketClient : ISocketClient
{
    // TODO: Review
    private const int DefaultTcpKeepAliveTime = 60;
    private const int DefaultTcpKeepAliveInterval = 30;
    private const int DefaultTcpKeepAliveRetryCount = 3;

    /// <inheritdoc />
    public event ISocketClient.SocketHandler? Disconnected;

    /// <inheritdoc />
    public event ISocketClient.SocketHandler? Connected;

    private Socket? socket;
    private readonly byte[] receiveBuffer = GC.AllocateArray<byte>(length: 65527, pinned: true);
    
    private readonly LengthPrefixedPacketReader lengthPrefixedPacketReader = new(ProtocolDefaults.LengthPrefixLength);

    private volatile bool connected;

    /// <inheritdoc />
    public bool IsConnected => this.connected;

    /// <inheritdoc />
    public async ValueTask ConnectAsync(string hostnameOrIp, int port, CancellationToken cancellationToken = default)
    {
        if (this.connected)
        {
            return;
        }

        IPAddress[] ipAddresses = await Dns.GetHostAddressesAsync(hostnameOrIp, cancellationToken);
        // TODO: Decide which to use. Prefer IPv4 or IPv6 addresses?
        IPAddress ipAddress = ipAddresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
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

        this.lengthPrefixedPacketReader.Reset();

        this.connected = true;

        this.Connected?.Invoke();

        _ = this.ReceiveAsync();
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Called when a packet is received.
    /// </summary>
    /// <param name="byteBuffer">The byte buffer containing the data.</param>
    protected abstract void OnPacketReceived(IByteBuffer byteBuffer);

    /// <summary>
    /// Called when the client is disconnected.
    /// </summary>
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

                foreach (var byteBuffer in this.lengthPrefixedPacketReader.TryRead(this.receiveBuffer, received))
                {
                    try
                    {
                        this.OnPacketReceived(byteBuffer);
                    }
                    catch
                    {
                        byteBuffer.Dispose();
                        throw;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            await this.DisconnectAsync();
        }
    }

    /// <inheritdoc />
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
            this.lengthPrefixedPacketReader.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.DisconnectAsync(force: true);
        
        GC.SuppressFinalize(this);
    }
}
