﻿using System;
using System.Collections.Generic;
using System.Text;

namespace WebMediaLink
{
    public enum FileType { Offline, Downloaded }

    public class PlayableFile
    {
        /// <summary>
        /// The url of the video
        /// </summary>
        public string VideoUrl { get; set; } = null;

        /// <summary>
        /// The url of the audio
        /// </summary>
        public string AudioUrl { get; set; } = null;

        /// <summary>
        /// The title of the file
        /// </summary>
        public string Title { get; set; } = null;

        /// <summary>
        /// The sha1 hash of the file
        /// </summary>
        public string Sha1 { get; set; } = null;

        /// <summary>
        /// The subtitles that this file has
        /// </summary>
        public SubtitleInfo[] Subtitles { get; set; } = null;

        /// <summary>
        /// Whether the file is available for download
        /// </summary>
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Whether it's stored offline or it's downloaded
        /// </summary>
        public FileType Type { get; } = FileType.Offline;

        /// <summary>
        /// The duration of the file
        /// </summary>
        public double Duration { get; set; } = 0;

        /// <summary>
        /// Creates a new file object, generating a new UUID for it
        /// </summary>
        /// <param name="videoUrl">The url of the video</param>
        /// <param name="audioUrl">The audio of the audio</param>
        /// <param name="title">The title of the file</param>
        /// <param name="subtitles">The subtitles of this file</param>
        /// <param name="duration">The duration of the file</param>
        /// <param name="isFileAvailable">Whether the file is available</param>
        public PlayableFile(string videoUrl, string audioUrl, string title, string sha1, SubtitleInfo[] subtitles, double duration, bool isFileAvailable, FileType type)
        {
            VideoUrl = videoUrl;
            AudioUrl = audioUrl;
            Title = title;
            Sha1 = sha1;
            Subtitles = subtitles;
            Duration = duration;
            IsAvailable = isFileAvailable;
            Type = type;
        }

        public bool CompareTo(PlayableFile fileToCompareAgainst)
        {
            return (VideoUrl == fileToCompareAgainst.VideoUrl && AudioUrl == fileToCompareAgainst.AudioUrl);
        }
    }
}
