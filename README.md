# Discount Code System

A TCP-based server for generating and validating discount codes with persistent storage.

📖[A Story about Angels and Demons](#angel-story) <--- Do you want to read a cool story about angels and demons? Click here. 
## Features

- Generate unique random discount codes (7-8 characters)
- Validate discount codes (can only be used once)
- Persistent storage using SQLite
- Concurrent request handling
- TCP-based communication protocol
- Up to 2000 codes per generation request

## System Requirements

- .NET 5.0 or later
- Visual Studio Code with C# extension
- Microsoft.Data.Sqlite NuGet package

## Project Structure

```
DiscountCodeSystem/
├── DiscountServer/
│   ├── Program.cs           # Server entry point
│   ├── TcpServer.cs         # TCP server implementation
│   ├── DiscountService.cs   # Business logic
│   ├── DatabaseRepository.cs # Data access layer
│   └── DiscountServer.csproj
├── DiscountClient/
│   ├── Program.cs           # Test client
│   └── DiscountClient.csproj
├── DiscountCodeSystem.Tests/
│   ├── DiscountServiceTests.cs  # Test class
│   ├── runtests.ps1             # Test runner script
│   └── DiscountCodeSystem.Tests.csproj
├── DiscountCodeSystem.sln   # Solution file
└── README.md               # This file
```

## Setup Instructions

### Prerequisites

1. Install [.NET SDK](https://dotnet.microsoft.com/download) (5.0 or later)
2. Install [Visual Studio Code](https://code.visualstudio.com/)
3. Install the [C# extension for VS Code](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)

### Cloning the Repository

```bash
git clone https://github.com/xmione/DiscountCodeSystem.git
cd DiscountCodeSystem
```

### Restoring Dependencies

```bash
dotnet restore
```

## Running the Application

### Starting the Server

1. Open the project in VS Code:
   ```bash
   code .
   ```

2. Open the terminal in VS Code (Ctrl+\`)

3. Navigate to the server directory and run:
   ```bash
   cd DiscountServer
   dotnet run
   ```

   The server will start listening on \`127.0.0.1:8888\`

### Testing with the Client

1. Open a new terminal in VS Code (Ctrl+Shift+\`)

2. Navigate to the client directory and run:
   ```bash
   cd DiscountClient
   dotnet run
   ```

   The client will:
   - Prompt you to enter the count or number of concurrent requests, 10 is the default.
   - Generate test codes based on the supplied count (8 characters each)
   - Prompt you to enter a code to test
   - Test code usage (first use should succeed, second should fail)

### Testing with xUnit
- See the document [Testing the Discount Code System](Docs/Testing%20the%20Discount%20Code%20System.md) if you want to see how the test project was created and how to run it.
- See below commands if you want to go straight to testing it already using xUnit tests.

   #### From Command Line

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

## API Protocol

The server uses a custom TCP protocol with binary messages:

### Generate Codes Request

| Position | Type    | Description          |
|----------|---------|----------------------|
| 0        | byte    | Message type (1)     |
| 1        | ushort  | Number of codes      |
| 3        | byte    | Code length (7-8)    |

### Generate Codes Response

| Position | Type    | Description          |
|----------|---------|----------------------|
| 0        | bool    | Success flag         |
| 1        | ushort  | Number of codes (if success) |
| 3+       | varies  | Codes (if success)   |

Each code is prefixed with its length (byte) followed by the code bytes.

### Use Code Request

| Position | Type    | Description          |
|----------|---------|----------------------|
| 0        | byte    | Message type (2)     |
| 1        | byte    | Code length          |
| 2        | bytes   | Code                 |

### Use Code Response

| Position | Type    | Description          |
|----------|---------|----------------------|
| 0        | byte    | Result code          |

Result codes:
- 0: Success
- 1: Code not found
- 2: Code already used

## Database

The system uses SQLite for persistent storage. The database file (\`discount_codes.db\`) is created automatically in the server directory when the server first runs.

### Database Schema

```sql
CREATE TABLE DiscountCodes (
    Code TEXT PRIMARY KEY,
    IsUsed INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL
);
```

## Development

### Building the Solution

```bash
dotnet build
```

### Running Tests

```bash
cd DiscountClient
dotnet run
```

### Debugging

1. Set breakpoints in VS Code by clicking in the gutter next to line numbers
2. Press F5 to start debugging
3. Use the debug console to inspect variables

## Troubleshooting

### Common Issues

1. **Port already in use**: Make sure no other application is using port 8888
2. **Database locked**: This can happen if the server is terminated abruptly. Delete the \`discount_codes.db\` file to reset.
3. **Client connection errors**: Ensure the server is running before starting the client.

### Error Messages

- "BinaryWriter does not contain a definition for 'FlushAsync'": This was fixed in the latest version. Ensure you're using the corrected code.
- "Cannot connect to server": Verify the server is running and the address/port are correct.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the GNU GENERAL PUBLIC LICENSE and MIT License - see the LICENSE files for details.

## Acknowledgments

- Built with .NET 9.0
- Uses SQLite for persistent storage
- Implements a custom TCP protocol

## Other related documents:
[Steps - How this application was created](Docs/Steps%20-%20How%20this%20application%20was%20created.md)

[Discount Code System Requirements Compliance](Docs/Discount%20Code%20System%20Requirements%20Compliance.md)

[Testing the Discount Code System](Docs/Testing%20the%20Discount%20Code%20System.md)

[.NET Principles and Design Patterns in Discount Code System](Docs/.NET%20Principles%20and%20Design%20Patterns%20in%20Discount%20Code%20System.md)

[A Story about Angles and Demons](Docs/A%20Story%20about%20Angels%20and%20Demons.md)
<a id="angel-story"></a>

-------------------------------------------------- NOTHING FOLLOWS --------------------------------------------------

