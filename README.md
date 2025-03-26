# mimoria

[![Build and Test](https://github.com/varelen/mimoria/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/varelen/mimoria/actions/workflows/dotnet.yml)

Performant cross-platform distributed key-value cache server written in .NET 9.

Currently under development.

## Content

* [Features](#features)
* [Installation](#installation)
    * [Quickstart](#quickstart)
    * [Manual](#manual)
    * [From source](#from-source)
* [C# client examples](#c-client-examples)
* [Config](#config)
* [Contributing](#contributing)
* [License](#license)

## Features

- [X] publish and subscribe (built-in channels like key expiration or deletion)
- [ ] cluster support for primary and secondary servers
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

## Installation

TODO: Add instructions and Docker Hub / Nuget support

### Quickstart

### Manual

### From source

## C# client examples

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

List<string> list = await mimoriaClient.GetListAsync("elements");
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
    "Logging": {
        "LogLevel": {
            "Default": "Debug",
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
