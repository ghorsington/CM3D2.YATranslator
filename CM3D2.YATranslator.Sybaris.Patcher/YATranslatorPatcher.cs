using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CM3D2.YATranslator.Hook;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Inject;

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

        private const string SYBARIS_MANAGED_DIR = @"..\Plugins\Managed";

        public static void Patch(AssemblyDefinition assembly)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string hookDir = $"{SYBARIS_MANAGED_DIR}\\{HOOK_NAME}.dll";
            AssemblyDefinition hookAssembly = AssemblyLoader.LoadAssembly(Path.Combine(assemblyDir, hookDir));

            if (Patchers.TryGetValue(assembly.Name.Name, out var patcher))
                patcher(hookAssembly, assembly);
        }

        private static void PatchUi(AssemblyDefinition hookAssembly, AssemblyDefinition assembly)
        {
            TypeDefinition hookType = hookAssembly.MainModule.GetType($"{HOOK_NAME}.{nameof(TranslationHooks)}");
            TypeDefinition text = assembly.MainModule.GetType("UnityEngine.UI.Text");
            TypeDefinition image = assembly.MainModule.GetType("UnityEngine.UI.Image");
            TypeDefinition maskableGraphic = assembly.MainModule.GetType("UnityEngine.UI.MaskableGraphic");

            MethodDefinition textSetter = text.GetMethod("set_text");
            MethodDefinition onTranslateUiText = hookType.GetMethod(nameof(TranslationHooks.OnTranslateUiText));
            textSetter.InjectWith(onTranslateUiText,
                                  tag: (int) StringType.Text,
                                  flags: InjectFlags.PassParametersRef
                                         | InjectFlags.PassInvokingInstance
                                         | InjectFlags.PassTag);

            MethodDefinition setSprite = image.GetMethod("set_sprite");
            MethodDefinition onTranslateSprite = hookType.GetMethod(nameof(TranslationHooks.OnTranslateSprite));
            setSprite.InjectWith(onTranslateSprite, flags: InjectFlags.PassParametersRef);

            MethodDefinition onEnable = maskableGraphic.GetMethod("OnEnable");
            MethodDefinition onTranslateGraphic = hookType.GetMethod(nameof(TranslationHooks.OnTranslateGraphic));
            onEnable.InjectWith(onTranslateGraphic, flags: InjectFlags.PassInvokingInstance);
        }

        private static void PatchAssemblyCSharp(AssemblyDefinition hookAssembly, AssemblyDefinition assembly)
        {
            TypeDefinition hookType = hookAssembly.MainModule.GetType($"{HOOK_NAME}.{nameof(TranslationHooks)}");

            TypeDefinition importCm = assembly.MainModule.GetType("ImportCM");
            TypeDefinition uiWidget = assembly.MainModule.GetType("UIWidget");
            TypeDefinition uiLabel = assembly.MainModule.GetType("UILabel");
            TypeDefinition scriptManager = assembly.MainModule.GetType("ScriptManager");
            TypeDefinition scheduleApi = assembly.MainModule.GetType("Schedule.ScheduleAPI");
            TypeDefinition freeSceneUi = assembly.MainModule.GetType("FreeScene_UI");
            TypeDefinition trophyUi = assembly.MainModule.GetType("Trophy_UI");
            TypeDefinition audioSrcMgr = assembly.MainModule.GetType("AudioSourceMgr");

            MethodDefinition infoReplace = scheduleApi.GetMethod("InfoReplace");
            MethodDefinition onTranslateInfoText = hookType.GetMethod(nameof(TranslationHooks.OnTranslateInfoText));
            infoReplace.InjectWith(onTranslateInfoText,
                                   tag: (int) StringType.Template,
                                   flags: InjectFlags.PassParametersRef | InjectFlags.PassTag);

            MethodDefinition replaceCharaName = scriptManager.GetMethod("ReplaceCharaName", "System.String");
            MethodDefinition onTranslateConstText = hookType.GetMethod(nameof(TranslationHooks.OnTranslateConstText));
            replaceCharaName.InjectWith(onTranslateConstText,
                                        tag: (int) StringType.Template,
                                        flags: InjectFlags.PassParametersRef | InjectFlags.PassTag);

            MethodDefinition loadTextureTarget = importCm.GetMethod("LoadTexture");
            MethodDefinition onArcTextureLoadHook = hookType.GetMethod(nameof(TranslationHooks.OnArcTextureLoad));
            MethodDefinition onArcTextureLoadedHook = hookType.GetMethod(nameof(TranslationHooks.OnArcTextureLoaded));
            try
            {
                loadTextureTarget.InjectWith(onArcTextureLoadHook,
                                             flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn);
                HookOnTextureLoaded(assembly, onArcTextureLoadedHook);
            }
            catch (Exception)
            {
                onArcTextureLoadHook = hookType.GetMethod(nameof(TranslationHooks.OnArcTextureLoadEx));
                loadTextureTarget.InjectWith(onArcTextureLoadHook,
                                             flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn);
                HookOnTextureLoaded(assembly, onArcTextureLoadedHook, 1);
            }

            MethodDefinition onTranslateTextHook = hookType.GetMethod(nameof(TranslationHooks.OnTranslateText));
            MethodDefinition processAndRequestTarget = uiLabel.GetMethod("ProcessAndRequest");
            processAndRequestTarget.InjectWith(onTranslateTextHook,
                                               tag: (int) StringType.UiLabel,
                                               flags: InjectFlags.PassInvokingInstance
                                                      | InjectFlags.PassFields
                                                      | InjectFlags.PassTag,
                                               typeFields: new[] {uiLabel.GetField("mText")});
            processAndRequestTarget.IsPublic = true;
            processAndRequestTarget.IsPrivate = false;

            MethodDefinition onAssetTextureLoadHook = hookType.GetMethod(nameof(TranslationHooks.OnAssetTextureLoad));
            MethodDefinition getMainTextureTarget = uiWidget.GetMethod("get_mainTexture");
            getMainTextureTarget.InjectWith(onAssetTextureLoadHook,
                                            tag: 0,
                                            flags: InjectFlags.PassInvokingInstance | InjectFlags.PassTag);

            MethodDefinition awakeTarget = uiWidget.GetMethod("Awake");
            awakeTarget.InjectWith(onAssetTextureLoadHook,
                                   tag: 0,
                                   flags: InjectFlags.PassInvokingInstance | InjectFlags.PassTag);

            MethodDefinition freeSceneStart = freeSceneUi.GetMethod("FreeScene_Start");
            freeSceneStart.InjectWith(onTranslateConstText,
                                      tag: (int) StringType.Const,
                                      flags: InjectFlags.PassParametersRef | InjectFlags.PassTag);

            MethodDefinition trophyStart = trophyUi.GetMethod("Trophy_Start");
            trophyStart.InjectWith(onTranslateConstText,
                                   tag: (int) StringType.Const,
                                   flags: InjectFlags.PassParametersRef | InjectFlags.PassTag);

            MethodDefinition loadPlay = audioSrcMgr.GetMethod("Play");
            MethodDefinition onLoadSound = hookType.GetMethod(nameof(TranslationHooks.OnPlaySound));
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
            TypeDefinition cultivationInv = assembly.MainModule.GetType("VRCultivationSeedInventory");

            MethodDefinition getSeedButton = cultivationInv.GetMethod("GetSeedButton");
            MethodDefinition onGetSeedButton = hookType.GetMethod(nameof(TranslationHooks.OnGetSeedButton));
            getSeedButton.InjectWith(onGetSeedButton,
                                     flags: InjectFlags.PassParametersVal
                                            | InjectFlags.ModifyReturn
                                            | InjectFlags.PassFields,
                                     typeFields: new[] { cultivationInv.GetField("m_UIButtonPlantSeeds") });

            MethodDefinition getSeedType = cultivationInv.GetMethod("GetSeedType");
            MethodDefinition onSystemTextTranslate = hookType.GetMethod(nameof(TranslationHooks.OnGetSeedType));
            getSeedType.InjectWith(onSystemTextTranslate, flags: InjectFlags.PassParametersRef);


            /**
             * Yotogi subtitle capture
             * CM3D2 Versions: 1.00+
             * 
             * Captures Yotogi subtitles in-game and saves them into memory.
             */
            TypeDefinition yotogiKagManager = assembly.MainModule.GetType("YotogiKagManager");

            MethodDefinition tagHitRet = yotogiKagManager.GetMethod("TagHitRet");
            MethodDefinition onYotogiKagHitRet = hookType.GetMethod(nameof(TranslationHooks.OnYotogiKagHitRet));

            tagHitRet.InjectWith(onYotogiKagHitRet, flags: InjectFlags.PassInvokingInstance);
        }

        private static void HookOnTextureLoaded(AssemblyDefinition assembly, MethodReference textureLoadedHook, int arg = 0)
        {
            TypeDefinition importCm = assembly.MainModule.GetType("ImportCM");
            MethodDefinition loadTextureTarget = importCm.GetMethod("LoadTexture");
            Instruction retInstruction = loadTextureTarget.Body.Instructions.Last();
            ILProcessor il = loadTextureTarget.Body.GetILProcessor();
            il.InsertBefore(retInstruction, il.Create(OpCodes.Ldarg, arg));
            il.InsertBefore(retInstruction, il.Create(OpCodes.Callvirt, assembly.MainModule.Import(textureLoadedHook)));
        }
    }
}