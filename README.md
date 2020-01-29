# CSVideo
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
...

## Audio Support
Supports mono and stereo floating-point audio samples, for example from a CSCore stream.

## Credits
Thanks to the FFmpeg.Autogen for the very nice C# FFmpeg wrapper

## Contributing
You can help me make CSVideo the go-to solution for video manipulation in C# by reporting bugs
in the GitHub issue tracker, or submitting pull requests. I am grateful for every contribution.