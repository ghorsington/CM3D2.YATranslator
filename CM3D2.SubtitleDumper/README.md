# Subtitle Dumper for CM3D2

## What is this?

This is a tool that scans ARC files, finds any text that can *possibly* be a valid transcription of a voice file, and dumps all the transcriptions as string files. The string files can be used by YAT (or other translation loaders that support subtitles) to translate events that do not provide VN-like text UI.

## How to build it?

Either build it using Visual Studio or use the `build.bat` script located one folder level above.

## How to use it?

Using the tool requires command prompt.

1. Build the tool.
2. Copy `CM3D2.SubtitleDumper.exe` AND `CM3D2.Toolkit.dll` to `<CM3D2 root>\GameData`.
3. Open command prompt in the `GameData` folder
    * You can use `Shift + Right Click` on folder's empty space and select `Open Command Promt here` (or PowerShell) from the appearing pop-up menu.
4. Make sure the tool works by using the command `CM3D2.SubtitleDumper.exe`. If the tool works, you'll see the following helper message:
```
CM3D2.SubtitleDumper
Searches CM3D2 Arc files for subtitle transcripts and dumps them.

Usage:
CM3D2.SubtitleDumper.exe <arc files>
All output is put into `output` folder
```
5. Run the tool itself by passing arc file names as arguments. Common wildcards are allowed.

For instance, dumping subtitles from all ARC files starting with a word "script" requires the following command:
```
CM3D2.SubtitleDumper.exe script*.arc
```

> NOTE: If you are using PowerShell, use the following *two* commands:
> ```
> $files = Get-ChildItem script*.arc | % { $_.Name }
> .\CM3D2.SubtitleDumper.exe $files
> ```
> Replace `script*.arc` with the pattern you'd like.

## Output

The tool outputs all possible subtitle strings into `output` folder. All strings are sorted by arc file name and by arc's inner file system's file names.

The tool **DOES NOT** automatically set level restrictions since it doesn't know which level each string belongs to. Thus if the dumped strings are used as-is in YAT, they will be loaded for **all** levels.

The previous note notwithstanding, the strings can be used directly with YAT without any edits, if need be.
