// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Sockets;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core.Network;

namespace Varelen.Mimoria.Server.Network;

public sealed class TcpConnection : ITcpConnection
{
    private const int DefaultBufferSize = 65535;

    public ulong Id { get; private set; }
    public Socket Socket { get; set; }
    public byte[] ReceiveBuffer { get; } = GC.AllocateArray<byte>(length: DefaultBufferSize, pinned: true);
    public LengthPrefixedPacketReader LengthPrefixedPacketReader { get; } = new(ProtocolDefaults.LengthPrefixLength);
    public bool Authenticated { get; set; }
    public EndPoint RemoteEndPoint { get; }
    public bool Connected => Volatile.Read(ref this.connected);

    private readonly AsyncTcpSocketServer tcpSocketServer;

    private bool connected = true;

    public TcpConnection(ulong id, AsyncTcpSocketServer asyncTcpSocketServer, Socket socket, EndPoint remoteEndPoint)
        => (this.Id, this.tcpSocketServer, this.Socket, this.RemoteEndPoint) = (id, asyncTcpSocketServer, socket, remoteEndPoint);

    public async ValueTask SendAsync(IByteBuffer byteBuffer)
    {
        try
        {
            await this.Socket.SendAllAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));

            this.tcpSocketServer.Metrics.IncrementBytesSent(byteBuffer.Size);
            this.tcpSocketServer.Metrics.IncrementPacketsSent();
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            this.Disconnect();
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    public void Disconnect()
    {
        if (!Interlocked.Exchange(ref this.connected, false))
        {
            return;
        }

        try
        {
            this.Socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignored
        }
        finally
        {
            this.Socket.Close();
            this.LengthPrefixedPacketReader.Dispose();

            this.tcpSocketServer.HandleCloseConnectionInternal(this);
        }
    }

    public override bool Equals(object? obj)
        => obj is TcpConnection connection
            && this.Id == connection.Id;

    public override int GetHashCode()
        => HashCode.Combine(this.Id);
}
