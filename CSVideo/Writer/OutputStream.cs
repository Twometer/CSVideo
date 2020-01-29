using FFmpeg.AutoGen;

namespace CSVideo.Writer
{
    internal unsafe class OutputStream
    {
        public AVStream* st;
        public AVCodecContext* enc;

        public long nextPts;
        public int samplesCount;
        public AVFrame* frame;
        public AVFrame* tempFrame;

        public SwsContext* swsCtx;
        public SwrContext* swrCtx;
    }
}
