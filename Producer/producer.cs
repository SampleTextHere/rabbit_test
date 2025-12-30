// Producer: publishes timestamped messages to a durable queue.
// Key points:
// - Reads RabbitMQ connection settings from env vars (RABBITMQ_HOST/USER/PASS).
// - Retries connection until the broker is ready.
// - Declares a durable queue so messages and the queue survive broker restarts.
// - Publishes messages as persistent (DeliveryMode = Persistent) so they are stored on disk.
// - Sends a message every 5 seconds in an infinite loop.
using System;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

var factory = new ConnectionFactory 
{ 
    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest"
};

// Wait for the broker to become reachable (RabbitMQ can take ~10-15s to boot).
Console.WriteLine("Waiting for RabbitMQ...");
IConnection connection = null!;
for (int i = 0; i < 30; i++)
{
    try
    {
        connection = await factory.CreateConnectionAsync();
        Console.WriteLine("Connected to RabbitMQ!");
        break;
    }
    catch (BrokerUnreachableException)
    {
        Console.WriteLine($"RabbitMQ not ready, retrying in {2 * (i + 1)}s...");
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}

if (connection == null)
{
    Console.WriteLine("Failed to connect to RabbitMQ after 30 retries");
    return;
}

using (connection)
{
    using var channel = await connection.CreateChannelAsync();
    
    // Declare the target queue. Durable=true ensures the queue survives restarts.
    await channel.QueueDeclareAsync(queue: "hello", durable: true, exclusive: false, autoDelete: false, arguments: null);
    
    // Make published messages persistent so the broker stores them on disk.
    var props = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };

    // Simple demo loop: publish one message every 5 seconds.
    Console.WriteLine("Sending messages every 5 seconds. Press Ctrl+C to exit.");
    
    int messageCount = 0;
    while (true)
    {
        messageCount++;
        var message = $"{messageCount}" ;
        var body = Encoding.UTF8.GetBytes(message);
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        
        // Publish to the default exchange with routingKey=queue name.
        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "hello", mandatory: true, basicProperties: props, body: body);
        Console.WriteLine($"[{timestamp}] Sent {message}");
        
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}