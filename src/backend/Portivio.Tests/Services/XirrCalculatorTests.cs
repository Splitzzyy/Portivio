using Portivio.Application.Services;
using Xunit;

namespace Portivio.Tests.Services
{
    public class XirrCalculatorTests
    {
        [Fact]
        public void Calculate_NoCashFlows_ReturnsZero()
        {
            var result = XirrCalculator.Calculate(new List<(DateTime, decimal)>());
            Assert.Equal(0m, result);
        }

        [Fact]
        public void Calculate_AllNegativeFlows_ReturnsZero()
        {
            var flows = new List<(DateTime, decimal)>
            {
                (DateTime.UtcNow.AddDays(-365), -1000m),
                (DateTime.UtcNow.AddDays(-180), -500m)
            };
            var result = XirrCalculator.Calculate(flows);
            Assert.Equal(0m, result);
        }

        [Fact]
        public void Calculate_AllPositiveFlows_ReturnsZero()
        {
            var flows = new List<(DateTime, decimal)>
            {
                (DateTime.UtcNow.AddDays(-365), 1000m),
                (DateTime.UtcNow, 500m)
            };
            var result = XirrCalculator.Calculate(flows);
            Assert.Equal(0m, result);
        }

        [Fact]
        public void Calculate_KnownXirr_ReturnsExpectedApproximation()
        {
            var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var flows = new List<(DateTime, decimal)>
            {
                (baseDate, -1000m),
                (baseDate.AddDays(365), 1100m)
            };

            var result = XirrCalculator.Calculate(flows);

            Assert.True(result > 0.09m && result < 0.11m, $"Expected ~0.10 but got {result}");
        }

        [Fact]
        public void Calculate_SingleBuyAndSell_ReturnsPositiveReturn()
        {
            var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var flows = new List<(DateTime, decimal)>
            {
                (baseDate, -5000m),
                (baseDate.AddDays(180), 6000m)
            };

            var result = XirrCalculator.Calculate(flows);

            Assert.True(result > 0, $"Expected positive XIRR but got {result}");
        }
    }
}
