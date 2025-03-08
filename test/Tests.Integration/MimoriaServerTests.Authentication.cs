// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Net.Sockets;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerTests : IAsyncLifetime
{
    private const byte ClusterOperationStart = 240;

    [Theory]
    [MemberData(nameof(GetClientOperations))]
    public async Task Authentication_Given_OneServer_When_ClientOperationWhenNotLoggedIn_Then_AuthenticationRequiredErrorIsReturned(Operation operation)
    {
        // Arrange
        var tcpClient = new TcpClient(Ip, this.port);
        NetworkStream networkStream = tcpClient.GetStream();

        using var byteBuffer = PooledByteBuffer.FromPool(operation, requestId: 0);
        byteBuffer.EndPacket();

        // Act
        await networkStream.WriteAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));

        var responseBufferBytes = new byte[1024];
        int read = await networkStream.ReadAsync(responseBufferBytes.AsMemory());

        using var responseBuffer = PooledByteBuffer.FromPool();
        responseBuffer.WriteBytes(responseBufferBytes);

        // Assert
        var packetLength = responseBuffer.ReadUInt();
        var responseOperation = (Operation)responseBuffer.ReadByte();
        var requestId = responseBuffer.ReadUInt();
        var status = (StatusCode)responseBuffer.ReadByte();
        var message = responseBuffer.ReadString();

        var operationStringLength = operation.ToString().Length;

        Assert.Equal(54 + operationStringLength, read);
        Assert.Equal((uint)50 + operationStringLength, packetLength);
        Assert.Equal(operation, responseOperation);
        Assert.Equal((uint)0, requestId);
        Assert.Equal(StatusCode.Error, status);
        Assert.Equal($"Authentication required to use operation '{operation}'", message);
    }

    [Theory]
    [MemberData(nameof(GetClusterOperations))]
    public async Task Authentication_Given_OneServer_When_ClusterOperationWhenNotLoggedIn_Then_OperationUnsupportedErrorIsReturned(Operation operation)
    {
        // Arrange
        var tcpClient = new TcpClient(Ip, this.port);
        NetworkStream networkStream = tcpClient.GetStream();

        using var byteBuffer = PooledByteBuffer.FromPool(operation, requestId: 0);
        byteBuffer.EndPacket();

        // Act
        await networkStream.WriteAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));

        var responseBufferBytes = new byte[1024];
        int read = await networkStream.ReadAsync(responseBufferBytes.AsMemory());

        using var responseBuffer = PooledByteBuffer.FromPool();
        responseBuffer.WriteBytes(responseBufferBytes);

        // Assert
        var packetLength = responseBuffer.ReadUInt();
        var responseOperation = (Operation)responseBuffer.ReadByte();
        var requestId = responseBuffer.ReadUInt();
        var status = (StatusCode)responseBuffer.ReadByte();
        var message = responseBuffer.ReadString();

        var operationStringLength = operation.ToString().Length;

        Assert.Equal(38 + operationStringLength, read);
        Assert.Equal((uint)34 + operationStringLength, packetLength);
        Assert.Equal(operation, responseOperation);
        Assert.Equal((uint)0, requestId);
        Assert.Equal(StatusCode.Error, status);
        Assert.Equal($"Operation '{operation}' is unsupported", message);
    }

    [Fact]
    public async Task Authentication_Given_OneServer_When_WrongPassword_Then_ErrorIsReturned()
    {
        // Arrange
        var tcpClient = new TcpClient(Ip, this.port);
        NetworkStream networkStream = tcpClient.GetStream();

        using var byteBuffer = PooledByteBuffer.FromPool(Operation.Login, requestId: 0);
        byteBuffer.WriteVarUInt(1);
        byteBuffer.WriteString("WrongPassword");
        byteBuffer.EndPacket();

        // Act
        await networkStream.WriteAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));

        var responseBufferBytes = new byte[1024];
        int read = await networkStream.ReadAsync(responseBufferBytes.AsMemory());

        using var responseBuffer = PooledByteBuffer.FromPool();
        responseBuffer.WriteBytes(responseBufferBytes);

        // Assert
        var packetLength = responseBuffer.ReadUInt();
        var operation = (Operation)responseBuffer.ReadByte();
        var requestId = responseBuffer.ReadUInt();
        var status = (StatusCode)responseBuffer.ReadByte();
        var loggedIn = responseBuffer.ReadByte();

        Assert.Equal(11, read);
        Assert.Equal((uint)7, packetLength);
        Assert.Equal(Operation.Login, operation);
        Assert.Equal((uint)0, requestId);
        Assert.Equal(StatusCode.Ok, status);
        Assert.Equal((uint)0, loggedIn);
    }

    public static TheoryData<Operation> GetClientOperations()
    {
        var list = new List<Operation>();
        foreach (var operation in Enum.GetValues<Operation>())
        {
            if (operation == Operation.Login || (byte)operation > ClusterOperationStart)
            {
                continue;
            }

            list.Add(operation);
        }
        return [.. list];
    }

    public static TheoryData<Operation> GetClusterOperations()
    {
        var list = new List<Operation>();
        foreach (var operation in Enum.GetValues<Operation>())
        {
            if (operation == Operation.Login || (byte)operation < ClusterOperationStart)
            {
                continue;
            }

            list.Add(operation);
        }
        return [.. list];
    }
}
