namespace EntraRadius.Models
{
    public class RadiusRequest
    {
        public required string UserName { get; set; }
        /// <summary>
        /// This should be clear text.
        /// Our authentiaction method will be EAP-TTLS with PAP.
        /// </summary>
        public required string Password { get; set; }
    }
}
