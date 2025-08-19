# Testing the Discount Code System

This document explains how to create and run tests for the Discount Code System using xUnit, including a sample test output.

## Creating the Test Project

### Step 1: Create xUnit Test Project

```bash
# Navigate to your solution directory
cd C:\repo\DiscountCodeSystem

# Create xUnit test project
dotnet new xunit -n DiscountCodeSystem.Tests

# Add to solution
dotnet sln add DiscountCodeSystem.Tests/DiscountCodeSystem.Tests.csproj
```

### Step 2: Add Project References

```bash
# Navigate to the test project directory
cd DiscountCodeSystem.Tests

# Add references to your projects
dotnet add reference ../DiscountServer/DiscountServer.csproj
dotnet add reference ../DiscountClient/DiscountClient.csproj
```

### Step 3: Install Required Packages

```bash
# Install additional packages for testing
dotnet add package Microsoft.AspNetCore.TestHost
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Moq
```

## Writing the Tests

### Test Project Structure

```
DiscountCodeSystem.Tests/
├── DiscountServiceTests.cs    # Unit tests for the discount service
├── TcpServerIntegrationTests.cs  # Integration tests for TCP server
└── TestClient.cs             # Helper class for testing
```

### Sample Test Class

Here's an example of a test class that verifies the core functionality:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using DiscountServer;

namespace DiscountCodeSystem.Tests
{
    public class DiscountServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _tempDb;
        private readonly DatabaseRepository _repository;
        private readonly DiscountService _service;

        public DiscountServiceTests()
        {
            // Create a unique temp directory for each test instance
            _tempDir = Path.Combine(Path.GetTempPath(), $"DiscountTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
            _tempDb = Path.Combine(_tempDir, "test.db");
            _repository = new DatabaseRepository(_tempDb);
            _service = new DiscountService(_repository);
        }

        public void Dispose()
        {
            // Dispose the repository first
            _repository.Dispose();
            
            // Force garbage collection to clean up any remaining connections
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            // Try to delete the temp directory with retries
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (Directory.Exists(_tempDir))
                    {
                        Directory.Delete(_tempDir, true);
                    }
                    break;
                }
                catch (IOException)
                {
                    // Wait and retry
                    Thread.Sleep(100 * (i + 1));
                }
            }
        }

        [Fact]
        public async Task GenerateCodesAsync_ValidInput_ReturnsCorrectCount()
        {
            // Act
            var result = await _service.GenerateCodesAsync(10, 8);
            
            // Assert
            Assert.True(result.Result);
            Assert.Equal(10, result.Codes.Count);
        }
        
        [Theory]
        [InlineData(0)]     // No codes
        [InlineData(2001)]  // Too many codes
        public async Task GenerateCodesAsync_InvalidCount_ReturnsFalse(ushort count)
        {
            // Act
            var result = await _service.GenerateCodesAsync(count, 8);
            
            // Assert
            Assert.False(result.Result);
        }
        
        [Theory]
        [InlineData(6)]     // Too short
        [InlineData(9)]     // Too long
        public async Task GenerateCodesAsync_InvalidLength_ReturnsFalse(byte length)
        {
            // Act
            var result = await _service.GenerateCodesAsync(10, length);
            
            // Assert
            Assert.False(result.Result);
        }
        
        [Fact]
        public async Task UseCodeAsync_ValidCode_ReturnsSuccess()
        {
            // Arrange
            var generateResult = await _service.GenerateCodesAsync(1, 8);
            var code = generateResult.Codes[0];
            
            // Act
            var result = await _service.UseCodeAsync(code);
            
            // Assert
            Assert.Equal(0, result.Result);
        }
        
        [Fact]
        public async Task UseCodeAsync_NonExistentCode_ReturnsNotFound()
        {
            // Act
            var result = await _service.UseCodeAsync("NONEXISTENT");
            
            // Assert
            Assert.Equal(1, result.Result);
        }
        
        [Fact]
        public async Task UseCodeAsync_AlreadyUsedCode_ReturnsAlreadyUsed()
        {
            // Arrange
            var generateResult = await _service.GenerateCodesAsync(1, 8);
            var code = generateResult.Codes[0];
            await _service.UseCodeAsync(code);
            
            // Act
            var result = await _service.UseCodeAsync(code);
            
            // Assert
            Assert.Equal(2, result.Result);
        }
    }
}
```

## Running the Tests

### From Command Line

```bash
# Navigate to the test project directory
cd DiscountCodeSystem.Tests

# Run all tests
dotnet test

# Run specific project
dotnet test DiscountCodeSystem.Tests

# Run with verbose output
dotnet test --verbosity normal

# Run specific test method
dotnet test --filter "TestName"
```

### From VS Code

1. Install the **.NET Test Explorer** extension
2. Click on the testing icon in the activity bar
3. Tests will be automatically discovered
4. Click "Run All Tests" or run individual tests

## Sample Test Output

Here's an example of a successful test run:

```
PS C:\repo\DiscountCodeSystem\DiscountCodeSystem.Tests> dotnet test
Restore complete (1.4s)
  DiscountClient succeeded (7.9s) → C:\repo\DiscountCodeSystem\DiscountClient\bin\x64\Debug\net9.0\DiscountClient.dll
  DiscountCodeSystem.Tests succeeded (4.1s) → bin\x64\Debug\net9.0\DiscountCodeSystem.Tests.dll
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 9.0.0)
[xUnit.net 00:00:01.72]   Discovering: DiscountCodeSystem.Tests
[xUnit.net 00:00:01.80]   Discovered:  DiscountCodeSystem.Tests
[xUnit.net 00:00:01.80]   Starting:    DiscountCodeSystem.Tests
[xUnit.net 00:00:02.84]   Finished:    DiscountCodeSystem.Tests
  DiscountCodeSystem.Tests test succeeded (5.0s)
Test summary: total: 8, failed: 0, succeeded: 8, skipped: 0, duration: 5.0s
Build succeeded in 19.8s
```

## Test Categories

### Unit Tests
- Test individual components in isolation
- Verify business logic in \`DiscountService\`
- Validate database operations in \`DatabaseRepository\`
- Test code generation and validation logic

### Integration Tests
- Test components working together
- Verify TCP server communication
- Test end-to-end workflows
- Validate database persistence

### Performance Tests
- Verify system can handle maximum load (2000 codes)
- Test concurrent request processing
- Measure response times under load

## Troubleshooting Common Issues

### File Access Problems
If you encounter errors like "The process cannot access the file because it is being used by another process":

1. Ensure all database connections are properly closed
2. Use the \`await using\` pattern for SQLite connections
3. Force garbage collection before file deletion
4. Use retry logic for file operations

### Test Failures
If tests are failing:

1. Check that the server implementation matches the test expectations
2. Verify validation logic in \`DiscountService\`
3. Ensure the database schema is correct
4. Check that all required packages are installed

### Debugging Tests
To debug tests:

1. Set breakpoints in your test methods
2. In VS Code, right-click on a test and select "Debug Test"
3. Use the debug console to inspect variables
4. Step through code execution to identify issues

## Best Practices

1. **Test Isolation**: Each test should run independently without sharing state
2. **Resource Management**: Properly dispose of resources like database connections
3. **Comprehensive Coverage**: Test both success and failure scenarios
4. **Clear Naming**: Use descriptive test names that explain what is being tested
5. **Arrange-Act-Assert**: Structure tests with clear setup, execution, and verification phases

By following this guide, you can effectively test the Discount Code System and ensure it meets all requirements.