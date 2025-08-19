namespace DiscountCodeSystem.Tests
{
    public class DiscountServiceTests : IDisposable
    {
        private readonly string _tempDb;
        private readonly DatabaseRepository _repository;
        private readonly DiscountService _service;

        public DiscountServiceTests()
        {
            // Create a unique temp database for each test instance
            _tempDb = Path.GetTempFileName();
            _repository = new DatabaseRepository(_tempDb);
            _service = new DiscountService(_repository);
        }

        public void Dispose()
        {
            // Ensure all connections are closed before deleting
            _repository.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            // Now safely delete the file
            if (File.Exists(_tempDb))
            {
                File.Delete(_tempDb);
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