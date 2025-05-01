# mimoria

![Build and Test](https://github.com/mimoria-cache/mimoria/actions/workflows/dotnet.yml/badge.svg?branch=main)
![Publish Docker image](https://github.com/mimoria-cache/mimoria/actions/workflows/docker-image.yml/badge.svg?branch=main)
![NuGet Version](https://img.shields.io/nuget/v/Varelen.Mimoria.Client)
![Docker Image Version](https://img.shields.io/docker/v/varelen/mimoria?label=Docker)
![GitHub License](https://img.shields.io/github/license/mimoria-cache/mimoria)

Modern and performant cross-platform distributed key-value cache server written in .NET 9.

Currently under development.

## Content

* [Features](#features)
* [Installation server](#installation-server)
    * [Quickstart](#quickstart)
    * [Manual](#manual)
    * [From source](#from-source)
* [Installation client](#installation-client)
* [C# client examples](#c-client-examples)
* [Config](#config)
* [Contributing](#contributing)
* [License](#license)

## Features

- [X] structured data (key-value, list, json, binary, map, counter)
  - [X] list has optional per value expiration
- [X] delete keys by pattern (starts with, ends with, contains)
- [X] publish and subscribe (built-in channels like key expiration, deletion, list added)
- [X] good test coverage (unit, integration and system) and asserts
- [X] metrics built-in with support for Azure Application Insights
- [ ] cluster support for primary and secondary servers (WIP)
  - [ ] synchronous replication
  - [ ] asynchronous replication
- [X] client libraries
  - [X] C# (with micro caching and DI support)
  - [ ] TypeScript/Node
  - [ ] Rust
- [X] json and binary object serialization
- [X] cross platform (Windows, Linux, macOS and ARM)
- [X] retry policies and resilience for
  - [X] connect and auto reconnect
  - [X] operation response timeout
  - [x] built-in exponential and linear retry policy
- [ ] IPv4 and IPv6 support

## Installation server

### Quickstart

You can run the server with Docker based on the main branch:
```bash
docker run -p 6565:6565 --name mimoria -e MIMORIA__PASSWORD=PleaseChooseAVeryLongOne varelen/mimoria:main
```

To change the port:
```bash
docker run -p 50000:50000 --name mimoria -e MIMORIA__PORT=50000 -e MIMORIA__PASSWORD=PleaseChooseAVeryLongOne varelen/mimoria:main
```

See the [config](#config) section for more options.

### Manual

### From source

## Installation client

You can install the .NET client library from Nuget:

```bash
dotnet add package Varelen.Mimoria.Client
```

## C# client examples

Using dotnet dependency injection (IServiceCollection):

```c#
// ...

builder.Services.AddMimoria(options =>
{
    options.Host = "localhost";
    options.Port = 6565;
    options.Password = "PleaseChooseAVeryLongOne";
    options.OperationTimeout = TimeSpan.FromMilliseconds(250);
    options.ConnectRetryPolicy = new ExponentialRetryPolicy(initialDelay: 1000, maxRetries: 4, typeof(TimeoutException));
    options.OperationRetryPolicy = new ExponentialRetryPolicy<IByteBuffer>(initialDelay: 1000, maxRetries: 4, typeof(TimeoutException));
});

// The "IMimoriaClient" interface can then be injected and be used
```

Single instance:

```c#
IMimoriaClient mimoriaClient = new MimoriaClient("127.0.0.1", 6565, "password");

await mimoriaClient.ConnectAsync();

await mimoriaClient.SetStringAsync("key", "Hello!", ttl: TimeSpan.FromMinutes(5));

string? value = await mimoriaClient.GetStringAsync("key");
Console.WriteLine(value);
// Outputs:
// Hello!

Stats stats = await mimoriaClient.GetStatsAsync();
Console.WriteLine(stats);
// Outputs:
// Uptime: 6, Connections: 1, CacheSize: 1, CacheHits: 1, CacheMisses: 0, CacheHitRatio: 1 (100%)
```

Pub Sub with add list subscription and expiring list value:

```c#
IMimoriaClient mimoriaClient = new MimoriaClient("127.0.0.1", 6565, "password");
await mimoriaClient.ConnectAsync();

var subscription = await mimoriaClient.SubscribeAsync(Channels.ForListAdded("elements"));
subscription.Payload += HandleListAdded;

static void HandleListAdded(MimoriaValue payload)
{
    string element = payload!;

    Console.WriteLine($"Added to the elements list: {element}");
}

await mimoriaClient.AddListAsync("elements", "Water");
await mimoriaClient.AddListAsync("elements", "Air", ttl: default, valueTtl: TimeSpan.FromSeconds(1));

await Task.Delay(TimeSpan.FromSeconds(2));

ImmutableList<string> list = await mimoriaClient.GetListAsync("elements");
// List only contains 'Water'
```

Cluster servers (primary and secondaries):

```c#
var clusterMimoriaClient = new ClusterMimoriaClient("password", [
    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6565),
    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6567),
    new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6569)
]);

await clusterMimoriaClient.ConnectAsync();

await clusterMimoriaClient.SetStringAsync("key", "valueeeee");

string? value = await clusterMimoriaClient.GetStringAsync("key", preferSecondary: true);
Console.WriteLine(value);
// Outputs (if using sync replication):
// some value
```

Sharded servers:

```c#
IShardedMimoriaClient shardedMimoriaClient = new ShardedMimoriaClient(
    "password", new IPEndPoint(IPAddress.Loopback, 6565), new IPEndPoint(IPAddress.Loopback, 6666));

await shardedMimoriaClient.ConnectAsync();

await shardedMimoriaClient.SetStringAsync("key", "some value");
string? value = await shardedMimoriaClient.GetStringAsync("key");
Console.WriteLine(value);
// Outputs:
// some value
```

Fast custom binary object serialization:

```c#
Guid userId = Guid.Parse("9d9b9548-3c34-415b-98c9-cb3bcfd56392");
var user = new User { Id = userId, Byte = 5, Name = "User 1" };

await mimoriaClient.SetObjectBinaryAsync($"user:{userId}", user);

User? responseUser = await mimoriaClient.GetObjectBinaryAsync<User>($"user:{userId}");
Console.WriteLine(responseUser)
// Outputs:
// Id: 9d9b9548-3c34-415b-98c9-cb3bcfd56392, Byte: 5, Name: User 1

public class User : IBinarySerializable
{
    public Guid Id { get; set; }
    public byte Byte { get; set; }
    public string Name { get; set; }
    
    public void Serialize(IByteBuffer byteBuffer)
    {
        byteBuffer.WriteGuid(this.Id);
        byteBuffer.WriteByte(this.Byte);
        byteBuffer.WriteString(this.Name);
    }

    public void Deserialize(IByteBuffer byteBuffer)
    {
        this.Id = byteBuffer.ReadGuid();
        this.Byte = byteBuffer.ReadByte();
        this.Name = byteBuffer.ReadString()!;
    }

    public override string ToString()
        => $"Id: {this.Id}, Byte: {this.Byte}, Name: {this.Name}";
}
```

Json object serialization:

```c#
Guid userId = Guid.Parse("9d9b9548-3c34-415b-98c9-cb3bcfd56392");
var user = new User { Id = userId, Byte = 5, Name = "User 1" };

await mimoriaClient.SetObjectJsonAsync<User>($"user:{userId}", user);

User? responseUser = await mimoriaClient.GetObjectJsonAsync<User>($"user:{userId}");
Console.WriteLine(responseUser);
// Outputs:
// Id: 9d9b9548-3c34-415b-98c9-cb3bcfd56392, Byte: 5, Name: User 1
```

Both ```SetObjectJsonAsync``` and ```GetObjectJsonAsync``` have an overload that can take an optional ```System.Text.Json.JsonSerializerOptions```:

```c#
var jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

await mimoriaClient.SetObjectJsonAsync<User>($"user:{myUserId}", user, jsonSerializerOptions);

_ = await mimoriaClient.GetObjectJsonAsync<User>($"user:{myUserId}", jsonSerializerOptions);
```

## Config

Options can aslo be set via environment variables with two underscores as separators.
List can be set with double underscores and the index.

Some examples:

```bash
MIMORIA__PASSWORD=PleaseChooseAVeryLongOne

MIMORIA__CLUSTER__ID=1

MIMORIA__CLUSTER__NODES__0__ID=2
```

Example of all config options:

```json
{
    "Mimoria": {
        "Ip": "0.0.0.0",
        "Port": 6565,
        "Password": "password",
        "Cluster": {
            "Id": 1,
            "Ip": "127.0.0.1",
            "Port": 6566,
            "Password": "YourClusterPassword",
            "Nodes": [
                {
                    "Id": 2,
                    "Host": "127.0.0.2",
                    "Port": 6568
                }
            ],
            "Replication": {
                "Type": "Async",
                "IntervalMilliseconds": 5000
            },
            "Election": {
                "LeaderHeartbeatIntervalMs": 1000,
                "LeaderMissingTimeoutMs": 3000,
                "ElectionTimeoutMs": 1000
            }
        }
    },
    "ConnectionStrings": {
        "ApplicationInsights": "OptionalApplicationInsightsConnectionString"
    },
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "System": "Information",
            "Microsoft": "Warning"
        }
    }
}

```

## Contributing

Any contributions are greatly appreciated.
Just fork the project, create a new feature branch, commit and push your changes and open a pull request.

## License

Distributed under the MIT License. See LICENSE for more information.
