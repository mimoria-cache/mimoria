// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Net;

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Server.Network
{
    public abstract class AsyncTcpSocketServer : ISocketServer
    {
        private readonly Socket socket;
        private ulong connections;

        public ulong Connections => Interlocked.Read(ref this.connections);

        protected AsyncTcpSocketServer()
        {
            // TODO: Keep alive
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // TODO: Dual mode
        }

        public void Start(string ip, ushort port, ushort backlog = 50)
        {
            this.socket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
            this.socket.Listen(backlog);

            _ = this.AcceptAsync();
        }

        private async Task AcceptAsync()
        {
            while (this.socket.IsBound)
            {
                Socket clientSocket = await this.socket.AcceptAsync();
                clientSocket.NoDelay = true;

                Interlocked.Increment(ref this.connections);

                var tcpConnection = new TcpConnection(this, clientSocket, clientSocket.RemoteEndPoint!);

                this.HandleOpenConnection(tcpConnection);

                _ = this.ReceiveAsync(tcpConnection);
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

                    tcpConnection.ExpectedPacketLength = BinaryPrimitives.ReadInt32BigEndian(tcpConnection.ReceiveBuffer);
                    tcpConnection.ReceivedBytes = received - 4;
                    tcpConnection.ByteBuffer.WriteBytes(tcpConnection.ReceiveBuffer.AsSpan(4, received - 4));

                    while (tcpConnection.ReceivedBytes < tcpConnection.ExpectedPacketLength)
                    {
                        int bytesToReceive = Math.Min(tcpConnection.ExpectedPacketLength - tcpConnection.ReceivedBytes, tcpConnection.ReceiveBuffer.Length);
                        
                        received = await tcpConnection.Socket.ReceiveAsync(tcpConnection.ReceiveBuffer.AsMemory(0, bytesToReceive), SocketFlags.None);
                        if (received == 0)
                        {
                            tcpConnection.Disconnect();
                            return;
                        }

                        tcpConnection.ReceivedBytes += received;
                        tcpConnection.ByteBuffer.WriteBytes(tcpConnection.ReceiveBuffer.AsSpan(0, received));
                    }

                    IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
                    byteBuffer.WriteBytes(tcpConnection.ByteBuffer.Bytes.AsSpan(0, tcpConnection.ExpectedPacketLength));

                    await this.HandlePacketReceived(tcpConnection, byteBuffer);

                    tcpConnection.ByteBuffer.Clear();
                }
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                tcpConnection.Disconnect();
            }
            catch (Exception)
            {
                // TODO: What to do? If we ignore other exceptions then they are silently dropped
                // because we are not awaiting this method
            }
        }

        protected abstract ValueTask HandlePacketReceived(TcpConnection tcpConnection, IByteBuffer byteBuffer);

        protected abstract void HandleOpenConnection(TcpConnection tcpConnection);
        protected abstract void HandleCloseConnection(TcpConnection tcpConnection);

        internal void DecrementConnections(TcpConnection tcpConnection)
        {
            Interlocked.Decrement(ref this.connections);
            this.HandleCloseConnection(tcpConnection);
        }

        public void Stop()
        {
            try
            {
                this.socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                // Ignored
            }
            finally
            {
                this.socket.Close();
            }
        }
    }
}
