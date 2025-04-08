// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using System.Collections.Frozen;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Metrics;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.Protocol;

public class MimoriaSocketServer : AsyncTcpSocketServer, IMimoriaSocketServer
{
    private readonly ILogger<MimoriaSocketServer> logger;
    private FrozenDictionary<Operation, Func<uint, TcpConnection, IByteBuffer, ValueTask>> operationHandlers = null!;

    public event IMimoriaSocketServer.TcpConnectionEvent? Disconnected;

    public MimoriaSocketServer(ILogger<MimoriaSocketServer> logger, IMimoriaMetrics mimoriaMetrics)
        : base(logger, mimoriaMetrics)
    {
        this.logger = logger;
    }

    protected override async ValueTask HandlePacketReceivedAsync(TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        var operation = (Operation)byteBuffer.ReadByte();
        uint requestId = byteBuffer.ReadUInt();

        if (!this.operationHandlers.TryGetValue(operation, out Func<uint, TcpConnection, IByteBuffer, ValueTask>? operationHandler))
        {
            this.logger.LogWarning("Client '{EndPoint}' sent an unsupported operation '{Operation}'", tcpConnection.RemoteEndPoint, operation);
            await SendErrorResponseAsync(tcpConnection, operation, requestId, $"Operation '{operation}' is unsupported");
            await tcpConnection.DisconnectAsync();
            return;
        }

        if (operation != Operation.Login && !tcpConnection.Authenticated)
        {
            await SendErrorResponseAsync(tcpConnection, operation, requestId, $"Authentication required to use operation '{operation}'");
            await tcpConnection.DisconnectAsync();
            return;
        }

        try
        {
            await operationHandler(requestId, tcpConnection, byteBuffer);
        }
        catch (ArgumentException exception)
        {
            await SendErrorResponseAsync(tcpConnection, operation, requestId, exception.Message);
            await tcpConnection.DisconnectAsync();
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Error while processing handler for operation '{Operation}' and client '{Client}'", operation, tcpConnection.RemoteEndPoint);
            await SendErrorResponseAsync(tcpConnection, operation, requestId, $"An internal server error occurred while processing handler for operation '{operation}'. See server logs for more information.");
            await tcpConnection.DisconnectAsync();
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

    private async Task OnDisconnected(TcpConnection tcpConnection)
    {
        if (this.Disconnected is not null)
        {
            foreach (IMimoriaSocketServer.TcpConnectionEvent handler in this.Disconnected.GetInvocationList().Cast<IMimoriaSocketServer.TcpConnectionEvent>())
            {
                await handler(tcpConnection);
            }
        }
    }

    protected override void HandleOpenConnection(TcpConnection tcpConnection)
    {
        this.logger.LogInformation("New connection '{RemoteEndPoint}'", tcpConnection.RemoteEndPoint);
    }

    protected override async Task HandleCloseConnectionAsync(TcpConnection tcpConnection)
    {
        await this.OnDisconnected(tcpConnection);

        this.logger.LogInformation("Closed connection '{RemoteEndPoint}'", tcpConnection.RemoteEndPoint);
    }
}
