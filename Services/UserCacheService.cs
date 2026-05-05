using Microsoft.Extensions.Caching.Memory;

namespace EntraRadius.Services
{
    public class UserCacheService : IUserCacheService
    {
        private readonly record struct CacheEntry(string HashedPassword, int? VlanId);

        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<UserCacheService> _logger;

        public UserCacheService(IMemoryCache memoryCache, ILogger<UserCacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public void CacheUser(string username, string password, int? vlanId, TimeSpan expiration)
        {
            var cacheKey = GetCacheKey(username);
            var entry = new CacheEntry(HashPassword(password), vlanId);
            _memoryCache.Set(cacheKey, entry, expiration);
            _logger.LogInformation("Cached user {Username} for {Minutes} minutes", username, expiration.TotalMinutes);
        }

        public (bool IsValid, int? VlanId) ValidateFromCache(string username, string password)
        {
            var cacheKey = GetCacheKey(username);

            if (_memoryCache.TryGetValue<CacheEntry>(cacheKey, out var entry))
            {
                var hashedPassword = HashPassword(password);
                var isValid = entry.HashedPassword == hashedPassword;

                if (isValid)
                {
                    _logger.LogInformation("User {Username} validated from cache", username);
                    return (true, entry.VlanId);
                }
                else
                {
                    _logger.LogWarning("User {Username} found in cache but password mismatch", username);
                    return (false, null);
                }
            }

            _logger.LogInformation("User {Username} not found in cache", username);
            return (false, null);
        }

        private string GetCacheKey(string username) => $"user:{username.ToLowerInvariant()}";

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
