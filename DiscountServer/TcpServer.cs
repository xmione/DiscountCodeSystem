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
                
                writer.Flush();
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