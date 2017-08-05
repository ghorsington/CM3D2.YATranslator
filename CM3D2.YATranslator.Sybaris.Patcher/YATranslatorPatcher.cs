using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Inject;

namespace CM3D2.YATranslator.Sybaris.Patcher
{
    public static class YATranslatorPatcher
    {
        public static readonly string[] TargetAssemblyNames = {"Assembly-CSharp.dll"};
        private const string HOOK_NAME = "CM3D2.YATranslator.Hook";
        private const string SYBARIS_MANAGED_DIR = @"..\Plugins\Managed";

        public static void Patch(AssemblyDefinition assembly)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string hookDir = $"{SYBARIS_MANAGED_DIR}\\{HOOK_NAME}.dll";
            AssemblyDefinition hookAssembly = AssemblyLoader.LoadAssembly(Path.Combine(assemblyDir, hookDir));

            TypeDefinition hookType = hookAssembly.MainModule.GetType($"{HOOK_NAME}.TranslationHooks");

            TypeDefinition importCm = assembly.MainModule.GetType("ImportCM");
            TypeDefinition uiWidget = assembly.MainModule.GetType("UIWidget");
            TypeDefinition uiLabel = assembly.MainModule.GetType("UILabel");
            TypeDefinition scriptManager = assembly.MainModule.GetType("ScriptManager");
            TypeDefinition scheduleApi = assembly.MainModule.GetType("Schedule.ScheduleAPI");

            MethodDefinition infoReplace = scheduleApi.GetMethod("InfoReplace");
            MethodDefinition onTranslateInfoText = hookType.GetMethod("OnTranslateInfoText");
            infoReplace.InjectWith(onTranslateInfoText, flags: InjectFlags.PassParametersRef);

            MethodDefinition replaceCharaName = scriptManager.GetMethod("ReplaceCharaName", "System.String");
            MethodDefinition onTranslateTaggedText = hookType.GetMethod("OnTranslateTaggedText");
            replaceCharaName.InjectWith(onTranslateTaggedText, flags: InjectFlags.PassParametersRef);

            MethodDefinition loadTextureTarget = importCm.GetMethod("LoadTexture");
            MethodDefinition onArcTextureLoadHook = hookType.GetMethod("OnArcTextureLoad");
            loadTextureTarget.InjectWith(onArcTextureLoadHook,
                                         flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn);

            MethodDefinition onTranslateTextHook = hookType.GetMethod("OnTranslateText");
            MethodDefinition processAndRequestTarget = uiLabel.GetMethod("ProcessAndRequest");
            processAndRequestTarget.InjectWith(onTranslateTextHook,
                                               flags: InjectFlags.PassInvokingInstance | InjectFlags.PassFields,
                                               typeFields: new[] {uiLabel.GetField("mText")});
            processAndRequestTarget.IsPublic = true;
            processAndRequestTarget.IsPrivate = false;

            MethodDefinition onAssetTextureLoadHook = hookType.GetMethod("OnAssetTextureLoad");
            MethodDefinition getMainTextureTarget = uiWidget.GetMethod("get_mainTexture");
            getMainTextureTarget.InjectWith(onAssetTextureLoadHook,
                                            tag: 0,
                                            flags: InjectFlags.PassInvokingInstance | InjectFlags.PassTag);

            MethodDefinition awakeTarget = uiWidget.GetMethod("Awake");
            awakeTarget.InjectWith(onAssetTextureLoadHook,
                                   tag: 0,
                                   flags: InjectFlags.PassInvokingInstance | InjectFlags.PassTag);
        }
    }
}