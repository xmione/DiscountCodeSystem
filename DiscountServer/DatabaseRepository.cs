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