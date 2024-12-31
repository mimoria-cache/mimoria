// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server.Network;

using System.Collections.Frozen;

namespace Varelen.Mimoria.Server.Protocol;

public class MimoriaSocketServer : AsyncTcpSocketServer, IMimoriaSocketServer
{
    private readonly ILogger<MimoriaSocketServer> logger;
    private FrozenDictionary<Operation, Func<uint, TcpConnection, IByteBuffer, ValueTask>> operationHandlers = null!;

    public event IMimoriaSocketServer.TcpConnectionEvent? Disconnected;

    public MimoriaSocketServer(ILogger<MimoriaSocketServer> logger)
    {
        this.logger = logger;
    }

    protected override async ValueTask HandlePacketReceived(TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        var operation = (Operation)byteBuffer.ReadByte();
        uint requestId = byteBuffer.ReadUInt();

        if (!this.operationHandlers.TryGetValue(operation, out Func<uint, TcpConnection, IByteBuffer, ValueTask>? operationHandler))
        {
            byteBuffer.Dispose();
            this.logger.LogWarning("Client '{EndPoint}' sent an unsupported operation '{Operation}'", tcpConnection.RemoteEndPoint, operation);
            await SendErrorResponseAsync(tcpConnection, operation, requestId, $"Operation '{operation}' is unsupported");
            tcpConnection.Disconnect();
            return;
        }

        if (operation != Operation.Login && !tcpConnection.Authenticated)
        {
            byteBuffer.Dispose();
            await SendErrorResponseAsync(tcpConnection, operation, requestId, $"Authentication required to use operation '{operation}'");
            tcpConnection.Disconnect();
            return;
        }

        try
        {
            await operationHandler(requestId, tcpConnection, byteBuffer);
        }
        catch (ArgumentException exception)
        {
            await SendErrorResponseAsync(tcpConnection, operation, requestId, exception.Message);
            tcpConnection.Disconnect();
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Error while processing handler for operation '{Operation}' and client '{Client}'", operation, tcpConnection.RemoteEndPoint);
            await SendErrorResponseAsync(tcpConnection, operation, requestId, $"An internal server error occurred while processing handler for operation '{operation}'. See server logs for more information.");
            tcpConnection.Disconnect();
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    public void SetOperationHandlers(Dictionary<Operation, Func<uint, TcpConnection, IByteBuffer, ValueTask>> operationHandlers)
        => this.operationHandlers = operationHandlers.ToFrozenDictionary();

    private static ValueTask SendErrorResponseAsync(TcpConnection tcpConnection, Operation operation, uint requestId, string errorText)
    {
        var byteBuffer = new PooledByteBuffer(operation);
        byteBuffer.WriteUInt(requestId);
        byteBuffer.WriteByte((byte)StatusCode.Error);
        byteBuffer.WriteString(errorText);
        byteBuffer.EndPacket();

        return tcpConnection.SendAsync(byteBuffer);
    }

    protected override void HandleOpenConnection(TcpConnection tcpConnection)
    {
        this.logger.LogInformation("New connection '{RemoteEndPoint}'", tcpConnection.RemoteEndPoint);
    }

    protected override void HandleCloseConnection(TcpConnection tcpConnection)
    {
        this.Disconnected?.Invoke(tcpConnection);

        this.logger.LogInformation("Closed connection '{RemoteEndPoint}'", tcpConnection.RemoteEndPoint);
    }
}
