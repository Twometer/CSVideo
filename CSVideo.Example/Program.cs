using CSCore;
using CSCore.Codecs;
using CSVideo.Writer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVideo.Example
{
    class Program
    {
        private const string FFmpegPath = @"";

        private const string ImagePath = @"";

        private const string AudioPath = @"";

        private const string OutputPath = @"";

        public static void Main(string[] args)
        {
            var success = FFmpegLoader.Load(FFmpegPath);
            if (!success)
            {
                Console.WriteLine("Could not load FFmpeg");
                return;
            }

            Console.WriteLine($"Loaded FFmpeg v{FFmpegLoader.FFmpegVersion}");

            var bitmap = (Bitmap)Image.FromFile(ImagePath);

            var audio = CodecFactory.Instance.GetCodec(AudioPath)
                .ToSampleSource();


            using (var writer = new VideoWriter(OutputPath))
            {
                writer.Open();

                float[] audioData = new float[writer.AudioSamplesPerFrame];

                while (true)
                {
                    if (writer.WriteVideo)
                    {
                        // Write a video frame
                        writer.WriteVideoFrame(bitmap);
                    }
                    else
                    {
                        // Write an audio frame
                        int read = audio.Read(audioData, 0, audioData.Length);
                        if (read <= 0)
                            break;

                        writer.WriteAudioFrame(audioData);
                    }
                }
            }

        }
    }
}
