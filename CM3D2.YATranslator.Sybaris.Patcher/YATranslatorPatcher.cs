using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CM3D2.YATranslator.Hook;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Inject;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace CM3D2.YATranslator.Sybaris.Patcher
{
    public static class YATranslatorPatcher
    {
        public static readonly string[] TargetAssemblyNames = {"Assembly-CSharp.dll", "UnityEngine.UI.dll"};
        private const string HOOK_NAME = "CM3D2.YATranslator.Hook";

        private static readonly Dictionary<string, Action<AssemblyDefinition, AssemblyDefinition>> Patchers =
                new Dictionary<string, Action<AssemblyDefinition, AssemblyDefinition>>
                {
                        {"Assembly-CSharp", PatchAssemblyCSharp},
                        {"UnityEngine.UI", PatchUi}
                };

        private const string SYBARIS_MANAGED_DIR = @".";

        public static void Patch(AssemblyDefinition assembly)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string hookDir = $"{SYBARIS_MANAGED_DIR}\\{HOOK_NAME}.dll";
            var hookAssembly = AssemblyLoader.LoadAssembly(Path.Combine(assemblyDir, hookDir));

            if (Patchers.TryGetValue(assembly.Name.Name, out var patcher))
                patcher(hookAssembly, assembly);
        }

        private static void PatchUi(AssemblyDefinition hookAssembly, AssemblyDefinition assembly)
        {
            var hookType = hookAssembly.MainModule.GetType($"{HOOK_NAME}.{nameof(TranslationHooks)}");
            var text = assembly.MainModule.GetType("UnityEngine.UI.Text");
            var image = assembly.MainModule.GetType("UnityEngine.UI.Image");
            var maskableGraphic = assembly.MainModule.GetType("UnityEngine.UI.MaskableGraphic");

            var textSetter = text.GetMethod("set_text");
            var onTranslateUiText = hookType.GetMethod(nameof(TranslationHooks.OnTranslateUiText));
            textSetter.InjectWith(onTranslateUiText,
                                  tag: (int) StringType.Text,
                                  flags: InjectFlags.PassParametersRef | InjectFlags.PassInvokingInstance | InjectFlags.PassTag);

            var setSprite = image.GetMethod("set_sprite");
            var onTranslateSprite = hookType.GetMethod(nameof(TranslationHooks.OnTranslateSprite));
            setSprite.InjectWith(onTranslateSprite, flags: InjectFlags.PassParametersRef);

            var onEnable = maskableGraphic.GetMethod("OnEnable");
            var onTranslateGraphic = hookType.GetMethod(nameof(TranslationHooks.OnTranslateGraphic));
            onEnable.InjectWith(onTranslateGraphic, flags: InjectFlags.PassInvokingInstance);
        }

        private static void PatchAssemblyCSharp(AssemblyDefinition hookAssembly, AssemblyDefinition assembly)
        {
            var hookType = hookAssembly.MainModule.GetType($"{HOOK_NAME}.{nameof(TranslationHooks)}");

            var importCm = assembly.MainModule.GetType("ImportCM");
            var uiWidget = assembly.MainModule.GetType("UIWidget");
            var uiLabel = assembly.MainModule.GetType("UILabel");
            var scriptManager = assembly.MainModule.GetType("ScriptManager");
            var scheduleApi = assembly.MainModule.GetType("Schedule.ScheduleAPI");
            var freeSceneUi = assembly.MainModule.GetType("FreeScene_UI");
            var trophyUi = assembly.MainModule.GetType("Trophy_UI");
            var audioSrcMgr = assembly.MainModule.GetType("AudioSourceMgr");
            var textureResource = assembly.MainModule.GetType("TextureResource");

            var texResourceCtor = textureResource.GetMethod(".ctor");

            Console.WriteLine($"Ctor parameters: {texResourceCtor.Parameters.Count}");

            if (texResourceCtor.Parameters.Count != 4)
            {
                // Compatability patch for COM3D2 v1.13+

                var newCtor = new MethodDefinition(".ctor",
                                                   MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName
                                                   | MethodAttributes.RTSpecialName,
                                                   assembly.MainModule.Import(typeof(void)));

                newCtor.Parameters.Add(new ParameterDefinition(texResourceCtor.Parameters[0].ParameterType));
                newCtor.Parameters.Add(new ParameterDefinition(texResourceCtor.Parameters[1].ParameterType));
                newCtor.Parameters.Add(new ParameterDefinition(texResourceCtor.Parameters[2].ParameterType));
                newCtor.Parameters.Add(new ParameterDefinition(texResourceCtor.Parameters[4].ParameterType));

                var il = newCtor.Body.GetILProcessor();

                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldarg_1));
                il.Append(il.Create(OpCodes.Ldarg_2));
                il.Append(il.Create(OpCodes.Ldarg_3));
                il.Append(il.Create(OpCodes.Ldnull));
                il.Append(il.Create(OpCodes.Ldarg_S, (byte) 4));
                il.Append(il.Create(OpCodes.Call, assembly.MainModule.Import(texResourceCtor)));
                il.Append(il.Create(OpCodes.Ret));

                textureResource.Methods.Add(newCtor);
            }

            var infoReplace = scheduleApi.GetMethod("InfoReplace");
            var onTranslateInfoText = hookType.GetMethod(nameof(TranslationHooks.OnTranslateInfoText));
            infoReplace.InjectWith(onTranslateInfoText,
                                   tag: (int) StringType.Template,
                                   flags: InjectFlags.PassParametersRef | InjectFlags.PassTag);

            var replaceCharaName = scriptManager.GetMethod("ReplaceCharaName", "System.String");
            var onTranslateConstText = hookType.GetMethod(nameof(TranslationHooks.OnTranslateConstText));
            replaceCharaName.InjectWith(onTranslateConstText,
                                        tag: (int) StringType.Template,
                                        flags: InjectFlags.PassParametersRef | InjectFlags.PassTag);

            var loadTextureTarget = importCm.GetMethod("LoadTexture");
            var onArcTextureLoadHook = hookType.GetMethod(nameof(TranslationHooks.OnArcTextureLoad));
            var onArcTextureLoadedHook = hookType.GetMethod(nameof(TranslationHooks.OnArcTextureLoaded));
            try
            {
                loadTextureTarget.InjectWith(onArcTextureLoadHook, flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn);
                HookOnTextureLoaded(assembly, onArcTextureLoadedHook);
            }
            catch (Exception)
            {
                onArcTextureLoadHook = hookType.GetMethod(nameof(TranslationHooks.OnArcTextureLoadEx));
                loadTextureTarget.InjectWith(onArcTextureLoadHook, flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn);
                HookOnTextureLoaded(assembly, onArcTextureLoadedHook, 1);
            }

            var onTranslateTextHook = hookType.GetMethod(nameof(TranslationHooks.OnTranslateText));
            var processAndRequestTarget = uiLabel.GetMethod("ProcessAndRequest");
            processAndRequestTarget.InjectWith(onTranslateTextHook,
                                               tag: (int) StringType.UiLabel,
                                               flags: InjectFlags.PassInvokingInstance | InjectFlags.PassFields | InjectFlags.PassTag,
                                               typeFields: new[] {uiLabel.GetField("mText")});
            processAndRequestTarget.IsPublic = true;
            processAndRequestTarget.IsPrivate = false;

            var onAssetTextureLoadHook = hookType.GetMethod(nameof(TranslationHooks.OnAssetTextureLoad));
            var getMainTextureTarget = uiWidget.GetMethod("get_mainTexture");
            getMainTextureTarget.InjectWith(onAssetTextureLoadHook, tag: 0, flags: InjectFlags.PassInvokingInstance | InjectFlags.PassTag);

            var awakeTarget = uiWidget.GetMethod("Awake");
            awakeTarget.InjectWith(onAssetTextureLoadHook, tag: 0, flags: InjectFlags.PassInvokingInstance | InjectFlags.PassTag);

            var freeSceneStart = freeSceneUi.GetMethod("FreeScene_Start");
            freeSceneStart.InjectWith(onTranslateConstText,
                                      tag: (int) StringType.Const,
                                      flags: InjectFlags.PassParametersRef | InjectFlags.PassTag);

            var trophyStart = trophyUi.GetMethod("Trophy_Start");
            trophyStart.InjectWith(onTranslateConstText,
                                   tag: (int) StringType.Const,
                                   flags: InjectFlags.PassParametersRef | InjectFlags.PassTag);

            var loadPlay = audioSrcMgr.GetMethod("Play");
            var onLoadSound = hookType.GetMethod(nameof(TranslationHooks.OnPlaySound));
            loadPlay.InjectWith(onLoadSound, flags: InjectFlags.PassInvokingInstance);

            ApplyMonkeyPatches(assembly, hookType);
        }

        private static void ApplyMonkeyPatches(AssemblyDefinition assembly, TypeDefinition hookType)
        {
            /** 
             * VPVR Cultivation Patch
             * CM3D2 Versions: 1.49+
             * 
             * Fixes VPVR's farming UI buttons breaking after being translated
             */
            var cultivationInv = assembly.MainModule.GetType("VRCultivationSeedInventory");

            var getSeedButton = cultivationInv.GetMethod("GetSeedButton");
            var onGetSeedButton = hookType.GetMethod(nameof(TranslationHooks.OnGetSeedButton));
            getSeedButton.InjectWith(onGetSeedButton,
                                     flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn | InjectFlags.PassFields,
                                     typeFields: new[] {cultivationInv.GetField("m_UIButtonPlantSeeds")});

            var getSeedType = cultivationInv.GetMethod("GetSeedType");
            var onSystemTextTranslate = hookType.GetMethod(nameof(TranslationHooks.OnGetSeedType));
            getSeedType.InjectWith(onSystemTextTranslate, flags: InjectFlags.PassParametersRef);

            /**
             * Yotogi subtitle capture
             * CM3D2 Versions: 1.00+
             * 
             * Captures Yotogi subtitles in-game and saves them into memory.
             */
            var yotogiKagManager = assembly.MainModule.GetType("YotogiKagManager");

            var tagHitRet = yotogiKagManager.GetMethod("TagHitRet");
            var onYotogiKagHitRet = hookType.GetMethod(nameof(TranslationHooks.OnYotogiKagHitRet));

            tagHitRet.InjectWith(onYotogiKagHitRet, flags: InjectFlags.PassInvokingInstance);
        }

        private static void HookOnTextureLoaded(AssemblyDefinition assembly, MethodReference textureLoadedHook, int arg = 0)
        {
            var importCm = assembly.MainModule.GetType("ImportCM");
            var loadTextureTarget = importCm.GetMethod("LoadTexture");
            var retInstruction = loadTextureTarget.Body.Instructions.Last();
            var il = loadTextureTarget.Body.GetILProcessor();
            il.InsertBefore(retInstruction, il.Create(OpCodes.Ldarg, arg));
            il.InsertBefore(retInstruction, il.Create(OpCodes.Callvirt, assembly.MainModule.Import(textureLoadedHook)));
        }
    }
}