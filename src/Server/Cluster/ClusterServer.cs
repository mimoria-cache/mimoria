// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Varelen.Mimoria.Server.Cache;

namespace Varelen.Mimoria.Server.Cluster;

public sealed class ClusterServer
{
    public delegate void ClusterEvent();
    public delegate void MessageEvent(int leader);

    private readonly ILogger<ClusterServer> logger;
    private readonly ILogger<ClusterConnection> connectionLogger;
    private readonly string ip;
    private readonly int port;
    private readonly Socket socket;
    private readonly ConcurrentDictionary<int, ClusterConnection> clients;
    private readonly int expectedClients;
    internal readonly string password;
    private readonly ICache cache;

    private bool running;

    public ConcurrentDictionary<int, ClusterConnection> Clients => clients;

    public event MessageEvent? AliveReceived;
    public event ClusterEvent? AllClientsConnected;

    public ClusterServer(ILogger<ClusterServer> logger, ILogger<ClusterConnection> connectionLogger, string ip, int port, int expectedClients, string password, ICache cache)
    {
        this.logger = logger;
        this.connectionLogger = connectionLogger;
        this.ip = ip;
        this.port = port;
        this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        this.clients = [];
        this.expectedClients = expectedClients;
        this.password = password;
        this.cache = cache;
    }

    public void Start()
    {
        this.socket.Bind(new IPEndPoint(IPAddress.Parse(this.ip), this.port));
        this.socket.Listen(10);

        this.running = true;

        _ = this.AcceptAsync();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (this.running)
            {
                Socket clientSocket = await this.socket.AcceptAsync(CancellationToken.None);

                var clusterConnection = new ClusterConnection(this.connectionLogger, this, clientSocket, this.cache);
                clusterConnection.Authenticated += HandleClusterConnectionAuthenticated;
                clusterConnection.AliveReceived += HandleClusterConnectionAliveReceived;

                _ = clusterConnection.ReceiveAsync();
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignore
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Unexpected error while accepting connections");

            this.Stop();
        }
    }

    public void HandleConnectionDisconnect(ClusterConnection clusterConnection)
    {
        Debug.Assert(!clusterConnection.Connected, "Cluster connection is still connected");

        clusterConnection.Authenticated -= HandleClusterConnectionAuthenticated;
        clusterConnection.AliveReceived -= HandleClusterConnectionAliveReceived;

        bool removed = this.clients.TryRemove(clusterConnection.Id, out _);
        Debug.Assert(clusterConnection.Id == -1 || removed, $"Cluster client with id '{clusterConnection.Id}' did not exist in clients dictionary");
    }

    private void HandleClusterConnectionAliveReceived(int leader)
    {
        this.AliveReceived?.Invoke(leader);
    }

    private void HandleClusterConnectionAuthenticated(ClusterConnection clusterConnection)
    {
        bool added = this.clients.TryAdd(clusterConnection.Id, clusterConnection);
        Debug.Assert(added, "Cluster connection was not added");

        this.logger.LogInformation("New cluster connection from '{RemoteAddress}' (clients connected: '{ClientConnectedCount}', clients expected: '{ClientsExpectedCount}')", clusterConnection.RemoteEndPoint, clients.Count, expectedClients);

        Debug.Assert(this.clients.Count <= this.expectedClients, $"Client count is larger than expected count ('{this.clients.Count}' vs. '{this.expectedClients}')");

        if (this.clients.Count == this.expectedClients)
        {
            this.AllClientsConnected?.Invoke();
        }
    }

    public void Stop()
    {
        if (!Interlocked.Exchange(ref this.running, false))
        {
            return;
        }

        this.socket.Close();

        foreach (var (_, clusterConnection) in this.clients)
        {
            clusterConnection.Disconnect();
        }
    }
}
