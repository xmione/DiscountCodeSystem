# Steps - How this application was created
## Step 1: Create the Solution Structure
### 1. Create a new folder for your solution:
```bash
mkdir DiscountCodeSystem
cd DiscountCodeSystem
```
### 2. Create a solution file:
```bash
dotnet new sln -n DiscountCodeSystem
```
### 3. Create the server project:
```bash
dotnet new console -n DiscountServer
dotnet sln add DiscountServer/DiscountServer.csproj
```
### 4. Add required NuGet packages:
```bash
cd DiscountServer
dotnet add package Microsoft.Data.Sqlite
cd ..
```
## Step 2: Create Project Files
### Create the following files in the DiscountServer directory:
#### 1. Program.cs
```csharp
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Initialize database repository
        var dbPath = "discount_codes.db";
        var repository = new DatabaseRepository(dbPath);
        
        // Initialize discount service
        var discountService = new DiscountService(repository);
        
        // Initialize and start TCP server
        var server = new TcpServer("127.0.0.1", 8888, discountService);
        
        Console.WriteLine("Starting server...");
        Console.WriteLine("Press Ctrl+C to stop the server.");
        
        // Handle Ctrl+C to gracefully shut down
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true;
            cts.Cancel();
            server.Stop();
        };
        
        try
        {
            await server.StartAsync();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server shutdown requested.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }
    }
}
```
#### 2. DatabaseRepository.cs
```csharp
using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;

public class DatabaseRepository
{
    private readonly string _connectionString;

    public DatabaseRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

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

    public async Task<bool> CodeExistsAsync(string code)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM DiscountCodes WHERE Code = @code";
        command.Parameters.AddWithValue("@code", code);
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<bool> InsertCodeAsync(string code)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO DiscountCodes (Code, IsUsed, CreatedAt) VALUES (@code, 0, @createdAt)";
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        
        try
        {
            await command.ExecuteNonQueryAsync();
            return true;
        }
        catch (SqliteException)
        {
            // Code already exists (primary key violation)
            return false;
        }
    }

    public async Task<byte> UseCodeAsync(string code)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        // Check if code exists
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT IsUsed FROM DiscountCodes WHERE Code = @code";
        checkCommand.Parameters.AddWithValue("@code", code);
        
        var result = await checkCommand.ExecuteScalarAsync();
        if (result == null)
        {
            return 1; // Code not found
        }
        
        bool isUsed = Convert.ToBoolean(result);
        if (isUsed)
        {
            return 2; // Code already used
        }
        
        // Mark code as used
        var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = "UPDATE DiscountCodes SET IsUsed = 1 WHERE Code = @code";
        updateCommand.Parameters.AddWithValue("@code", code);
        await updateCommand.ExecuteNonQueryAsync();
        
        return 0; // Success
    }
}
```
#### 3. DiscountService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class DiscountService
{
    private readonly DatabaseRepository _repository;
    private readonly Random _random = new Random();
    private const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public DiscountService(DatabaseRepository repository)
    {
        _repository = repository;
    }

    public async Task<GenerateResponse> GenerateCodesAsync(ushort count, byte length)
    {
        // Validate input
        if (count > 2000)
        {
            return new GenerateResponse { Result = false };
        }
        
        if (length < 7 || length > 8)
        {
            return new GenerateResponse { Result = false };
        }

        var codes = new List<string>();
        var attempts = 0;
        const int maxAttempts = 10000; // Prevent infinite loops

        while (codes.Count < count && attempts < maxAttempts)
        {
            attempts++;
            
            // Generate a random code
            var code = new string(Enumerable.Repeat(AllowedChars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
            
            // Check if code already exists in database
            if (!await _repository.CodeExistsAsync(code))
            {
                // Try to insert the code
                if (await _repository.InsertCodeAsync(code))
                {
                    codes.Add(code);
                }
            }
        }

        return new GenerateResponse 
        { 
            Result = codes.Count == count,
            Codes = codes
        };
    }

    public async Task<UseCodeResponse> UseCodeAsync(string code)
    {
        var result = await _repository.UseCodeAsync(code);
        return new UseCodeResponse { Result = result };
    }
}

// Response models
public class GenerateResponse
{
    public bool Result { get; set; }
    public List<string> Codes { get; set; } = new List<string>();
}

public class UseCodeResponse
{
    public byte Result { get; set; } // 0 = success, 1 = code not found, 2 = code already used
}
```
#### 4. TcpServer.cs
```csharp
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class TcpServer
{
    private readonly TcpListener _listener;
    private readonly DiscountService _discountService;
    private bool _isRunning;

    public TcpServer(string ipAddress, int port, DiscountService discountService)
    {
        _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
        _discountService = discountService;
    }

    public async Task StartAsync()
    {
        _listener.Start();
        _isRunning = true;
        Console.WriteLine("Server started...");

        while (_isRunning)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client); // Fire and forget
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _listener.Stop();
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
            
            using var stream = client.GetStream();
            using var reader = new BinaryReader(stream);
            using var writer = new BinaryWriter(stream);

            while (client.Connected)
            {
                // Read message type (1 byte)
                var messageType = reader.ReadByte();
                
                if (messageType == 1) // Generate codes
                {
                    // Read count (ushort)
                    var count = reader.ReadUInt16();
                    
                    // Read length (byte)
                    var length = reader.ReadByte();
                    
                    // Process request
                    var response = await _discountService.GenerateCodesAsync(count, length);
                    
                    // Write response
                    writer.Write(response.Result);
                    
                    if (response.Result)
                    {
                        // Write number of codes
                        writer.Write((ushort)response.Codes.Count);
                        
                        // Write each code
                        foreach (var code in response.Codes)
                        {
                            // Write code length (byte)
                            writer.Write((byte)code.Length);
                            
                            // Write code as bytes
                            writer.Write(Encoding.UTF8.GetBytes(code));
                        }
                    }
                }
                else if (messageType == 2) // Use code
                {
                    // Read code length (byte)
                    var codeLength = reader.ReadByte();
                    
                    // Read code as bytes
                    var codeBytes = reader.ReadBytes(codeLength);
                    var code = Encoding.UTF8.GetString(codeBytes);
                    
                    // Process request
                    var response = await _discountService.UseCodeAsync(code);
                    
                    // Write response
                    writer.Write(response.Result);
                }
                else
                {
                    Console.WriteLine($"Unknown message type: {messageType}");
                    break;
                }
                
                await writer.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine($"Client disconnected: {client.Client.RemoteEndPoint}");
        }
    }
}
```
## Step 3: Configure VS Code
### 1. Open the project in VS Code:
```bash
code .
```
### 2. Install recommended extensions (if not already installed):
- C# for Visual Studio Code (powered by OmniSharp)
- .NET Install Tool
### 3. Build and run the project:
- Open the terminal in VS Code (Ctrl+`)
- Run the server:
```bash
cd DiscountServer
dotnet run
```
## Step 4: Create Test Client (Optional)
### If you want to test the server, create a simple client project:

#### 1. Create client project:
```bash
dotnet new console -n DiscountClient
dotnet sln add DiscountClient/DiscountClient.csproj
```
#### 2. Create DiscountClient/Program.cs:
```csharp
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var client = new TestClient();
        
        Console.WriteLine("Testing code generation...");
        await client.TestGenerateCodesAsync("127.0.0.1", 8888, 5, 8);
        
        Console.WriteLine("\nEnter a code to test (or press Enter to generate a test code):");
        var code = Console.ReadLine();
        
        if (string.IsNullOrEmpty(code))
        {
            // Generate a code first
            Console.WriteLine("Generating a test code...");
            await client.TestGenerateCodesAsync("127.0.0.1", 8888, 1, 8);
            Console.WriteLine("Please copy one of the generated codes and enter it:");
            code = Console.ReadLine();
        }
        
        if (!string.IsNullOrEmpty(code))
        {
            Console.WriteLine("Testing code usage...");
            await client.TestUseCodeAsync("127.0.0.1", 8888, code);
            
            Console.WriteLine("Testing the same code again...");
            await client.TestUseCodeAsync("127.0.0.1", 8888, code);
        }
    }
}

public class TestClient
{
    public async Task TestGenerateCodesAsync(string ipAddress, int port, ushort count, byte length)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port);
            
            using var stream = client.GetStream();
            using var reader = new BinaryReader(stream);
            using var writer = new BinaryWriter(stream);
            
            // Send generate request (message type 1)
            writer.Write((byte)1); // Message type
            writer.Write(count);    // Count
            writer.Write(length);   // Length
            await writer.FlushAsync();
            
            // Read response
            var result = reader.ReadBoolean();
            Console.WriteLine($"Generate request result: {result}");
            
            if (result)
            {
                var codeCount = reader.ReadUInt16();
                Console.WriteLine($"Generated {codeCount} codes:");
                
                for (int i = 0; i < codeCount; i++)
                {
                    var codeLength = reader.ReadByte();
                    var codeBytes = reader.ReadBytes(codeLength);
                    var code = Encoding.UTF8.GetString(codeBytes);
                    Console.WriteLine($"  {code}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    public async Task TestUseCodeAsync(string ipAddress, int port, string code)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port);
            
            using var stream = client.GetStream();
            using var reader = new BinaryReader(stream);
            using var writer = new BinaryWriter(stream);
            
            // Send use code request (message type 2)
            writer.Write((byte)2); // Message type
            writer.Write((byte)code.Length); // Code length
            writer.Write(Encoding.UTF8.GetBytes(code)); // Code
            await writer.FlushAsync();
            
            // Read response
            var resultCode = reader.ReadByte();
            string resultMessage = resultCode switch
            {
                0 => "Success",
                1 => "Code not found",
                2 => "Code already used",
                _ => "Unknown error"
            };
            
            Console.WriteLine($"Use code request result: {resultMessage}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```
## Step 5: Run the Solution
### 1. Start the server:
- In VS Code terminal:
```bash
cd DiscountServer
dotnet run
```
### 2. Test with the client (in a separate terminal):
```bash
cd DiscountClient
dotnet run
```
## Setting Up xUnit Testing
### 1. Create Test Project
```bash
# Create xUnit test project
dotnet new xunit -n DiscountCodeSystem.Tests

# Add to solution
dotnet sln add DiscountCodeSystem.Tests/DiscountCodeSystem.Tests.csproj

# Add references to your projects
cd DiscountCodeSystem.Tests
dotnet add reference ../DiscountServer/DiscountServer.csproj
dotnet add reference ../DiscountClient/DiscountClient.csproj
```
### 2. Install Additional Packages
```bash
# For integration testing
dotnet add package Microsoft.AspNetCore.TestHost
dotnet add package Microsoft.Extensions.Hosting

# For mocking (if needed)
dotnet add package Moq
```
### 3. Example Test Structure
```csharp
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using DiscountServer;

namespace DiscountCodeSystem.Tests
{
    public class DiscountServiceTests
    {
        [Fact]
        public async Task GenerateCodesAsync_ValidInput_ReturnsCorrectCount()
        {
            // Arrange
            var tempDb = Path.GetTempFileName();
            var repository = new DatabaseRepository(tempDb);
            var service = new DiscountService(repository);
            
            // Act
            var result = await service.GenerateCodesAsync(10, 8);
            
            // Assert
            Assert.True(result.Result);
            Assert.Equal(10, result.Codes.Count);
            
            // Cleanup
            File.Delete(tempDb);
        }
        
        [Theory]
        [InlineData(2001)]  // Too many codes
        [InlineData(0)]     // No codes
        [InlineData(6)]     // Too short
        [InlineData(9)]     // Too long
        public async Task GenerateCodesAsync_InvalidInput_ReturnsFalse(ushort count)
        {
            // Arrange
            var tempDb = Path.GetTempFileName();
            var repository = new DatabaseRepository(tempDb);
            var service = new DiscountService(repository);
            
            // Act
            var result = await service.GenerateCodesAsync(count, 8);
            
            // Assert
            Assert.False(result.Result);
            
            // Cleanup
            File.Delete(tempDb);
        }
        
        [Fact]
        public async Task UseCodeAsync_ValidCode_ReturnsSuccess()
        {
            // Arrange
            var tempDb = Path.GetTempFileName();
            var repository = new DatabaseRepository(tempDb);
            var service = new DiscountService(repository);
            var generateResult = await service.GenerateCodesAsync(1, 8);
            var code = generateResult.Codes[0];
            
            // Act
            var result = await service.UseCodeAsync(code);
            
            // Assert
            Assert.Equal(0, result.Result);
            
            // Cleanup
            File.Delete(tempDb);
        }
    }
    
    public class TcpServerIntegrationTests : IDisposable
    {
        private readonly TcpServer _server;
        private readonly Task _serverTask;
        private readonly string _tempDb;
        
        public TcpServerIntegrationTests()
        {
            _tempDb = Path.GetTempFileName();
            var repository = new DatabaseRepository(_tempDb);
            var service = new DiscountService(repository);
            _server = new TcpServer("127.0.0.1", 8889, service);
            _serverTask = Task.Run(() => _server.StartAsync());
            
            // Give server time to start
            Task.Delay(500).Wait();
        }
        
        [Fact]
        public async Task Server_GenerateCodesRequest_ReturnsCorrectResponse()
        {
            // Arrange
            var client = new TestClient();
            
            // Act
            var result = await client.TestGenerateCodesAsync("127.0.0.1", 8889, 5, 8);
            
            // Assert
            Assert.True(result.Success);
            Assert.Equal(5, result.Codes.Count);
        }
        
        [Fact]
        public async Task Server_UseCodeRequest_ReturnsCorrectResponse()
        {
            // Arrange
            var client = new TestClient();
            var generateResult = await client.TestGenerateCodesAsync("127.0.0.1", 8889, 1, 8);
            var code = generateResult.Codes[0];
            
            // Act
            var result = await client.TestUseCodeAsync("127.0.0.1", 8889, code);
            
            // Assert
            Assert.Equal(0, result.ResultCode);
        }
        
        public void Dispose()
        {
            _server.Stop();
            _serverTask.Wait();
            File.Delete(_tempDb);
        }
    }
    
    // Enhanced test client for testing
    public class TestClient
    {
        public async Task<(bool Success, List<string> Codes)> TestGenerateCodesAsync(string ipAddress, int port, ushort count, byte length)
        {
            // Implementation similar to your client but returns results instead of writing to console
            // ...
        }
        
        public async Task<(byte ResultCode)> TestUseCodeAsync(string ipAddress, int port, string code)
        {
            // Implementation similar to your client but returns results instead of writing to console
            // ...
        }
    }
}
```
## Running Tests
```bash
# Run all tests
dotnet test

# Run specific project
dotnet test DiscountCodeSystem.Tests

# Run with verbose output
dotnet test --verbosity normal

# Run specific test method
dotnet test --filter "TestName"
```
## Test Categories to Implement
### 1. Unit Tests:
- DiscountService methods
- DatabaseRepository methods
- Code generation logic
- Validation logic
### 2. Integration Tests:
- TCP server communication
- End-to-end workflows
- Database persistence
- Concurrent request handling
### 3. Performance Tests:
- Generate 2000 codes
- Handle multiple concurrent clients
- Database performance under load
## VS Code Integration
### 1. Install the .NET Test Explorer extension
### 2. Use the testing icon in the activity bar
### 3. Run/debug tests directly from the editor
## Why Not MS-Test?
### While MS-Test is capable, it has some drawbacks for this project:

- More verbose syntax ([TestClass], [TestMethod])
- Less flexible setup/teardown
- Slower adoption in new .NET projects
- Less natural for .NET Core development
- It's the modern standard for .NET testing
- Perfect for both unit and integration tests
- Excellent for TCP/server testing scenarios
- Will grow with your project
- Great community support and documentation
