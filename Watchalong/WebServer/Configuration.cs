using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace WebServer
{
    public class Configuration
    {
        /// <summary>
        /// The IP to host the media server on
        /// </summary>
        [YamlMember(Alias = "IP for Web Interface")]
        public string WebHostIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// The port to host the media server on
        /// </summary>
        [YamlMember(Alias = "Port for Web Interface")]
        public ushort WebHostPort { get; set; } = 20320;

        /// <summary>
        /// The IP to host the media server on
        /// </summary>
        [YamlMember(Alias = "IP for WebMedia Link")]
        public string MediaserverHostIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// The port to host the media server on
        /// </summary>
        [YamlMember(Alias = "Port for WebMedia Link")]
        public ushort MediaserverHostPort { get; set; } = 20322;
    }
}
