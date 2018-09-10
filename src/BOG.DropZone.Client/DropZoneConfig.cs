using System;
using System.Collections.Generic;
using System.Text;

namespace BOG.DropZone.Client
{
    public class DropZoneConfig
    {
        public string BaseUrl { get; set; } = null;
        public string AccessToken { get; set; } = null;
        public string Password { get; set; } = null;
        public string Salt { get; set; } = null;
    }
}
