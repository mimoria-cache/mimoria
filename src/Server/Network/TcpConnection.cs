// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Sockets;

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Server.Network;

public sealed class TcpConnection
{
    private const int DefaultBufferSize = 65527;
 
    public Socket Socket { get; set; }
    // TODO: Configurable?
    public byte[] ReceiveBuffer { get; } = GC.AllocateArray<byte>(length: DefaultBufferSize, pinned: true);
    public IByteBuffer ByteBuffer { get; } = new PooledByteBuffer();
    public int ExpectedPacketLength { get; set; }
    public int ReceivedBytes { get; set; }
    public bool Authenticated { get; set; }
    public EndPoint RemoteEndPoint { get; private set; }
    public bool Connected => this.connected;

    private readonly AsyncTcpSocketServer tcpSocketServer;

    private volatile bool connected = true;

    public TcpConnection(AsyncTcpSocketServer asyncTcpSocketServer, Socket socket, EndPoint remoteEndPoint)
        => (this.tcpSocketServer, this.Socket, this.RemoteEndPoint) = (asyncTcpSocketServer, socket, remoteEndPoint);

    public async ValueTask SendAsync(IByteBuffer byteBuffer)
    {
        try
        {
            _ = await this.Socket.SendAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size), SocketFlags.None);
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
        if (!this.connected)
        {
            return;
        }

        this.connected = false;

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
            this.ByteBuffer.Dispose();

            this.tcpSocketServer.DecrementConnections(this);
        }
    }
}
