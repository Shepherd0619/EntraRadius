using Microsoft.Extensions.Options;
using EntraRadius.Models;

namespace EntraRadius.Services
{
    public class VlanMappingService
    {
        private readonly VlanConfiguration _config;

        public VlanMappingService(IOptions<VlanConfiguration> config)
        {
            _config = config.Value;
        }

        public int? ResolveVlanId(IReadOnlyList<string> groupIds)
        {
            foreach (var mapping in _config.Mappings)
            {
                if (groupIds.Contains(mapping.GroupId, StringComparer.OrdinalIgnoreCase))
                {
                    return mapping.VlanId;
                }
            }

            return _config.DefaultVlanId;
        }
    }
}
