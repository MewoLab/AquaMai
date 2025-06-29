# AquaMai

## Installation

1. Build the project or find an existing build somewhere™
2. Download [MelonLoader.x64.zip](https://github.com/LavaGang/MelonLoader/releases/download/v0.7.0/MelonLoader.x64.zip)
3. Extract MelonLoader zip to where your Sinmai.exe is
4. Make a Mods folder and put AquaMai.dll inside it
5. Pet your cat
6. Launch!

## Features

**Cheats**

* Unlock all tickets

**UX Optimization**

* Remove the starting logo and warning cutscene
* Single Player (1P) mode
* Skip from card scanning directly to music selection (experimental)
* Disable daily automatic reboot
* Customize version text
* Skip the current song by holding 7
* Skip "new event" and "information" screen for new players.

**Bug Fixes**

* Fix crash in the character selection screen

**Performance**

* Speed up things

## Development

1. Copy `Assembly-CSharp.dll` and `AMDaemon.NET.dll` to `Libs` folder.
1. Install [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-developer-pack-offline-installer)
1. Run `build.ps1`.
1. Copy `Output/AquaMai.dll` to `Mods` folder.
1. Configure and copy `AquaMai.toml` to the same folder as your game executable: `Sinmai.exe`

## Relevant Links

* [MelonLoader Wiki](https://melonwiki.xyz/#/modders/quickstart)
* [Harmony Docs](https://harmony.pardeike.net/articles/patching-prefix.html)
