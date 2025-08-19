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
            writer.Flush();
            
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
            writer.Flush();
            
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