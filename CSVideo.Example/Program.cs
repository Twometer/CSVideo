using CSVideo.Writer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVideo.Example
{
    class Program
    {
        public static void Main(string[] args)
        {
            var success = FFmpegLoader.Load("");
            if (!success)
            {
                Console.WriteLine("Could not load FFmpeg");
                return;
            }

            Console.WriteLine($"Loaded FFmpeg v{FFmpegLoader.FFmpegVersion}");

            using (var writer = new VideoWriter("test.mp4"))
            {
                // TODO
            }

        }
    }
}
