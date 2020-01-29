using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFmpeg.AutoGen;

namespace CSVideo
{
    public class FFmpegLoader
    {
        public static string FFmpegVersion
        {
            get
            {
                EnsureLoaded();
                return ffmpeg.av_version_info();
            }
        }

        private static bool IsLoaded { get; set; } = false;

        public static bool Load(string libraryLocation)
        {
            var dir = new DirectoryInfo(libraryLocation);
            if (!dir.Exists)
                return false;

            try
            {
                ffmpeg.RootPath = dir.FullName;
                ffmpeg.av_version_info();
                IsLoaded = true;
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }

        internal static void EnsureLoaded()
        {
            if (!IsLoaded)
            {
                throw new FFmpegException("The FFmpeg libraries are not loaded. Please call FFmpegLoader.Load() before attempting to use ffmpeg.");
            }
        }

    }
}
