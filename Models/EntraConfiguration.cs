namespace EntraRadius.Models
{
    public class EntraConfiguration
    {
        public string TenantId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = Array.Empty<string>();
        public int CacheDurationMinutes { get; set; } = 60;
    }
}
