// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Server.Metrics;

/// <summary>
/// Metrics for the Mimoria server.
/// </summary>
public sealed class MimoriaMetrics : IMimoriaMetrics
{
    public const string MeterName = "Varelen.Mimoria.Server";

    private readonly UpDownCounter<long> connectionsCounter;
    private readonly Counter<long> bytesReceivedCounter;
    private readonly Counter<long> bytesSentCounter;
    private readonly Counter<long> packetsReceivedCounter;
    private readonly Counter<long> packetsSentCounter;
    private readonly Histogram<double> operationProcessingTime;

    private readonly Counter<long> cacheHitsCounter;
    private readonly Counter<long> cacheMissesCounter;
    private readonly Counter<long> cacheExpiredKeysCounter;

    private readonly UpDownCounter<long> pubSubChannelsSubscribedCounter;
    private readonly Counter<long> pubSubMessagesReceivedCounter;
    private readonly Counter<long> pubSubMessagesSentCounter;

    public MimoriaMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, "0.0.1");

        this.connectionsCounter = meter.CreateUpDownCounter<long>("mimoria.server.connections", unit: "{connections}", description: "The current connections connected to the Mimoria server.");
        this.bytesReceivedCounter = meter.CreateCounter<long>("mimoria.server.bytes.received", unit: "byte", description: "The total bytes received");
        this.bytesSentCounter = meter.CreateCounter<long>("mimoria.server.bytes.sent", unit: "byte", description: "The total bytes sent");
        this.packetsReceivedCounter = meter.CreateCounter<long>("mimoria.server.packets.received", unit: "{packet}", description: "The total packets received");
        this.packetsSentCounter = meter.CreateCounter<long>("mimoria.server.packets.sent", unit: "{packet}", description: "The total packets sent");
        this.operationProcessingTime = meter.CreateHistogram<double>("mimoria.server.operations.processing_time", description: "The processing time of operations");
        this.cacheHitsCounter = meter.CreateCounter<long>("mimoria.server.cache.hits", unit: "{packet}", description: "The total packets received");
        this.cacheMissesCounter = meter.CreateCounter<long>("mimoria.server.cache.misses");
        this.cacheExpiredKeysCounter = meter.CreateCounter<long>("mimoria.server.cache.expired_keys");
        this.pubSubChannelsSubscribedCounter = meter.CreateUpDownCounter<long>("mimoria.server.pubsub.channels_subscribed");
        this.pubSubMessagesReceivedCounter = meter.CreateCounter<long>("mimoria.server.pubsub.messages_received");
        this.pubSubMessagesSentCounter = meter.CreateCounter<long>("mimoria.server.pubsub.messages_sent");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementConnections()
        => this.connectionsCounter.Add(delta: 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecrementConnections()
        => this.connectionsCounter.Add(delta: -1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementBytesReceived(long bytes)
        => this.bytesReceivedCounter.Add(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementBytesSent(long bytes)
        => this.bytesSentCounter.Add(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementPacketsReceived()
        => this.packetsReceivedCounter.Add(delta: 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementPacketsSent()
        => this.packetsSentCounter.Add(delta: 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordOperationProcessingTime(double milliseconds, Operation operation)
    {
        var tagList = new TagList
        {
            { "operation", operation.ToString() }
        };
        this.operationProcessingTime.Record(milliseconds, tagList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCacheHits()
        => this.cacheHitsCounter.Add(delta: 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCacheMisses()
        => this.cacheMissesCounter.Add(delta: 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCacheExpiredKeys()
        => this.cacheExpiredKeysCounter.Add(delta: 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementPubSubChannelsSubscribed(long delta)
        => this.pubSubChannelsSubscribedCounter.Add(delta);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementPubSubMessagesReceived()
        => this.pubSubMessagesReceivedCounter.Add(delta: 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementPubSubMessagesSent()
        => this.pubSubMessagesSentCounter.Add(delta: 1);
}
