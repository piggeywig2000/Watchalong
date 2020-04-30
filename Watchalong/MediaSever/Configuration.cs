using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using Watchalong.Utils;

namespace MediaSever
{
    /// <summary>
    /// The YAML configuration
    /// </summary>
    class Configuration
    {
        /// <summary>
        /// The name of the media server
        /// </summary>
        [YamlMember(Alias = "Name of media server")]
        public string Name { get; set; } = "Untitled";

        /// <summary>
        /// The password of the media server
        /// </summary>
        [YamlMember(Alias = "Password (leave blank for no password)")]
        public string Password { get; set; } = "";

        /// <summary>
        /// The IP of the web server
        /// </summary>
        [YamlMember(Alias = "IP of WebMedia Link")]
        public string WebserverIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// The port of the web server
        /// </summary>
        [YamlMember(Alias = "Port of WebMedia Link")]
        public ushort WebserverPort { get; set; } = 20322;

        /// <summary>
        /// The root folder containing the media files
        /// </summary>
        [YamlMember(Alias = "Path to folder containing media files")]
        public string PathToMedia { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        /// <summary>
        /// The root folder containing the downloaded files
        /// </summary>
        [YamlMember(Alias = "Path to folder containing downloaded files")]
        public string PathToDownload { get; set; } = Helpful.GetExecutingDirectory() + "\\download";

        /// <summary>
        /// The root folder containing the subtitle files
        /// </summary>
        [YamlMember(Alias = "Path to folder containing subtitle files")]
        public string PathToSubtitle { get; set; } = Helpful.GetExecutingDirectory() + "\\subtitle";

        /// <summary>
        /// The IP to host the media server on
        /// </summary>
        [YamlMember(Alias = "IP for HTTP server")]
        public string HttpHostIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// The port to host the media server on
        /// </summary>
        [YamlMember(Alias = "Port for HTTP server")]
        public ushort HttpHostPort { get; set; } = 20321;
    }
}
