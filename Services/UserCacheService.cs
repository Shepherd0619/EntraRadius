using Microsoft.Extensions.Caching.Memory;

namespace EntraRadius.Services
{
    public class UserCacheService : IUserCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<UserCacheService> _logger;

        public UserCacheService(IMemoryCache memoryCache, ILogger<UserCacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public void CacheUser(string username, string password, TimeSpan expiration)
        {
            var cacheKey = GetCacheKey(username);
            var hashedPassword = HashPassword(password);

            _memoryCache.Set(cacheKey, hashedPassword, expiration);
            _logger.LogInformation("Cached user {Username} for {Minutes} minutes", username, expiration.TotalMinutes);
        }

        public bool ValidateFromCache(string username, string password)
        {
            var cacheKey = GetCacheKey(username);

            if (_memoryCache.TryGetValue(cacheKey, out string? cachedHashedPassword))
            {
                var hashedPassword = HashPassword(password);
                var isValid = cachedHashedPassword == hashedPassword;

                if (isValid)
                {
                    _logger.LogInformation("User {Username} validated from cache", username);
                }
                else
                {
                    _logger.LogWarning("User {Username} found in cache but password mismatch", username);
                }

                return isValid;
            }

            _logger.LogInformation("User {Username} not found in cache", username);
            return false;
        }

        private string GetCacheKey(string username)
        {
            return $"user:{username.ToLowerInvariant()}";
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
