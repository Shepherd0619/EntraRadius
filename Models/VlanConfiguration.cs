namespace EntraRadius.Models
{
    public class VlanGroupMapping
    {
        public string GroupId { get; set; } = string.Empty;
        public int VlanId { get; set; }
    }

    public class VlanConfiguration
    {
        public List<VlanGroupMapping> Mappings { get; set; } = new();
        public int? DefaultVlanId { get; set; }
    }
}
