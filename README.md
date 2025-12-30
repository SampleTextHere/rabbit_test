# rabbit_test

A RabbitMQ producer/consumer application with SQL Server integration and real-time metrics. The producer publishes numeric messages at configurable rates; the consumer executes parameterized SQL queries and reports performance metrics every second.

## Architecture

This project uses a **hybrid deployment** for SQL Server 2012 TLS compatibility:
- **RabbitMQ and Producer**: run in Docker containers
- **Consumer**: runs natively on Windows for proper SQL Server encryption support

## Prerequisites
- Docker Desktop (Windows)
- .NET 10.0 SDK
- SQL Server 2012 or later
- Ports open:
  - RabbitMQ: TCP 5672, 15672
  - SQL Server: TCP 1433 (or custom port for named instance)

## Key Files
- [compose.yaml](compose.yaml): Docker services (RabbitMQ + Producer)
- [run-consumer.ps1](run-consumer.ps1): PowerShell script to run consumer natively
- [Producer/producer.cs](Producer/producer.cs): publishes numeric messages at configurable rate
- [Consumer/consumer.cs](Consumer/consumer.cs): push-based consumption with real-time metrics
- [Consumer/Database.cs](Consumer/Database.cs): SQL Server helper using System.Data.SqlClient

## Configuration

### Producer Settings (in compose.yaml)
- `RABBITMQ_HOST`: `rabbitmq`
- `RABBITMQ_USER`: `guest`
- `RABBITMQ_PASS`: `guest`
- `PRODUCER_MSGS_PER_SEC`: Message rate (default: `20` msgs/sec)

### Consumer Settings (in run-consumer.ps1)
**RabbitMQ connection:**
- `RABBITMQ_HOST`: `localhost` (connects to Docker RabbitMQ)
- `RABBITMQ_USER`: `guest`
- `RABBITMQ_PASS`: `guest`
- `RABBITMQ_PREFETCH`: Message prefetch count (default: `1`)

**Database connection:**
- `DB_HOST`: SQL Server hostname (e.g., `beldb12.belsis.local`)
- `DB_INSTANCE`: SQL Server instance name (e.g., `sql2012v2`)
- `DB_USER`: SQL authentication username
- `DB_PASS`: SQL authentication password
- `DB_NAME`: Database name
- `DB_ENCRYPT`: `true` for encrypted connection
- `DB_TRUST_SERVER_CERT`: `true` if using self-signed certificate

## Running the Application

### Quick Start
1. **Start RabbitMQ** (runs persistently):
   ```powershell
   docker compose up -d rabbitmq
   ```

2. **Start Producer(s)** (scale as needed):
   ```powershell
   docker compose up --build producer          # Single producer
   docker compose up --scale producer=3        # Three producers
   ```

3. **Run Consumer** (native Windows, separate terminal):
   ```powershell
   .\run-consumer.ps1
   ```

Access RabbitMQ Management UI: http://localhost:15672 (guest/guest)

### Managing Services Independently
RabbitMQ runs persistently; start/stop producers without affecting it:

```powershell
# Start/restart RabbitMQ
docker compose up -d rabbitmq

# Scale producers (doesn't restart RabbitMQ)
docker compose up --scale producer=5

# Stop producers only (RabbitMQ keeps running)
docker compose stop producer

# Stop everything
docker compose down
```

### Adjusting Producer Rate
Edit `PRODUCER_MSGS_PER_SEC` in [compose.yaml](compose.yaml):
```yaml
environment:
  PRODUCER_MSGS_PER_SEC: 50  # 50 messages per second
```
Then rebuild: `docker compose up --build producer`

### Consumer Metrics
The consumer reports real-time metrics every second:
```
[10:58:15.915] Metrics - last 1s: success=52, failed=0, avg_time=18.5ms, msgs/sec=19.87; totals: success=520, failed=0
```

- `success`/`failed`: Messages processed in last second
- `avg_time`: Average SQL execution time per message (ms)
- `msgs/sec`: Overall throughput since startup
- `totals`: Cumulative success/failure counts

## How It Works
- **Producer** (Docker):
  - Sends incrementing numbers (1, 2, 3...) at configurable rate
  - Publishes to durable `hello` queue with persistent delivery
  - Rate controlled by `PRODUCER_MSGS_PER_SEC` env var
  
- **Consumer** (native Windows):
  - Receives numeric messages
  - Executes parameterized SQL: `SELECT * FROM gtttah WHERE rec_id <= @TopN`
  - Manual acknowledgement only after successful SQL execution
  - Reports metrics (throughput, timing, success/failure) every second
  
- **Database**:
  - System.Data.SqlClient for SQL Server 2012 compatibility
  - Encrypted connections with TrustServerCertificate support
  - Parameterized queries prevent SQL injection
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

## Performance Tuning

### Producer Rate
Adjust message rate in [compose.yaml](compose.yaml):
- Low rate: `PRODUCER_MSGS_PER_SEC: 10`
- High rate: `PRODUCER_MSGS_PER_SEC: 100`
- Scale producers: `docker compose up --scale producer=5`

### Consumer Throughput
Increase prefetch in [run-consumer.ps1](run-consumer.ps1):
```powershell
$env:RABBITMQ_PREFETCH = "50"  # Pipeline up to 50 messages
```

Run multiple consumers (each in separate terminal):
```powershell
.\run-consumer.ps1  # Terminal 1
.\run-consumer.ps1  # Terminal 2
.\run-consumer.ps1  # Terminal 3
```

### Monitoring
- **RabbitMQ UI**: http://localhost:15672 - queue depth, message rates
- **Consumer metrics**: Real-time throughput and latency every second
- **Error tracking**: SQL errors logged with full stack traces

## Architecture Notes
- **Why hybrid deployment?** SQL Server 2012's TLS implementation has compatibility issues with Linux containers. Running the consumer natively on Windows leverages the Windows TLS stack for proper encryption support.
- **Message persistence**: Both queue (durable) and messages (persistent) survive broker restarts
- **Manual acknowledgements**: Messages are only removed from queue after successful SQL execution
- **Push-based consumption**: More efficient than polling, consumer receives messages as they arrive

## Next Steps
- Customize SQL query in `MessageProcessor.ExecuteSqlAsync()` 
- Adjust producer rate and consumer prefetch for your workload
- Monitor metrics to identify bottlenecks
- Scale producers/consumers based on throughput requirements
