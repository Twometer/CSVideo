using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFmpeg.AutoGen;

namespace CSVideo
{
    public class FFmpegLoader
    {
        private static bool IsLoaded { get; set; } = false;

        public static bool Load(string libraryLocation)
        {
            try
            {
                ffmpeg.RootPath = libraryLocation;
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
