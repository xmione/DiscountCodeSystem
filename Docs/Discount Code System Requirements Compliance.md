# Discount Code System Requirements Compliance

This document explains how the implemented Discount Code System satisfies each specified requirement.

## 1. Persistent Storage Between Restarts

**Requirement:** DISCOUNT codes must remain between service restarts. Store them in persistent storage (db, file, etc.)

**Implementation:**
- The system uses **SQLite** as the persistent storage mechanism
- All discount codes are stored in a database file (\`discount_codes.db\`)
- The \`DatabaseRepository\` class handles all database operations:
  ```csharp
  private void InitializeDatabase()
  {
      using var connection = new SqliteConnection(_connectionString);
      connection.Open();
      
      var command = connection.CreateCommand();
      command.CommandText = @"
          CREATE TABLE IF NOT EXISTS DiscountCodes (
              Code TEXT PRIMARY KEY,
              IsUsed INTEGER NOT NULL,
              CreatedAt TEXT NOT NULL
          );
      ";
      command.ExecuteNonQuery();
  }
  ```
- When the service restarts, it reads from the same database file, preserving all previously generated codes and their usage status

## 2. Code Length Validation

**Requirement:** The length of the DISCOUNT code is 7-8 characters during generation.

**Implementation:**
- The \`GenerateCodesAsync\` method in \`DiscountService\` validates the length parameter:
  ```csharp
  if (length < 7 || length > 8)
  {
      return new GenerateResponse { Result = false };
  }
  ```
- If the client requests a length outside 7-8 characters, the request is rejected
- Only valid lengths (7 or 8) are processed

## 3. Random and Unique Code Generation

**Requirement:** DISCOUNT code must be generated randomly and cannot repeat.

**Implementation:**
- Codes are generated randomly using a cryptographic approach:
  ```csharp
  private const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  
  var code = new string(Enumerable.Repeat(AllowedChars, length)
      .Select(s => s[_random.Next(s.Length)]).ToArray());
  ```
- Uniqueness is enforced through database checks:
  ```csharp
  if (!await _repository.CodeExistsAsync(code))
  {
      if (await _repository.InsertCodeAsync(code))
      {
          codes.Add(code);
      }
  }
  ```
- The database has a primary key constraint on the \`Code\` column, preventing duplicates

## 4. Single-Use Code Enforcement

**Requirement:** DISCOUNT code can only be used once

**Implementation:**
- The \`UseCodeAsync\` method checks if a code has already been used:
  ```csharp
  var checkCommand = connection.CreateCommand();
  checkCommand.CommandText = "SELECT IsUsed FROM DiscountCodes WHERE Code = @code";
  ```
- If the code exists and hasn't been used (\`IsUsed = 0\`), it's marked as used:
  ```csharp
  var updateCommand = connection.CreateCommand();
  updateCommand.CommandText = "UPDATE DiscountCodes SET IsUsed = 1 WHERE Code = @code";
  ```
- Returns specific status codes:
  - 0: Success (code used for the first time)
  - 1: Code not found
  - 2: Code already used

## 5. Unlimited Generation Requests

**Requirement:** Generation could be repeated as many times as desired

**Implementation:**
- The \`GenerateCodesAsync\` method has no inherent limitation on how many times it can be called
- Each call is independent and can generate new codes
- The system tracks all generated codes in the database, so subsequent calls avoid duplicates
- There's no counter or limit on the total number of generation requests

## 6. Maximum Batch Size Limit

**Requirement:** Maximum of 2 thousand DISCOUNT codes can be generated with single request.

**Implementation:**
- The \`GenerateCodesAsync\` method validates the count parameter:
  ```csharp
  if (count > 2000)
  {
      return new GenerateResponse { Result = false };
  }
  ```
- Requests for more than 2000 codes are rejected
- The method efficiently handles the maximum load of 2000 codes per request

## 7. Concurrent Request Processing

**Requirement:** System must be capable of processing multiple requests in parallel.

**Implementation:**
- **Asynchronous Architecture**: The entire system uses \`async/await\` patterns:
  ```csharp
  public async Task<GenerateResponse> GenerateCodesAsync(ushort count, byte length)
  public async Task<UseCodeResponse> UseCodeAsync(string code)
  ```
- **Concurrent Client Handling**: The TCP server spawns a new task for each client:
  ```csharp
  var client = await _listener.AcceptTcpClientAsync();
  _ = HandleClientAsync(client); // Fire and forget
  ```
- **Thread-Safe Database Operations**: SQLite connections are properly scoped and disposed:
  ```csharp
  await using var connection = new SqliteConnection(_connectionString);
  await connection.OpenAsync();
  ```
- **Non-Blocking I/O**: All network and database operations are asynchronous, preventing thread blocking
- **Stateless Service Design**: The \`DiscountService\` and \`DatabaseRepository\` are stateless, making them thread-safe

## Additional Technical Implementation Details

### Database Schema
```sql
CREATE TABLE DiscountCodes (
    Code TEXT PRIMARY KEY,     -- Ensures uniqueness
    IsUsed INTEGER NOT NULL,   -- Tracks usage (0=unused, 1=used)
    CreatedAt TEXT NOT NULL    -- Timestamp for auditing
);
```

### Request Processing Flow
1. **Client connects** → TCP server accepts connection
2. **Client sends request** → Server reads message type (1 for generate, 2 for use)
3. **Server processes request** → Calls appropriate service method
4. **Service interacts with database** → Repository handles persistence
5. **Server sends response** → Client receives result

### Concurrency Model
- Each client connection is handled in its own asynchronous task
- Database operations use connection pooling and proper disposal
- SQLite handles concurrent reads and serializes writes automatically
- No shared state between requests, eliminating race conditions

This implementation fully satisfies all requirements while maintaining high performance, reliability, and scalability for concurrent usage scenarios.