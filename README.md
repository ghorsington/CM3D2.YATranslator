# Yet Another Translator for CM3D2

As the name implies, this is a translation plug-in for CM3D2.
Use it to translate in-game strings, textures and assets.

### [Download the latest release](https://github.com/denikson/CM3D2.YATranslator/releases)
### [View the wiki for help](https://github.com/denikson/CM3D2.YATranslator/wiki)

## Why another translation plug-in?

Yes, I do know that there are already two identical plug-ins, albeit one being for
ReiPatcher (the Unified Translation Plug-in) and the other for Sybaris (TranslationPlus).

However, neither are

* updated for CM3D2 version 1.49 and newer,
* providing support both for Sybaris and ReiPatcher,
* are open sourced (while TranslationPlus is available on GitHub, there is no licence, and the plug-in is pretty much a recompiled UTP).

Thus this plug-in was written from scratch while maintaining similar functionality and public API.
Moreover, this plug-in was made solely with CM3D2 in mind, which allowed to simplify some code
and remove some unneeded quirks.

## Main features

* Patchers for Sybaris and ReiPatcher (for those "legacy" users)
* String and asset replacing (pretty much the same)
* **Fixed** texture replacing
* **CM3D2 VPVR support**
  * All strings translateable, all main UI elements replaceable
* **Tagged translations** (the one containing `[HF]`, etc.) no longer require RegExes
    * This fixed some tagged translations not working (like the ones ending with `...?`)
    * Text scrolling effect now works with translations
* **Logging and dumping** is improved
  * You can log different asset translation separately
  * Can dump untranslated assets and textures
* **Subtitle** support
* **Text-to-clipboard** supported internally


## Building

To build you need to:

1. Download the source code
2. Have MSBuild 15.0 installed
3. Place required assemblies into `Libs` folder. More info in the folder's README.
4. Run `build.bat`


## Contributing, problem reporting

If you have suggestions or any problems related to the plug-in, feel free to create an issue.
If you do so, **please**, tag your issues accordingly.

Feel free to fork, edit and create pull requests.
