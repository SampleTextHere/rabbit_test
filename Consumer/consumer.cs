// Consumer: receives messages and processes them one-by-one.
// Key points:
// - Reads RabbitMQ connection settings from env vars (RABBITMQ_HOST/USER/PASS).
// - Retries connection until the broker is ready.
// - Declares the same durable queue so messages are retained across restarts.
// - Prefetch is configurable via env var RABBITMQ_PREFETCH (defaults to 1).
//   When set to 1, the broker delivers the next message only after the previous is acked.
// - Uses manual acknowledgements (autoAck=false) to avoid message loss if the app crashes while processing.
// - Calls an async SQL placeholder before acking, ensuring strict one-at-a-time processing.
using System;
using System.Text;
using System.Linq;
using System.Data.SqlClient;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

var factory = new ConnectionFactory 
{ 
    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest"
};

// Wait for the broker to become reachable.
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

    // Declare target queue; durable=true ensures the queue persists.
    await channel.QueueDeclareAsync(queue: "hello", durable: true, exclusive: false, autoDelete: false, arguments: null);

    // Prefetch is configurable via env; default to 1 (one-at-a-time)
    var prefetchEnv = Environment.GetEnvironmentVariable("RABBITMQ_PREFETCH");
    var prefetch = (ushort)(int.TryParse(prefetchEnv, out var p) && p > 0 ? p : 1);
    await channel.BasicQosAsync(0, prefetchCount: prefetch, global: false);

    Console.WriteLine("[*] Consuming messages. Press Ctrl+C to exit.");

    var consumer = new AsyncEventingBasicConsumer(channel);
    consumer.ReceivedAsync += async (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] Received {message}");

        // Process the message (placeholder for your SQL). Only ack after this completes.
        await MessageProcessor.ExecuteSqlAsync(message);

        // Manual ack: tells the broker this message is fully processed.
        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
    };

    await channel.BasicConsumeAsync("hello", autoAck: false, consumer: consumer);

    // Keep the app running continuously
    Console.WriteLine("Consumer running. Press Ctrl+C to exit.");
    await Task.Delay(Timeout.InfiniteTimeSpan);
}

internal static class MessageProcessor
{
    public static async Task ExecuteSqlAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("  Attempting SQL connection...");

            // Expect the producer to send a number (as text). Use it as TOP N.
            if (!int.TryParse(message.Trim(), out var topN) || topN <= 0)
            {
                Console.WriteLine("  Message is not a positive integer; skipping SQL execution.");
                return;
            }

            // Parameterized TOP to avoid injection. SQL Server supports TOP (@n) with parentheses.
            var sql = "SELECT TOP (@TopN) * FROM gtttah";
            var parameters = new[] { new SqlParameter("@TopN", topN) };

            var results = await Database.ExecuteQueryAsync(sql, parameters, cancellationToken);

            if (results.Count == 0)
            {
                Console.WriteLine("  SQL executed, no rows returned.");
                return;
            }

            Console.WriteLine($"  SQL executed successfully. Rows returned: {results.Count}");
            PrintRows(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  SQL ERROR: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
            // Re-throw to prevent ack if SQL fails
            throw;
        }
    }

    private static void PrintRows(List<Dictionary<string, object?>> rows)
    {
        // Show up to 10 rows; only the first 3 columns per row for readability.
        var displayCount = Math.Min(rows.Count, 10);
        for (int i = 0; i < displayCount; i++)
        {
            var row = rows[i];
            if (row.Count == 0)
            {
                Console.WriteLine($"    Row {i + 1}: <no columns>");
                continue;
            }

            var cols = row.Take(3).Select(kv => $"{kv.Key}={(kv.Value ?? "<null>")}");
            Console.WriteLine($"    Row {i + 1}: {{ {string.Join(", ", cols)} }}");
        }

        if (rows.Count > displayCount)
        {
            Console.WriteLine($"    ... {rows.Count - displayCount} more row(s) truncated for display");
        }
    }
}
