using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

using static FFmpeg.AutoGen.ffmpeg;

namespace CSVideo.Writer
{
    public unsafe class VideoWriter : IDisposable
    {
        public string Filename { get; set; }

        public int VideoBitrate { get; set; } = 4_000_000; // 4 Mbit/s

        public int AudioBitrate { get; set; } = 128_000; // 128 Kbit/s

        public int AudioSampleRate { get; set; } = 44100; // 44100 Hz

        public int Width { get; set; } = 1920;

        public int Height { get; set; } = 1080;

        public int Fps { get; set; } = 25;

        public int Channels { get; set; } = 2;

        public int AudioSamplesPerFrame => audioStream.tempFrame->nb_samples * Channels;

        public bool WriteVideo => av_compare_ts(videoStream.nextPts, videoStream.enc->time_base, audioStream.nextPts, audioStream.enc->time_base) <= 0;

        private OutputStream videoStream;
        private OutputStream audioStream;

        private AVOutputFormat* fmt;
        private AVFormatContext* ctx;
        private AVCodec* audioCodec;
        private AVCodec* videoCodec;

        private bool hasAudio;
        private bool hasVideo;

        private bool closed;

        public VideoWriter(string filename)
        {
            Filename = filename;
            videoStream = new OutputStream();
            audioStream = new OutputStream();
        }

        public void Open()
        {
            FFmpegLoader.EnsureLoaded();

            if (closed)
                throw new InvalidOperationException("Cannot reopen closed video writer");

            fixed (AVFormatContext** fctx = &ctx)
                avformat_alloc_output_context2(fctx, null, null, Filename);

            this.fmt = ctx->oformat;

            if (fmt->video_codec != AVCodecID.AV_CODEC_ID_NONE)
            {
                AddStream(ref videoStream, ref videoCodec, fmt->video_codec);
                hasVideo = true;
            }

            if (fmt->audio_codec != AVCodecID.AV_CODEC_ID_NONE)
            {
                AddStream(ref audioStream, ref audioCodec, fmt->audio_codec);
                hasAudio = true;
            }

            int ret;
            if (hasVideo)
                OpenVideo();
            if (hasAudio)
                OpenAudio();

            // av_dump_format(ctx, 0, Filename, 1);

            ret = avio_open(&ctx->pb, Filename, AVIO_FLAG_WRITE);
            if (ret < 0)
                throw new FFmpegException("Could not open file for writing", ret);

            ret = avformat_write_header(ctx, null);
            if (ret < 0)
                throw new FFmpegException("Could not write file header", ret);
        }

        public void Close()
        {
            if (closed) return;
            closed = true;

            av_write_trailer(ctx);

            if (hasVideo)
                CloseStream(videoStream);
            if (hasAudio)
                CloseStream(audioStream);

            avio_closep(&ctx->pb);
            avformat_free_context(ctx);
        }

        private void OpenVideo()
        {
            int ret;
            AVCodecContext* c = videoStream.enc;

            ret = avcodec_open2(c, videoCodec, null);
            if (ret < 0)
                throw new FFmpegException("Could not open codec", ret);

            videoStream.frame = AllocVideoFrame(c->pix_fmt, c->width, c->height);
            if (c->pix_fmt != AVPixelFormat.AV_PIX_FMT_BGR24)
            {
                videoStream.tempFrame = AllocVideoFrame(AVPixelFormat.AV_PIX_FMT_YUV420P, c->width, c->height);
            }

            ret = avcodec_parameters_from_context(videoStream.st->codecpar, c);
            if (ret < 0)
                throw new FFmpegException("Could not copy stream parameters", ret);
        }

        private void OpenAudio()
        {
            AVCodecContext* c;
            int nb_samples;
            int ret;
            c = audioStream.enc;

            /* open it */
            ret = avcodec_open2(c, audioCodec, null);
            if (ret < 0)
            {
                throw new FFmpegException("Could not open audio codec", ret);
            }

            if ((c->codec->capabilities & AV_CODEC_CAP_VARIABLE_FRAME_SIZE) != 0)
                nb_samples = 10000;
            else
                nb_samples = c->frame_size;
            audioStream.frame = AllocAudioFrame(c->sample_fmt, c->channel_layout,
                                               c->sample_rate, nb_samples);
            audioStream.tempFrame = AllocAudioFrame(AVSampleFormat.AV_SAMPLE_FMT_FLT, c->channel_layout,
                                               c->sample_rate, nb_samples);
            /* copy the stream parameters to the muxer */
            ret = avcodec_parameters_from_context(audioStream.st->codecpar, c);
            if (ret < 0)
            {
                throw new FFmpegException("Could not copy stream parameters", ret);
            }

            /* create resampler context */
            audioStream.swrCtx = swr_alloc();
            if (audioStream.swrCtx == null)
            {
                throw new FFmpegException("Could not allocate resampler context");
            }
            /* set options */
            av_opt_set_int(audioStream.swrCtx, "in_channel_count", c->channels, 0);
            av_opt_set_int(audioStream.swrCtx, "in_sample_rate", c->sample_rate, 0);
            av_opt_set_sample_fmt(audioStream.swrCtx, "in_sample_fmt", AVSampleFormat.AV_SAMPLE_FMT_FLT, 0);
            av_opt_set_int(audioStream.swrCtx, "out_channel_count", c->channels, 0);
            av_opt_set_int(audioStream.swrCtx, "out_sample_rate", c->sample_rate, 0);
            av_opt_set_sample_fmt(audioStream.swrCtx, "out_sample_fmt", c->sample_fmt, 0);
            /* initialize the resampling context */
            if ((ret = swr_init(audioStream.swrCtx)) < 0)
            {
                throw new FFmpegException("Could not initialize the resampling context", ret);
            }
        }

        private void AddStream(ref OutputStream stream, ref AVCodec* codec, AVCodecID codecId)
        {
            AVCodecContext* c;

            codec = avcodec_find_encoder(codecId);
            if (codec == null)
                throw new NotSupportedException("No encoder for codec " + avcodec_get_name(codecId));

            stream.st = avformat_new_stream(ctx, null);
            if (stream.st == null)
                throw new FFmpegException("Could not allocate stream");

            stream.st->id = (int)(ctx->nb_streams - 1);

            c = avcodec_alloc_context3(codec);
            if (c == null)
                throw new FFmpegException("Could not allocate encoding context");
            stream.enc = c;

            if (codec->type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                c->sample_fmt = codec->sample_fmts != null ? codec->sample_fmts[0] : AVSampleFormat.AV_SAMPLE_FMT_FLTP;
                c->bit_rate = AudioBitrate;
                c->sample_rate = AudioSampleRate;
                c->channels = av_get_channel_layout_nb_channels(c->channel_layout);
                c->channel_layout = AV_CH_LAYOUT_STEREO;
                if (codec->channel_layouts != null)
                {
                    c->channel_layout = codec->channel_layouts[0];
                    for (int i = 0; codec->channel_layouts[i] != 0; i++)
                    {
                        if (codec->channel_layouts[i] == AV_CH_LAYOUT_STEREO)
                            c->channel_layout = AV_CH_LAYOUT_STEREO;
                    }
                }
                c->channels = av_get_channel_layout_nb_channels(c->channel_layout);
                stream.st->time_base = new AVRational() { num = 1, den = c->sample_rate };
            }
            else if (codec->type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                c->codec_id = codecId;
                c->bit_rate = VideoBitrate;
                c->width = Width;
                c->height = Height;
                stream.st->time_base = new AVRational() { num = 1, den = Fps };
                c->time_base = stream.st->time_base;
                c->gop_size = 12;
                c->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

                if (c->codec_id == AVCodecID.AV_CODEC_ID_MPEG1VIDEO)
                {
                    c->mb_decision = 2;
                }
            }

            if ((ctx->oformat->flags & AVFMT_GLOBALHEADER) != 0)
                c->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;
        }

        private AVFrame* AllocAudioFrame(AVSampleFormat sampleFmt, ulong channelLayout, int sampleRate, int nbSamples)
        {
            AVFrame* frame = av_frame_alloc();
            int ret;

            if (frame == null)
                throw new FFmpegException("Could not allocate audio frame");

            frame->format = (int)sampleFmt;
            frame->channel_layout = channelLayout;
            frame->sample_rate = sampleRate;
            frame->nb_samples = nbSamples;

            if (nbSamples != 0)
            {
                ret = av_frame_get_buffer(frame, 0);
                if (ret < 0)
                    throw new FFmpegException("Could not allocate audio buffer", ret);
            }
            return frame;
        }

        private AVFrame* AllocVideoFrame(AVPixelFormat pixFmt, int width, int height)
        {
            AVFrame* frame;
            int ret;

            frame = av_frame_alloc();
            if (frame == null)
                throw new FFmpegException("Could not allocate video frame");

            frame->format = (int)pixFmt;
            frame->width = width;
            frame->height = height;

            ret = av_frame_get_buffer(frame, 32);
            if (ret < 0)
                throw new FFmpegException("Could not allocate frame data", ret);

            return frame;
        }

        private int WriteFrame(AVRational* timeBase, AVStream* stream, AVPacket* pkt)
        {
            av_packet_rescale_ts(pkt, *timeBase, stream->time_base);
            pkt->stream_index = stream->index;
            return av_interleaved_write_frame(ctx, pkt);
        }

        public bool WriteVideoFrame(Bitmap bitmap)
        {
            int ret;
            AVCodecContext* c = videoStream.enc;
            AVFrame* frame;
            int got_packet = 0;
            AVPacket pkt = new AVPacket();

            frame = MakeVideoFrame(bitmap);

            av_init_packet(&pkt);

            ret = avcodec_encode_video2(c, &pkt, frame, &got_packet);
            if (ret < 0)
                throw new FFmpegException("Error encoding video frame", ret);

            if (got_packet != 0)
                ret = WriteFrame(&c->time_base, videoStream.st, &pkt);
            else
                ret = 0;

            if (ret < 0)
                throw new FFmpegException("Error writing video frame", ret);

            return (frame != null || got_packet != 0) ? false : true;
        }

        public bool WriteAudioFrame(float[] data)
        {
            AVCodecContext* c = audioStream.enc;
            AVPacket pkt = new AVPacket();
            AVFrame* frame;

            int ret;
            int got_packet;
            int dst_nb_samples;

            frame = MakeAudioFrame(data);
            if (frame != null)
            {
                dst_nb_samples = (int)av_rescale_rnd(swr_get_delay(audioStream.swrCtx, c->sample_rate) + frame->nb_samples, c->sample_rate, c->sample_rate, AVRounding.AV_ROUND_UP);
                if (dst_nb_samples != frame->nb_samples)
                    throw new FFmpegException("Invalid results when rescaling samples");

                ret = av_frame_make_writable(audioStream.frame);
                if (ret < 0)
                    throw new FFmpegException("Audio frame not writable", ret);

                byte*[] outData = audioStream.frame->data;
                byte*[] inData = frame->data;
                fixed (byte** fOutData = outData)
                fixed (byte** fInData = inData)
                    ret = swr_convert(audioStream.swrCtx, fOutData, dst_nb_samples, fInData, frame->nb_samples);

                audioStream.frame->data.UpdateFrom(outData);

                if (ret < 0)
                    throw new FFmpegException("Could not resample audio", ret);

                frame = audioStream.frame;
                frame->pts = av_rescale_q(audioStream.samplesCount, new AVRational() { num = 1, den = c->sample_rate }, c->time_base);
                audioStream.samplesCount += dst_nb_samples;
            }

            ret = avcodec_encode_audio2(c, &pkt, frame, &got_packet);
            if (ret < 0)
                throw new FFmpegException("Could not encode audio frame", ret);

            if (got_packet != 0)
            {
                ret = WriteFrame(&c->time_base, audioStream.st, &pkt);
                if (ret < 0)
                    throw new FFmpegException("Error while writing audio frame", ret);
            }


            return (frame != null || got_packet != 0) ? false : true;
        }

        private AVFrame* MakeAudioFrame(float[] data)
        {
            AVFrame* frame = audioStream.tempFrame;
            int j, i;
            float* q = (float*)frame->data[0];

            for (j = 0; j < frame->nb_samples; j++)
            {
                for (i = 0; i < audioStream.enc->channels; i++)
                {
                    if (Channels == 1)
                    {
                        *q++ = data[j];
                    }
                    else if (Channels == 2)
                    {
                        *q++ = data[2 * j + i];
                    }
                    else
                    {
                        throw new ArgumentException($"Expected 1 or 2 audio channels, but got {Channels}");
                    }
                }
            }
            frame->pts = audioStream.nextPts;
            audioStream.nextPts += frame->nb_samples;
            return frame;
        }

        private AVFrame* MakeVideoFrame(Bitmap bitmap)
        {
            AVCodecContext* c = videoStream.enc;

            if (av_frame_make_writable(videoStream.frame) < 0)
                throw new FFmpegException("Video frame not writable");

            if (videoStream.swsCtx == null)
            {
                videoStream.swsCtx = sws_getContext(c->width, c->height, AVPixelFormat.AV_PIX_FMT_BGR24, c->width, c->height, c->pix_fmt, SWS_BICUBIC, null, null, null);
                if (videoStream.swsCtx == null)
                    throw new FFmpegException("Could not initialize conversion context");
            }
            var ost = videoStream;

            ExtractBitmapData(ost.tempFrame, bitmap);
            sws_scale(ost.swsCtx, ost.tempFrame->data, ost.tempFrame->linesize, 0, c->height, ost.frame->data, ost.frame->linesize);

            videoStream.frame->pts = videoStream.nextPts++;
            return videoStream.frame;
        }

        private void ExtractBitmapData(AVFrame* frame, Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] data = new byte[bitmapData.Stride * bitmapData.Height];
                Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
                fixed (byte* pData = data)
                {
                    frame->data[0] = pData;
                    frame->linesize[0] = data.Length / bitmap.Height;
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        private void CloseStream(OutputStream stream)
        {
            var enc = stream.enc;
            var frame = stream.frame;
            var tmpFrame = stream.tempFrame;
            var swrCtx = stream.swrCtx;
            avcodec_free_context(&enc);
            av_frame_free(&frame);
            av_frame_free(&tmpFrame);
            sws_freeContext(stream.swsCtx);
            swr_free(&swrCtx);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
