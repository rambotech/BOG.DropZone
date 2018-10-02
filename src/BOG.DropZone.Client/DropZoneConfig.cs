namespace BOG.DropZone.Client
{
    public class DropZoneConfig
    {
        public string BaseUrl { get; set; } = null;
        public string ZoneName { get; set; } = null;
        public string AccessToken { get; set; } = null;
        public string AdminToken { get; set; } = null;
        public string Password { get; set; } = null;
        public string Salt { get; set; } = null;
        public bool UseEncryption { get; set; } = false;
        public int TimeoutSeconds { get; set; } = 15;
    }
}
