using System;
using System.Collections.Generic;
using System.Text;

namespace WebMediaLink
{
    public class SubtitleInfo
    {
        /// <summary>
        /// The URL of the subtitle file
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// The name of the subtitle
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// The language of the subtitle
        /// </summary>
        public string Language { get; set; } = null;

        /// <summary>
        /// The codec of the subtitle
        /// </summary>
        public int CodecId { get; set; } = 0;
    }
}
