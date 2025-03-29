// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Options;

public class MimoriaOptions
{
    public const string SectionName = "Mimoria";

    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6565;
    public ushort Backlog { get; set; } = 50;
    public string? Password { get; set; }

    public CacheOptions Cache { get; set; } = new CacheOptions();

    public ClusterOptions? Cluster { get; set; }

    public class CacheOptions
    {
        public TimeSpan ExpirationCheckInterval { get; set; } = TimeSpan.FromMinutes(10);
    }

    public class ClusterOptions
    {
        public int Id { get; set; }
        public string Ip { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 6566;
        public string? Password { get; set; }
        public NodeOptions[] Nodes { get; set; } = [];
        public ReplicationOptions Replication { get; set; } = new ReplicationOptions();
        public ElectionOptions Election { get; set; } = new ElectionOptions();
    }

    public class NodeOptions
    {
        public int? Id { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
    }

    public class ReplicationOptions
    {
        public ReplicationType Type { get; set; } = ReplicationType.Sync;
        public int? IntervalMilliseconds { get; set; } = null;
    }

    public class ElectionOptions
    {
        public int LeaderHeartbeatIntervalMs { get; set; } = 1000;
        public int LeaderMissingTimeoutMs { get; set; } = 3000;
        public int ElectionTimeoutMs { get; set; } = 1000;
    }

    public enum ReplicationType
    {
        Sync,
        Async
    }
}

