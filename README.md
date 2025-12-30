# rabbit_test

A minimal RabbitMQ producer/consumer application with SQL Server integration. The producer publishes persistent messages to a durable queue; the consumer receives them in push mode with manual acks and executes SQL queries.

## Architecture

Due to TLS compatibility issues between Linux containers and SQL Server 2012, this project uses a **hybrid deployment**:
- **Producer and RabbitMQ**: run in Docker containers
- **Consumer**: runs natively on Windows for proper SQL Server encryption support

## Prerequisites
- Docker Desktop (Windows)
- .NET 10.0 SDK
- SQL Server 2012 or later
- Ports open:
  - RabbitMQ: TCP 5672, 15672
  - SQL Server: TCP 1433 (or custom port for named instance)

## Key Files
- [compose.yaml](compose.yaml): Docker services (RabbitMQ + Producer only)
- [run-consumer.ps1](run-consumer.ps1): PowerShell script to run consumer natively
- [Producer/producer.cs](Producer/producer.cs): publishes messages on user input
- [Consumer/consumer.cs](Consumer/consumer.cs): push-based consumption, manual ack, SQL execution
- [Consumer/Database.cs](Consumer/Database.cs): SQL Server helper using System.Data.SqlClient

## Configuration

### RabbitMQ Settings (in compose.yaml)
Both producer and consumer use:
- `RABBITMQ_HOST`: `rabbitmq` (for producer in Docker), `localhost` (for native consumer)
- `RABBITMQ_USER`: `guest`
- `RABBITMQ_PASS`: `guest`

### Database Settings (for native consumer)
Configure in [run-consumer.ps1](run-consumer.ps1) or set environment variables:
- `DB_HOST`: SQL Server hostname (e.g., `beldb12.belsis.local`)
- `DB_INSTANCE`: SQL Server instance name (e.g., `sql2012v2`)
- `DB_USER`: SQL authentication username
- `DB_PASS`: SQL authentication password
- `DB_NAME`: Database name
- `DB_ENCRYPT`: `true` for encrypted connection
- `DB_TRUST_SERVER_CERT`: `true` if using self-signed certificate

### Consumer Behavior
- `RABBITMQ_PREFETCH`: Controls message prefetch (default: 1 for strict one-by-one processing)

## Running the Application

### Step 1: Start RabbitMQ and Producer
```powershell
docker compose up --build
```

This starts:
- RabbitMQ broker (ports 5672 for AMQP, 15672 for management UI)
- Producer application (waits for your input to send messages)

Access RabbitMQ Management UI: http://localhost:15672 (username: `guest`, password: `guest`)

### Step 2: Run Consumer Natively on Windows
In a **separate PowerShell terminal**:
```powershell
.\run-consumer.ps1
```

Or run manually with environment variables:
```powershell
$env:RABBITMQ_HOST = "localhost"
$env:RABBITMQ_USER = "guest"
$env:RABBITMQ_PASS = "guest"
$env:DB_HOST = "beldb12.belsis.local"
$env:DB_INSTANCE = "sql2012v2"
$env:DB_USER = "raportest"
$env:DB_PASS = "raportest"
$env:DB_NAME = "raportestedremit"
$env:DB_ENCRYPT = "true"
$env:DB_TRUST_SERVER_CERT = "true"
dotnet run --project Consumer/Consumer.csproj
```

### Step 3: Send Messages
In the producer terminal, type your message and press Enter. The consumer will:
1. Receive the message from RabbitMQ
2. Execute SQL query (placeholder in `MessageProcessor.ExecuteSqlAsync`)
3. Acknowledge the message

### To Stop
- Consumer: Press `Ctrl+C` in the consumer terminal
- Docker services:
```powershell
docker compose down
```

## How It Works
- **Producer** (runs in Docker):
  - Declares `hello` queue with `durable: true`
  - Publishes with `DeliveryMode = Persistent` (survives broker restarts)
  - Waits for user input, sends timestamped messages
- **Consumer** (runs natively on Windows):
  - Declares the same durable queue
  - `BasicQos(prefetch)` controls message pipeline (default: 1)
  - `BasicConsume(autoAck: false)` - manual acknowledgement only after SQL execution
  - Calls `MessageProcessor.ExecuteSqlAsync(message)` before acknowledging
- **Database**:
  - Connection string built from environment variables in [Consumer/Database.cs](Consumer/Database.cs)
  - Uses System.Data.SqlClient for SQL Server 2012 compatibility
  - Supports encrypted connections with TrustServerCertificate option

## SQL Server Setup
1. **Enable TCP/IP** in SQL Server Configuration Manager
2. **Set static TCP port** (e.g., 1433) under `IPAll` in TCP/IP properties; restart SQL Server service
3. **Enable SQL Authentication** and create login matching `DB_USER`/`DB_PASS`
4. **Open Windows Firewall** inbound rule for TCP port (e.g., 1433)
5. For **named instances**, either:
   - Run SQL Server Browser service (UDP 1434) and open firewall
   - Or use static port and connect via `server,port` format

## Troubleshooting
- **RabbitMQ startup**: Broker can take 10-15 seconds; apps retry automatically
- **Connection refused**: Ensure RabbitMQ is running (`docker compose logs rabbitmq`)
- **Consumer can't connect**: Check that ports 5672 and 15672 are published in compose.yaml
- **SQL connection fails**:
  - Verify SQL Server is accessible from host (test with SSMS)
  - Check firewall rules for SQL Server port
  - Ensure SQL authentication is enabled
  - For named instances, verify SQL Browser is running or use static port
- **TLS/encryption errors**: This is why consumer runs natively - Windows TLS stack properly supports SQL Server 2012 encryption

## Scaling
- Increase `RABBITMQ_PREFETCH` for higher throughput
- Run multiple consumer instances (each in separate terminal)
- Manual acknowledgements ensure at-least-once delivery

## Architecture Notes
- **Why hybrid deployment?** SQL Server 2012's TLS implementation has compatibility issues with Linux containers. Running the consumer natively on Windows leverages the Windows TLS stack for proper encryption support.
- **Message persistence**: Both queue (durable) and messages (persistent) survive broker restarts
- **Manual acknowledgements**: Messages are only removed from queue after successful SQL execution
- **Push-based consumption**: More efficient than polling, consumer receives messages as they arrive

## Next Steps
- Implement actual SQL queries in `MessageProcessor.ExecuteSqlAsync()` using the `Database` helper methods
- Customize message format and processing logic
- Add error handling/retry logic for failed SQL operations
