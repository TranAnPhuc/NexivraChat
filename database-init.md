# Project Plan: Database Verification and Initialization (database-init)

This plan outlines the steps required to verify the local PostgreSQL database service status, configure the connection string, and run the backend application to trigger the database schema initialization for the NexivraChat application.

## 1. Overview
The goal of this plan is to verify that the local PostgreSQL database is up and running on Windows, validate the backend connection settings, and execute the backend application to trigger `DbInitializer.Initialize`. This process creates the necessary database schema and seeds the default chat rooms, ensuring the backend is ready to accept client connections.

## 2. Project Type
DATABASE / SETUP

## 3. Success Criteria
- **PostgreSQL service** is running on the local Windows machine.
- **ConnectionString** is verified and, if necessary, updated in `appsettings.json` or `appsettings.Development.json`.
- **Database initialization** runs successfully, creating the 6 required tables and index structures:
  1. `users`
  2. `chat_rooms`
  3. `private_chats`
  4. `messages`
  5. `user_profiles`
  6. `conversation_reads`
- **Default rooms** (`General` and `AI Lounge`) are successfully seeded into the `chat_rooms` table.
- **dotnet build** compiles the backend application successfully without any errors.

## 4. Tech Stack
- **PowerShell** (Windows service management and scripting)
- **C# / .NET 8** (Backend runtime)
- **Dapper** (Micro-ORM for database initialization)
- **PostgreSQL** (Relational Database)

## 5. File Structure
The following files are relevant to the database configuration and initialization:
```plaintext
backend/
├── NexivraChatBackend/
│   ├── Data/
│   │   ├── DapperContext.cs          # Configures database connection
│   │   └── DbInitializer.cs          # Performs table creations and seeding
│   ├── appsettings.json              # Contains main ConnectionStrings
│   ├── appsettings.Development.json  # Contains development environment overrides
│   └── Program.cs                    # Triggers DbInitializer on start
```

## 6. Task Breakdown

### Task 1: Check and Start PostgreSQL Service
- **Agent Recommendation**: `devops-engineer`
- **Skill Recommendation**: `powershell-windows`
- **Priority**: P0
- **Dependencies**: None
- **INPUT**: Windows PowerShell environment.
- **OUTPUT**: PostgreSQL service is verified as running.
- **VERIFY**:
  1. Run the following command in PowerShell:
     ```powershell
     Get-Service -Name "postgresql*"
     ```
  2. If the service is stopped or not running, start it using:
     ```powershell
     Start-Service -Name "postgresql-x64-*" # (using the matching version suffix found)
     ```
  3. Re-run `Get-Service` and confirm that the `Status` is `Running`.

### Task 2: Verify and Update ConnectionString
- **Agent Recommendation**: `database-architect`
- **Skill Recommendation**: `database-design`
- **Priority**: P0
- **Dependencies**: Task 1
- **INPUT**: 
  - `backend/NexivraChatBackend/appsettings.json`
  - `backend/NexivraChatBackend/appsettings.Development.json`
- **OUTPUT**: Connection string is verified or modified to match local PostgreSQL server credentials.
- **VERIFY**:
  1. Inspect the connection string under `ConnectionStrings:DefaultConnection` in `appsettings.json` (currently: `"Host=localhost;Database=postgres;Username=postgres;Password=Boggyroom24032005@"`).
  2. Confirm database name, username, and password are correct.
  3. Validate connection using the PostgreSQL CLI tool `psql`:
     ```powershell
     $env:PGPASSWORD="Boggyroom24032005@"
     psql -h localhost -U postgres -d postgres -c "SELECT 1;"
     ```
     Ensure it connects successfully without authentication errors.

### Task 3: Build and Run Backend to Initialize Database
- **Agent Recommendation**: `backend-specialist`
- **Skill Recommendation**: `verify-changes`
- **Priority**: P1
- **Dependencies**: Task 2
- **INPUT**: C# Backend code in `backend/NexivraChatBackend`
- **OUTPUT**: Database schema created and default rooms seeded.
- **VERIFY**:
  1. Navigate to backend directory and build the project:
     ```powershell
     dotnet build backend/NexivraChatBackend/NexivraChatBackend.csproj
     ```
     Confirm compilation succeeds.
  2. Run the application:
     ```powershell
     dotnet run --project backend/NexivraChatBackend
     ```
  3. Verify the console outputs:
     `Database initialized successfully.`
  4. Query the database using `psql` to check the created tables and seeded rooms:
     ```powershell
     $env:PGPASSWORD="Boggyroom24032005@"
     psql -h localhost -U postgres -d postgres -c "\dt;"
     psql -h localhost -U postgres -d postgres -c "SELECT * FROM chat_rooms;"
     ```
     Confirm that 6 tables exist and 2 default rooms (`General`, `AI Lounge`) are returned.

## 7. Final Verification Checklist (Phase X)

Prior to completing this initialization effort, perform the following validation steps:

- [ ] **Service Check**: Verify PostgreSQL service status is `Running`.
- [ ] **Config Check**: Verify the connection credentials in `appsettings.json` are correct.
- [ ] **Build Check**: Run `dotnet build backend/NexivraChatBackend` and verify 0 errors.
- [ ] **Initialization Logs**: Confirm `Database initialized successfully.` message in the startup log.
- [ ] **Table Verification**: Run SQL query to check if all 6 tables exist.
- [ ] **Seed Data Verification**: Confirm `General` and `AI Lounge` chat rooms are present in the database.

### ✅ PHASE X COMPLETE
- Service Status: [x] Running (postgresql-x64-18)
- Config Validation: [x] Verified connection settings in appsettings.json
- Build Compilation: [x] dotnet build succeeded with 0 errors
- Table Checks: [x] DbInitializer ran successfully and created all tables/seed data
- Date: 2026-06-28
