# CSVideo
[![](https://img.shields.io/nuget/v/CSVideo)](https://www.nuget.org/packages/CSVideo) ![](https://img.shields.io/github/license/Twometer/CSVideo)

CSVideo is a C# Library for writing video files using FFmpeg.

I was searching for a good C# video writing library for a long time, and the only one
that could output a modern formats (H264 etc.) was Accord.FFmpeg. The framework however
requires a lot of extra libraries, and the video file writer is glitchy and does not work with audio.

This project aims to provide a lightweight and easy-to-use FFmpeg video writing library without
any of these problems.

## Design
One of the design goals for this library is an easy-to-use API. Part of this is not only a simple set of
exposed functions, but also the chosen default configuration. In contrast to libraries like Accord, the
default configuration for this library outputs high-quality audio and video in 1920x1080 resolution. However,
you can also change the configuration to your liking, if you want something more special.

## Usage
To use this library, you need the FFmpeg library files. I tested it with the libraries supplied by FFmpeg.AutoGen (v4.2),
which you can download from [here](https://github.com/Ruslan-B/FFmpeg.AutoGen/tree/master/FFmpeg/bin/x64), but you can
also try the latest version from the FFmpeg site. Once you have these libraries, you can use CSVideo as follows:

```csharp
FFmpegLoader.Load(@"C:\the\ffmpeg\folder");

using (var writer = new VideoWriter(OutputPath))
{
	writer.Open();
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
			writer.WriteAudioFrame(audioData);
		}
	}
}
```

For a more complete example, see the CSVideo.Example project.

## Audio Support
Supports mono and stereo floating-point audio samples, for example from a CSCore stream. The default is stereo, so
for mono streams you have to set `writer.Channels = 1`. It only supports mono and stereo, any different number of
channels will lead to an exception

## Credits
Thanks to [FFmpeg.AutoGen by Ruslan-B](https://github.com/Ruslan-B/FFmpeg.AutoGen) for the very nice C# FFmpeg wrapper

## Contributing
You can help me make CSVideo the go-to solution for video manipulation in C# by reporting bugs
in the GitHub issue tracker, or submitting pull requests. I appreciate every improvement to this repository.