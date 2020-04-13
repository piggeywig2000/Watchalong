using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RunProcessAsTask;
using Watchalong.Utils;

namespace MediaSever
{
    public class DownloadInfo
    {
        [JsonProperty(PropertyName = "fulltitle")]
        public string Title { get; set; } = null;

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; } = null;

        [JsonProperty(PropertyName = "url")]
        public string Url { get; set; } = null;

        [JsonProperty(PropertyName = "ext")]
        public string Extension { get; set; } = null;

        [JsonProperty(PropertyName = "requested_formats")]
        public DownloadInfoRequestedFormat[] RequestedUrls { get; set; } = null;

        [JsonProperty(PropertyName = "duration")]
        public double Duration { get; set; } = 0;

        [JsonProperty(PropertyName = "vcodec")]
        public string VideoCodec { get; set; } = null;

        [JsonProperty(PropertyName = "acodec")]
        public string AudioCodec { get; set; } = null;
    }

    public class DownloadInfoRequestedFormat
    {
        [JsonProperty(PropertyName = "url")]
        public string Url { get; set; } = null;

        [JsonProperty(PropertyName = "vcodec")]
        public string VideoCodec { get; set; } = null;

        [JsonProperty(PropertyName = "acodec")]
        public string AudioCodec { get; set; } = null;
    }

    public class YoutubeDL
    {
        /// <summary>
        /// The path to youtubedl.exe
        /// </summary>
        private string PathToExecutable { get; } = "";

        /// <summary>
        /// Token that is triggered when the client disconnects
        /// </summary>
        private CancellationToken CancelToken { get; set; }

        /// <summary>
        /// Creates a new youtubedl instance with the specified path to executable
        /// </summary>
        /// <param name="pathToExe">The path to the executable</param>
        /// <param name="cancelToken">The cancellation token to stop the downloads</param>
        public YoutubeDL(string pathToExe, CancellationToken cancelToken)
        {
            PathToExecutable = pathToExe;
            CancelToken = cancelToken;
        }

        public async Task Init()
        {
            ConLog.Log("YouTubeDL", "Initiating YouTubeDL", LogType.Info);

            //Check that the file exists - if it doesn't, download it
            if (!File.Exists(PathToExecutable))
            {
                ConLog.Log("YouTubeDL", "YouTubeDL executable not found! Downloading...", LogType.Warning);
                using (WebClient client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(new Uri("https://yt-dl.org/downloads/latest/youtube-dl.exe"), PathToExecutable);
                }
            }

            ConLog.Log("YouTubeDL", "Initiated YouTubeDL", LogType.Ok);
        }


        private async Task<string> RunProcessAndGetOutput(string arguments)
        {
            ProcessResults result = await ProcessEx.RunAsync(new ProcessStartInfo
            {
                FileName = PathToExecutable,
                Arguments = arguments
            }, CancelToken);

            //Get result
            //Check for error
            if (result.ExitCode != 0) return null;

            //Return standard output
            return string.Join("\n", result.StandardOutput);
        }

        /// <summary>
        /// Downloads the information from a URL
        /// </summary>
        /// <param name="url">The URL to get the information of</param>
        /// <returns>The information downloaded. Null if the download failed</returns>
        public async Task<DownloadInfo> GetDownloadInfoFromUrl(string url)
        {
            ConLog.Log("YouTubeDL", "Downloading info from " + url, LogType.Info);

            string output = await RunProcessAndGetOutput("--no-playlist --quiet --dump-json -f \"bestvideo+bestaudio/best/bestaudio/bestvideo\" --merge-output-format \"mp4\" \"" + url + "\"");

            //Check for error
            if (output == null)
            {
                ConLog.Log("YouTubeDL", "Failed to download info from " + url, LogType.Warning);
                return null;
            }

            //Get json
            DownloadInfo result = JsonConvert.DeserializeObject<DownloadInfo>(output);

            ConLog.Log("YouTubeDL", "Downloaded info from " + url, LogType.Ok);
            return result;
        }

        /// <summary>
        /// Downloads a file from a URL
        /// </summary>
        /// <param name="url">The URL to download from</param>
        /// <param name="pathToDownloadInto">The path of the file when it is downloaded</param>
        /// <returns>True if it was successful, false if not</returns>
        public async Task<bool> DownloadFileFromUrl(string url, string pathToDownloadInto)
        {
            ConLog.Log("YouTubeDL", "Downloading " + url, LogType.Info);

            string output = await RunProcessAndGetOutput("--no-playlist --quiet -f \"bestvideo+bestaudio/best/bestaudio/bestvideo\" --merge-output-format \"mp4\" -o \"" + pathToDownloadInto + "\" \"" + url + "\"");

            //Check for error
            if (output == null)
            {
                ConLog.Log("YouTubeDL", "Failed to download " + url, LogType.Warning);
                return false;
            }
            else
            {
                ConLog.Log("YouTubeDL", "Downloaded " + url, LogType.Ok);
                return true;
            }
        }
    }
}
