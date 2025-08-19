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