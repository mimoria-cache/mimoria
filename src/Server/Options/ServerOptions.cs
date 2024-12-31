// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Options;

public class ServerOptions
{
    public string Ip { get; set; } = "127.0.0.1";
    public ushort Port { get; set; } = 6565;
    public ushort Backlog { get; set; } = 50;
    public string Password { get; set; } = "";

    public ClusterOptions? Cluster { get; set; }

    public class ClusterOptions
    {
        public int Id { get; set; }
        public int Port { get; set; } = 6566;
        public Node[] Nodes { get; set; } = [];
        public Replication Replication { get; set; } = new Replication();
        public ElectionOptions Election { get; set; } = new ElectionOptions();
    }

    public class Node
    {
        public int Id { get; set; }
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; }
    }

    public class Replication
    {
        public ReplicationType Type { get; set; } = ReplicationType.Sync;
        public int? IntervalMilliseconds { get; set; } = null;
    }

    public class ElectionOptions
    {
        public int LeaderHeartbeatIntervalMs { get; set; } = 1000;
        public int LeaderMissingTimeoutMs { get; set; } = 3000;
    }

    public enum ReplicationType
    {
        Sync,
        Async
    }
}

