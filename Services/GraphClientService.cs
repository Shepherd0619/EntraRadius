using System.Net.Http.Headers;
using System.Text.Json;
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
        private readonly HttpClient _httpClient;

        public GraphClientService(IOptions<EntraConfiguration> config, ILogger<GraphClientService> logger, IHttpClientFactory httpClientFactory)
        {
            _config = config.Value;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();

            _publicClientApp = PublicClientApplicationBuilder
                .Create(_config.ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, _config.TenantId)
                .Build();
        }

        public async Task<AuthenticationResult?> AuthenticateAsync(string username, string password)
        {
            try
            {
                var result = await _publicClientApp
                    .AcquireTokenByUsernamePassword(_config.Scopes, username, password)
                    .ExecuteAsync();

                if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                {
                    _logger.LogInformation("Successfully authenticated user {Username} with Entra", username);
                    return result;
                }

                _logger.LogWarning("Authentication failed for user {Username} - no access token received", username);
                return null;
            }
            catch (MsalServiceException ex)
            {
                if (ex.ErrorCode == "invalid_grant")
                {
                    _logger.LogWarning(ex, "Invalid credentials for user {Username}", username);
                    return null;
                }

                _logger.LogError(ex, "Entra service error during authentication for user {Username}: {ErrorCode}", username, ex.ErrorCode);
                throw new EntraServiceException("Entra service is unreachable or returned an error", ex);
            }
            catch (MsalClientException ex)
            {
                _logger.LogError(ex, "Client error during authentication for user {Username}: {ErrorCode}", username, ex.ErrorCode);
                return null;
            }
            catch (MsalException ex)
            {
                _logger.LogError(ex, "Authentication failed for user {Username}: {ErrorCode}", username, ex.ErrorCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during authentication for user {Username}", username);
                throw new EntraServiceException("Unexpected error during authentication", ex);
            }
        }

        public async Task<IReadOnlyList<string>> GetUserGroupsAsync(string accessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://graph.microsoft.com/v1.0/me/memberOf?$select=id");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var groupIds = new List<string>();
                if (doc.RootElement.TryGetProperty("value", out var values))
                {
                    foreach (var item in values.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var id))
                        {
                            var idString = id.GetString();
                            if (!string.IsNullOrEmpty(idString))
                                groupIds.Add(idString);
                        }
                    }
                }

                return groupIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve group membership from Graph API");
                throw new EntraServiceException("Failed to retrieve group membership", ex);
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
