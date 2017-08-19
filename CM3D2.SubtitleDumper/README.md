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

## Changing options

You can change the options by either **providing them as arguments** or changing **the name of the subtitledumper executable**.

For **arguments** just pass them as you usueally would:

```
CM3D2.SubtitleDumper.exe --merge=PerArc script*.arc
```

For **file name** change add parentheses (`( )`) to the file name and write the arguments inside them.
For instance, you can set `--merge` to `PerArc` by changing the name of the dumper from

```
CM3D2.SubtitleDumper.exe
```
to
```
CM3D2.SubtitleDumper(--merge=PerArc).exe
```

### Available options

| Option                | Description                                                         |
| --------------------- | ------------------------------------------------------------------- |
| `--merge=<type>`      | Specifies how subtitle files should be merged. Possible values of `<type>` are: `None` (subtitles are placed in separate files according to script name), `PerArc` (all subtitles found in the same ARC file are put in the same text file), `SingleFile` (all subtitles are put in a single file). Default value is `None`.
| `--max-threads=<num>` | Specifies that if there are multiple input ARC files, they should be dumped in parallel. Using multiple threads will speed up subtitle dumping on multicore CPUs. Default value is `4`.


## Duplicate handling

SubtitleDumper tries to avoid dumping duplicate subtitles, as it will usually slow down translation process.
Thus SubtitleDumper will do the following to avoid duplicate subtitles:

* If output folder is not empty when SubtitleDumper launches, SubtitleDumper will not dump subtitles that are already in the output folder. **Remove the output folder** before running SubtitleDumper if you want to dump everything again.
* If the same voice file is specified in multiple scripts, SubtitleDumper will dump the subtitle of the voice file only once. Thus if it seems that SubtitleDumper does not dump some subtitles, most likely the same subtitle was already dumped from another script.
