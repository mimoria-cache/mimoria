// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.Protocol;

public class MimoriaSocketServer : AsyncTcpSocketServer, IMimoriaSocketServer
{
    private readonly ILogger<MimoriaSocketServer> logger;
    private readonly Dictionary<Operation, Func<uint, TcpConnection, IByteBuffer, ValueTask>> operationHandlers;

    public MimoriaSocketServer(ILogger<MimoriaSocketServer> logger)
    {
        this.logger = logger;
        this.operationHandlers = new(Enum.GetNames(typeof(Operation)).Length);
    }

    protected override async ValueTask HandlePacketReceived(TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        var operation = (Operation)byteBuffer.ReadByte();
        uint requestId = byteBuffer.ReadUInt();

        if (!this.operationHandlers.TryGetValue(operation, out Func<uint, TcpConnection, IByteBuffer, ValueTask>? operationHandler))
        {
            byteBuffer.Dispose();
            await SendErrorResponseAsync(tcpConnection, operation, requestId, $"Operation '{operation}' is unsupported");
            this.logger.LogWarning("Client '{EndPoint}' sent an unsupported operation '{Operation}'", tcpConnection.Socket.RemoteEndPoint, operation);
            return;
        }

        if (operation != Operation.Login && !tcpConnection.Authenticated)
        {
            byteBuffer.Dispose();
            await SendErrorResponseAsync(tcpConnection, operation, requestId, $"Authentication required to use operation '{operation}'");
            return;
        }

        try
        {
            await operationHandler(requestId, tcpConnection, byteBuffer);
        }
        catch (ArgumentException exception)
        {
            await SendErrorResponseAsync(tcpConnection, operation, requestId, exception.Message);
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    public void SetOperationHandler(Operation operation, Func<uint, TcpConnection, IByteBuffer, ValueTask> handler)
        => this.operationHandlers[operation] = handler;

    private static async ValueTask SendErrorResponseAsync(TcpConnection tcpConnection, Operation operation, uint requestId, string errorText)
    {
        var byteBuffer = new PooledByteBuffer(operation);
        byteBuffer.WriteUInt(requestId);
        byteBuffer.WriteByte((byte)StatusCode.Error);
        byteBuffer.WriteString(errorText);
        byteBuffer.EndPacket();

        await tcpConnection.SendAsync(byteBuffer);
    }

    protected override void HandleOpenConnection(TcpConnection tcpConnection)
    {
        this.logger.LogInformation("New connection '{RemoteEndPoint}'", tcpConnection.RemoteEndPoint);
    }

    protected override void HandleCloseConnection(TcpConnection tcpConnection)
    {
        this.logger.LogInformation("Closed connection '{RemoteEndPoint}'", tcpConnection.RemoteEndPoint);
    }
}
