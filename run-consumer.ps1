# PowerShell script to run the Consumer natively on Windows
# This script sets environment variables and runs the consumer

# RabbitMQ configuration (connects to containerized RabbitMQ via localhost)
$env:RABBITMQ_HOST = "localhost"
$env:RABBITMQ_USER = "guest"
$env:RABBITMQ_PASS = "guest"

# SQL Server configuration (connects to network SQL Server)
$env:DB_HOST = "beldb12.belsis.local"
$env:DB_INSTANCE = "sql2012v2"
$env:DB_USER = "raportest"
$env:DB_PASS = "raportest"
$env:DB_NAME = "raportestedremit"
$env:DB_ENCRYPT = "true"
$env:DB_TRUST_SERVER_CERT = "true"

# Optional: Set prefetch count (default is 1)
# $env:RABBITMQ_PREFETCH = "10"

Write-Host "Starting Consumer with the following configuration:" -ForegroundColor Cyan
Write-Host "  RabbitMQ: $env:RABBITMQ_HOST" -ForegroundColor Yellow
Write-Host "  SQL Server: $env:DB_HOST\$env:DB_INSTANCE" -ForegroundColor Yellow
Write-Host "  Database: $env:DB_NAME" -ForegroundColor Yellow
Write-Host "  Encryption: $env:DB_ENCRYPT" -ForegroundColor Yellow
Write-Host ""

# Run the consumer
dotnet run --project Consumer/Consumer.csproj
