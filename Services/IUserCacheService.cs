namespace EntraRadius.Services
{
    public interface IUserCacheService
    {
        void CacheUser(string username, string password, int? vlanId, TimeSpan expiration);
        (bool IsValid, int? VlanId) ValidateFromCache(string username, string password);
    }
}
