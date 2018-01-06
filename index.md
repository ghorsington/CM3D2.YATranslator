---
layout: project
title: "CM3D2.YATranslator"
project_title: "Yet Another Translator"
project_subtitle: "A fast translation loader for CM3D2 with subtitle support"
asset_types:
  - postfix: "ReiPatcher"
    index: 0
  - postfix: "Sybaris"
    index: 1
caption_file: "yatcaption.md"
sections:
  - id: "help"
    name: "Help"
    contents_file: "help_section.md"
buttons:
  - name: "Wiki"
    href: "https://github.com/denikson/CM3D2.YATranslator/wiki"
---

**Y**et **A**nother **T**ranslator is a translation loader for CM3D2.
The plug-in provides a faster, open-sourced alternative to other translations loaders, like the Unified Translation Plug-in and TranslationPlus.

## Why Another one?

**YAT** was initially created as a fork of TranslationPlus to fix compatibility issues that arose in other plug-ins. Later, **YAT** was fully rewritten to add full support for new in-game content.

### Differences between YAT and other major translation plug-ins

<style>
  td, tr th:not(:first-child) {
    text-align: center;
  }
</style>

<table class="table table-striped table-bordered  ">
  <thead>
    <tr>
      <th scope="col">Feature / Translation Loader</th>
      <th scope="col">Unified Translation Loader</th>
      <th scope="col">TranslationPlus</th>
      <th scope="col">YATranslator</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <th scope="row">String Translation (classic UI)</th>
      <td><i class="fas fa-circle"></i></td>
      <td><i class="fas fa-circle"></i></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">Asset Replacement (classic UI)</th>
      <td><i class="fas fa-circle"></i></td>
      <td><i class="fas fa-circle"></i></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">Texture Replacement</th>
      <td>Broken since CM3D2 1.49</td>
      <td>Broken since CM3D2 1.49</td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">Untranslated resource extraction</th>
      <td><i class="fas fa-circle"></i></td>
      <td><i class="fas fa-circle"></i></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">New UI (VPVR/KPVR) Translation</th>
      <td></td>
      <td></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">Subtitles for all voices</th>
      <td></td>
      <td></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">VR support</th>
      <td></td>
      <td></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">Built in text-to-clipboard</th>
      <td></td>
      <td></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">ReiPatcher support</th>
      <td><i class="fas fa-circle"></i></td>
      <td></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
    <tr>
      <th scope="row">Sybaris support</th>
      <td></td>
      <td><i class="fas fa-circle"></i></td>
      <td><i class="fas fa-circle"></i></td>
    </tr>
  </tbody>
</table>


## Translations and support

Since **YAT** supports exactly the same translation files as other major translation loaders, no action is required to "upgrade" translations.

**YAT** is already supported by many translators, and translations for subtitles and new in-game UI already exist.
Moreover, the [AutoTranslate plug-in](https://github.com/texel-sensei/CM3D2.AutoTranslate) versions 1.3 and newer work with **YAT**.