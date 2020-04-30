using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Watchalong.Utils;
using WebMediaLink;

namespace WebServer
{
    public class ServerFile : PlayableFile
    {
        /// <summary>
        /// The UUID of the file
        /// </summary>
        public int UUID { get; } = 0;

        /// <summary>
        /// Creates a new file object, generating a new UUID for it
        /// </summary>
        /// <param name="videoUrl">The url of the video</param>
        /// <param name="audioUrl">The url of the audio</param>
        /// <param name="title">The the title of the file</param>
        /// <param name="subtitles">The subtitles of this file</param>
        /// <param name="duration">The duration of the file</param>
        /// <param name="isFileAvailable">Whether the file is available</param>
        /// <param name="type">Whether it's stored offline or it's downloaded</param>
        public ServerFile(string videoUrl, string audioUrl, string title, SubtitleInfo[] subtitles, double duration, bool isFileAvailable, FileType type) : base (videoUrl, audioUrl, title, subtitles, duration, isFileAvailable, type)
        {
            UUID = Uuid.GetUuid();
        }

        /// <summary>
        /// Creates a new file object with a provided UUID
        /// </summary>
        /// <param name="uuid">The UUID of the file object</param>
        /// <param name="videoUrl">The url of the video</param>
        /// <param name="audioUrl">The url of the audio</param>
        /// <param name="title">The the title of the file</param>
        /// <param name="subtitles">The subtitles of this file</param>
        /// <param name="duration">The duration of the file</param>
        /// <param name="isFileAvailable">Whether the file is available</param>
        /// <param name="type">Whether it's stored offline or it's downloaded</param>
        public ServerFile(int uuid, string videoUrl, string audioUrl, string title, SubtitleInfo[] subtitles, double duration, bool isFileAvailable, FileType type) : base(videoUrl, audioUrl, title, subtitles, duration, isFileAvailable, type)
        {
            UUID = uuid;
        }
    }
}
