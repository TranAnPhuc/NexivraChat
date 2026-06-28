using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Dapper;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;
using NexivraChatBackend.Data;

namespace NexivraChatBackend.Tests.Fixtures
{
    public class DatabaseFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _postgresContainer;
        private Respawner? _respawner;
        private DbConnection? _dbConnection;

        public string ConnectionString { get; private set; } = string.Empty;
        public DapperContext? DapperContext { get; private set; }

        public async Task InitializeAsync()
        {
            try
            {
                // Defer creation of Testcontainer builder and Build() call to InitializeAsync to handle environment without Docker
                _postgresContainer = new PostgreSqlBuilder()
                    .WithImage("postgres:15-alpine")
                    .WithDatabase("nexivra_chat")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();

                // Try using Testcontainers first
                await _postgresContainer.StartAsync();
                ConnectionString = _postgresContainer.GetConnectionString();
            }
            catch (Exception ex)
            {
                // Fall back to local PostgreSQL instance
                Console.WriteLine($"Docker is not available/running. Falling back to local PostgreSQL. Error: {ex.Message}");
                _postgresContainer = null;
                
                var pgPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "changeme";
                var localBaseConnectionString = $"Host=localhost;Database=postgres;Username=postgres;Password={pgPassword}";
                var testDbName = "nexivra_chat_tests";
                
                // Create database if not exists
                using (var masterConn = new NpgsqlConnection(localBaseConnectionString))
                {
                    await masterConn.OpenAsync();
                    var dbExists = await masterConn.ExecuteScalarAsync<bool>(
                        "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @testDbName)",
                        new { testDbName });
                    
                    if (!dbExists)
                    {
                        await masterConn.ExecuteAsync($"CREATE DATABASE {testDbName}");
                    }
                }
                
                ConnectionString = $"Host=localhost;Database={testDbName};Username=postgres;Password={pgPassword}";
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = ConnectionString
                })
                .Build();

            DapperContext = new DapperContext(configuration);

            // Initialize the database schema
            DbInitializer.Initialize(DapperContext);

            // Setup Respawn
            _dbConnection = new NpgsqlConnection(ConnectionString);
            await _dbConnection.OpenAsync();

            _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = new[] { "public" }
            });
        }

        public async Task ResetDatabaseAsync()
        {
            if (_dbConnection != null && _respawner != null)
            {
                await _respawner.ResetAsync(_dbConnection);
            }
        }

        public async Task DisposeAsync()
        {
            if (_dbConnection != null)
            {
                await _dbConnection.DisposeAsync();
            }
            if (_postgresContainer != null)
            {
                try
                {
                    await _postgresContainer.DisposeAsync();
                }
                catch
                {
                    // Ignore if container wasn't started or fails to dispose
                }
            }
        }
    }
}
