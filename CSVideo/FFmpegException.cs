using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVideo
{
    public class FFmpegException : Exception
    {
        public FFmpegException(string message) : base(message)
        {
        }

        public FFmpegException(string message, int errorCode)
            : base($"{message}: {ErrorToString(errorCode)}")
        {
        }

        private static unsafe string ErrorToString(int i)
        {
            var b = new byte[500];
            fixed (byte* bp = b)
                ffmpeg.av_strerror(i, bp, 500);
            return Encoding.ASCII.GetString(b);
        }
    }
}
