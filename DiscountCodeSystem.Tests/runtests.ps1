# Run all tests
dotnet test

# Run specific project
dotnet test DiscountCodeSystem.Tests

# Run with verbose output
dotnet test --verbosity normal

# Run specific test method
dotnet test --filter "TestName"