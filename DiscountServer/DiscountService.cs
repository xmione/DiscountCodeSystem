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