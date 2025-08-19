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