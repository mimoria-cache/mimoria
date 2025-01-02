// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Options;

using System.Net;

namespace Varelen.Mimoria.Server.Options;

public sealed class MimoriaOptionsValidation : IValidateOptions<MimoriaOptions>
{
    public ValidateOptionsResult Validate(string? name, MimoriaOptions options)
    {
        if (!IPAddress.TryParse(options.Ip, out _))
        {
            return ValidateOptionsResult.Fail($"'Ip' needs to be a valid IPv4 address");
        }

        if (options.Port < ushort.MinValue || options.Port > ushort.MaxValue)
        {
            return ValidateOptionsResult.Fail($"'Port' needs to be in range '{ushort.MinValue}-{ushort.MaxValue}' but was '{options.Port}'");
        }

        if (options.Password is null)
        {
            return ValidateOptionsResult.Fail($"Mimoria section requires a 'Password' to be set");
        }

        if (options.Cluster is not null)
        {
            if (!IPAddress.TryParse(options.Cluster.Ip, out _))
            {
                return ValidateOptionsResult.Fail($"Cluster -> 'Ip' needs to be a valid IPv4 address");
            }

            if (options.Port < ushort.MinValue || options.Port > ushort.MaxValue)
            {
                return ValidateOptionsResult.Fail($"Cluster -> 'Port' needs to be in range '{ushort.MinValue}-{ushort.MaxValue}' but was '{options.Cluster.Port}'");
            }

            if (options.Cluster.Password is null)
            {
                return ValidateOptionsResult.Fail($"Cluster section requires a 'Password' to be set");
            }

            if (options.Cluster.Replication.Type == MimoriaOptions.ReplicationType.Async
                && options.Cluster.Replication.IntervalMilliseconds is null)
            {
                return ValidateOptionsResult.Fail($"Cluster -> Replication section requires 'IntervalMilliseconds' to be set if the replication type is 'Async'");
            }

            if (options.Cluster.Nodes.Length == 0)
            {
                return ValidateOptionsResult.Fail($"Cluster section needs at least one node in 'Nodes' configured");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
