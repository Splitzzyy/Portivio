using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Portivio.API.Controllers;
using Portivio.API.Services;
using Portivio.Application.DTOs.Auth;
using Portivio.Application.Results;
using Portivio.Application.Services;
using Xunit;

namespace Portivio.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly Mock<IAuthHttpContextService> _authHttpContextServiceMock;
        private readonly Mock<IBackgroundJobClient> _jobClientMock;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _authServiceMock = new Mock<IAuthService>();
            _authHttpContextServiceMock = new Mock<IAuthHttpContextService>();
            _jobClientMock = new Mock<IBackgroundJobClient>();

            _controller = new AuthController(
                _authServiceMock.Object,
                _authHttpContextServiceMock.Object,
                _jobClientMock.Object);
        }

        [Fact]
        public async Task Login_Success_EnqueuesHoldingRecalculationJob()
        {
            // Arrange
            var loginRequest = new LoginRequest { Email = "test@example.com", Password = "Password123" };
            var authResponse = new AuthResponse { AccessToken = "access-token" };
            var result = Result<AuthResponse>.Success(authResponse);

            _authHttpContextServiceMock.Setup(s => s.CreateLoginRequest(It.IsAny<HttpContext>(), It.IsAny<LoginRequest>()))
                .Returns(loginRequest);
            
            _authServiceMock.Setup(s => s.LoginAsync(loginRequest))
                .ReturnsAsync(result);

            _authHttpContextServiceMock.Setup(s => s.CreateClientAuthResponse(It.IsAny<HttpContext>(), authResponse))
                .Returns(authResponse);

            // Act
            var actionResult = await _controller.Login(loginRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Equal(authResponse, okResult.Value);

            // Verify Hangfire job was enqueued
            _jobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "RunDailyRefreshAsync" && job.Type == typeof(IHoldingRecalculationService)),
                It.IsAny<EnqueuedState>()),
                Times.Once);
        }

        [Fact]
        public async Task GoogleLogin_Success_EnqueuesHoldingRecalculationJob()
        {
            // Arrange
            var googleRequest = new GoogleLoginRequest { Token = "google-id-token" };
            var authResponse = new AuthResponse { AccessToken = "access-token" };
            var result = Result<AuthResponse>.Success(authResponse);

            _authHttpContextServiceMock.Setup(s => s.CreateGoogleLoginRequest(It.IsAny<HttpContext>(), It.IsAny<GoogleLoginRequest>()))
                .Returns(googleRequest);
            
            _authServiceMock.Setup(s => s.GoogleLoginAsync(googleRequest))
                .ReturnsAsync(result);

            _authHttpContextServiceMock.Setup(s => s.CreateClientAuthResponse(It.IsAny<HttpContext>(), authResponse))
                .Returns(authResponse);

            // Act
            var actionResult = await _controller.GoogleLogin(googleRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Equal(authResponse, okResult.Value);

            // Verify Hangfire job was enqueued
            _jobClientMock.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "RunDailyRefreshAsync" && job.Type == typeof(IHoldingRecalculationService)),
                It.IsAny<EnqueuedState>()),
                Times.Once);
        }
    }
}
