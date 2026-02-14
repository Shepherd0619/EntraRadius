using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using EntraRadius.Models;

namespace EntraRadius.Services
{
    public class GraphClientService
    {
        private readonly EntraConfiguration _config;
        private readonly ILogger<GraphClientService> _logger;
        private readonly IPublicClientApplication _publicClientApp;

        public GraphClientService(IOptions<EntraConfiguration> config, ILogger<GraphClientService> logger)
        {
            _config = config.Value;
            _logger = logger;

            _publicClientApp = PublicClientApplicationBuilder
                .Create(_config.ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, _config.TenantId)
                .Build();
        }

        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            try
            {
                var result = await _publicClientApp
                    .AcquireTokenByUsernamePassword(_config.Scopes, username, password)
                    .ExecuteAsync();

                if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                {
                    _logger.LogInformation("Successfully authenticated user {Username} with Entra", username);
                    return true;
                }

                _logger.LogWarning("Authentication failed for user {Username} - no access token received", username);
                return false;
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "Entra service error during authentication for user {Username}: {ErrorCode}", username, ex.ErrorCode);
                throw new EntraServiceException("Entra service is unreachable or returned an error", ex);
            }
            catch (MsalClientException ex)
            {
                _logger.LogError(ex, "Client error during authentication for user {Username}: {ErrorCode}", username, ex.ErrorCode);
                return false;
            }
            catch (MsalException ex)
            {
                _logger.LogError(ex, "Authentication failed for user {Username}: {ErrorCode}", username, ex.ErrorCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during authentication for user {Username}", username);
                throw new EntraServiceException("Unexpected error during authentication", ex);
            }
        }
    }

    public class EntraServiceException : Exception
    {
        public EntraServiceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
