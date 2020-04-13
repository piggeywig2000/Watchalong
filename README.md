# Watchalong
Facilitates watchalongs with your friends

## Overview
This program allows you to watch video files and listen to audio files together with your friends over the internet. It's designed for use with locally stored media files, but it also supports downloading media files using youtube-dl.

The people taking part in the watchalong connect to a web interface. The program keeps everyone in sync so that everyone is watching the same part at the same time. If one person starts buffering, the program automatically pauses everyone else's media players and waits for them to finish buffering.

One person hosts a web server which users can connect to using their browsers. The web server manages the playback and keeps everyone in sync but does not actually provide the media files.
Another person (or the same person) hosts a media server. The media server connects to the web server and provides the media files, so it is important that the person hosting the media server has a good upload speed.

There can be multiple media servers connected to the same web server.

## Usage
Head over to the [releases](https://github.com/piggeywig2000/Watchalong/releases) page to download. Make sure to install the ASP.NET Core Runtime 3.1 before running the application. This application has only been tested with Windows and probably doesn't work with Linux. You're welcome to try Linux and tell me what's broken.

Windows Firewall hates applications that host HTTP servers (for good reason), so you'll need to add an exception for it in the Windows Firewall. Also, it might help to run it as administrator.
