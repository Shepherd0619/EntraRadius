namespace EntraRadius.Services
{
    public interface IUserCacheService
    {
        void CacheUser(string username, string password, TimeSpan expiration);
        bool ValidateFromCache(string username, string password);
    }
}
