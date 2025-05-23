﻿// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Server.Metrics;

public interface IMimoriaMetrics
{
    void IncrementConnections();
    void DecrementConnections();
    void IncrementBytesReceived(long bytes);
    void IncrementBytesSent(long bytes);
    void IncrementPacketsReceived();
    void IncrementPacketsSent();
    void RecordOperationProcessingTime(double milliseconds, Operation operation);
    void IncrementCacheHits();
    void IncrementCacheMisses();
    void IncrementCacheExpiredKeys();
    void IncrementPubSubChannelsSubscribed(long delta);
    void IncrementPubSubMessagesReceived();
    void IncrementPubSubMessagesSent();
}
