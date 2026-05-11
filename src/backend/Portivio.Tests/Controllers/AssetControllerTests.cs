using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Portivio.API.Controllers;
using Portivio.Application.DTOs.Asset;
using Portivio.Application.Results;
using Portivio.Application.Services;
using Xunit;

namespace Portivio.Tests.Controllers
{
    public class AssetControllerTests
    {
        private static AssetController CreateController(Mock<IAssetInstrumentService> serviceMock, Guid userId)
        {
            var controller = new AssetController(serviceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                            authenticationType: "Test"))
                    }
                }
            };

            return controller;
        }

        [Fact]
        public async Task UpdateStock_ReturnsOkAndCallsService()
        {
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var instrumentId = Guid.NewGuid();
            var request = new UpdateStockRequest { Name = "TCS", Symbol = "TCS", Exchange = "NSE", Quantity = 1m, Price = 100m, Date = DateTime.UtcNow };
            var response = new AssetIngestResponse { InstrumentId = instrumentId, InstrumentName = "TCS", Symbol = "NSE:TCS", TransactionId = Guid.NewGuid(), Message = "updated" };

            var service = new Mock<IAssetInstrumentService>();
            service.Setup(s => s.UpdateStockAsync(userId, profileId, instrumentId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AssetIngestResponse>.Success(response));

            var controller = CreateController(service, userId);
            var result = await controller.UpdateStock(profileId, instrumentId, request, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(response, ok.Value);
            service.VerifyAll();
        }

        [Fact]
        public async Task UpdateMutualFund_ReturnsOkAndCallsService()
        {
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var instrumentId = Guid.NewGuid();
            var request = new UpdateMutualFundRequest { SchemeName = "PPFAS", SchemeCode = "120503", Units = 1m, NavPerUnit = 10m, Date = DateTime.UtcNow };
            var response = new AssetIngestResponse { InstrumentId = instrumentId, InstrumentName = "PPFAS", Symbol = "120503", TransactionId = Guid.NewGuid(), Message = "updated" };

            var service = new Mock<IAssetInstrumentService>();
            service.Setup(s => s.UpdateMutualFundAsync(userId, profileId, instrumentId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AssetIngestResponse>.Success(response));

            var controller = CreateController(service, userId);
            var result = await controller.UpdateMutualFund(profileId, instrumentId, request, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(response, ok.Value);
            service.VerifyAll();
        }

        [Fact]
        public async Task UpdateGold_ReturnsOkAndCallsService()
        {
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var instrumentId = Guid.NewGuid();
            var request = new UpdateGoldRequest { Form = "Digital", Purity = "24K", WeightGrams = 1m, RatePerGram = 7000m, Date = DateTime.UtcNow };
            var response = new AssetIngestResponse { InstrumentId = instrumentId, InstrumentName = "Gold", Symbol = "GOLD:24K:DIGITAL", TransactionId = Guid.NewGuid(), Message = "updated" };

            var service = new Mock<IAssetInstrumentService>();
            service.Setup(s => s.UpdateGoldAsync(userId, profileId, instrumentId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AssetIngestResponse>.Success(response));

            var controller = CreateController(service, userId);
            var result = await controller.UpdateGold(profileId, instrumentId, request, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(response, ok.Value);
            service.VerifyAll();
        }

        [Fact]
        public async Task UpdatePpf_ReturnsOkAndCallsService()
        {
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var instrumentId = Guid.NewGuid();
            var request = new UpdatePpfRequest { AccountNo = "PPF001", OpenedOn = DateTime.UtcNow.AddYears(-1), CurrentRatePercent = 7.1m, InitialContribution = 50000m, ContributionDate = DateTime.UtcNow };
            var response = new AssetIngestResponse { InstrumentId = instrumentId, InstrumentName = "PPF", Symbol = "PPF:PPF001", TransactionId = Guid.NewGuid(), Message = "updated" };

            var service = new Mock<IAssetInstrumentService>();
            service.Setup(s => s.UpdatePpfAsync(userId, profileId, instrumentId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AssetIngestResponse>.Success(response));

            var controller = CreateController(service, userId);
            var result = await controller.UpdatePpf(profileId, instrumentId, request, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(response, ok.Value);
            service.VerifyAll();
        }

        [Fact]
        public async Task UpdateFixedDeposit_ReturnsOkAndCallsService()
        {
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var instrumentId = Guid.NewGuid();
            var request = new UpdateFixedDepositRequest { Bank = "HDFC", AccountNo = "FD1", Principal = 100000m, RatePercent = 7m, StartDate = DateTime.UtcNow.AddYears(-1), MaturityDate = DateTime.UtcNow.AddYears(1) };
            var response = new AssetIngestResponse { InstrumentId = instrumentId, InstrumentName = "FD", Symbol = "FD:HDFC:FD1", TransactionId = Guid.NewGuid(), Message = "updated" };

            var service = new Mock<IAssetInstrumentService>();
            service.Setup(s => s.UpdateFixedDepositAsync(userId, profileId, instrumentId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AssetIngestResponse>.Success(response));

            var controller = CreateController(service, userId);
            var result = await controller.UpdateFixedDeposit(profileId, instrumentId, request, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(response, ok.Value);
            service.VerifyAll();
        }

        [Fact]
        public async Task UpdateRecurringDeposit_ReturnsOkAndCallsService()
        {
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var instrumentId = Guid.NewGuid();
            var request = new UpdateRecurringDepositRequest { Bank = "ICICI", AccountNo = "RD1", MonthlyAmount = 5000m, RatePercent = 6.5m, StartDate = DateTime.UtcNow.AddMonths(-1), TenureMonths = 12 };
            var response = new AssetIngestResponse { InstrumentId = instrumentId, InstrumentName = "RD", Symbol = "RD:ICICI:RD1", TransactionId = Guid.NewGuid(), Message = "updated" };

            var service = new Mock<IAssetInstrumentService>();
            service.Setup(s => s.UpdateRecurringDepositAsync(userId, profileId, instrumentId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AssetIngestResponse>.Success(response));

            var controller = CreateController(service, userId);
            var result = await controller.UpdateRecurringDeposit(profileId, instrumentId, request, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(response, ok.Value);
            service.VerifyAll();
        }
    }
}
