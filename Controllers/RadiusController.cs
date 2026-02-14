using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using EntraRadius.Services;
using EntraRadius.Models;

namespace EntraRadius.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RadiusController : ControllerBase
    {
        private readonly GraphClientService _graphClientService;
        private readonly IUserCacheService _userCacheService;
        private readonly EntraConfiguration _config;
        private readonly ILogger<RadiusController> _logger;

        public RadiusController(
            GraphClientService graphClientService,
            IUserCacheService userCacheService,
            IOptions<EntraConfiguration> config,
            ILogger<RadiusController> logger)
        {
            _graphClientService = graphClientService;
            _userCacheService = userCacheService;
            _config = config.Value;
            _logger = logger;
        }

        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] RadiusRequest request)
        {
            if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
            {
                _logger.LogWarning("Authentication attempt with missing username or password");
                return BadRequest(new { message = "Username and password are required" });
            }

            try
            {
                // Try to authenticate against Entra first
                var isAuthenticated = await _graphClientService.AuthenticateAsync(request.UserName, request.Password);

                if (isAuthenticated)
                {
                    // Cache the successful authentication
                    var cacheDuration = TimeSpan.FromMinutes(_config.CacheDurationMinutes);
                    _userCacheService.CacheUser(request.UserName, request.Password, cacheDuration);

                    _logger.LogInformation("User {Username} authenticated successfully via Entra", request.UserName);
                    return Ok(new { message = "Authentication successful", source = "entra" });
                }
                else
                {
                    _logger.LogWarning("User {Username} failed authentication via Entra", request.UserName);
                    return Unauthorized(new { message = "Authentication failed" });
                }
            }
            catch (EntraServiceException ex)
            {
                // Entra is unreachable, fallback to cache
                _logger.LogWarning(ex, "Entra service is unreachable, attempting cache fallback for user {Username}", request.UserName);

                var isValidInCache = _userCacheService.ValidateFromCache(request.UserName, request.Password);

                if (isValidInCache)
                {
                    _logger.LogInformation("User {Username} authenticated successfully via cache fallback", request.UserName);
                    return Ok(new { message = "Authentication successful (fallback)", source = "cache" });
                }
                else
                {
                    _logger.LogWarning("User {Username} failed authentication via cache fallback", request.UserName);
                    return new ObjectResult(new { message = "Authentication failed - Entra is unreachable and user not found in cache" })
                    {
                        StatusCode = StatusCodes.Status503ServiceUnavailable
                    };
                }
            }
        }
    }
}
