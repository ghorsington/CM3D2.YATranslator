using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Inject;
using ReiPatcher;
using ReiPatcher.Patch;

namespace CM3D2.YATranslator.Patch
{
    public class YATranslatorPatcher : PatchBase
    {
        public const string TAG = "YAT_PATCHED";
        private const string HOOK_NAME = "CM3D2.YATranslator.Hook";

        public override string Name => "Yet Another Translator Patcher";

        private AssemblyDefinition HookAssembly { get; set; }

        public override bool CanPatch(PatcherArguments args) => args.Assembly.Name.Name == "Assembly-CSharp"
                                                                && !HasAttribute(args.Assembly, TAG);

        public override void Patch(PatcherArguments args)
        {
            TypeDefinition hookType = HookAssembly.MainModule.GetType($"{HOOK_NAME}.TranslationHooks");

            TypeDefinition importCm = args.Assembly.MainModule.GetType("ImportCM");
            TypeDefinition uiWidget = args.Assembly.MainModule.GetType("UIWidget");
            TypeDefinition uiLabel = args.Assembly.MainModule.GetType("UILabel");
            TypeDefinition scriptManager = args.Assembly.MainModule.GetType("ScriptManager");
            TypeDefinition scheduleApi = args.Assembly.MainModule.GetType("Schedule.ScheduleAPI");

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

            SetPatchedAttribute(args.Assembly, TAG);
        }

        public override void PrePatch()
        {
            RPConfig.RequestAssembly("Assembly-CSharp.dll");
            HookAssembly = AssemblyLoader.LoadAssembly(Path.Combine(AssembliesDir, $"{HOOK_NAME}.dll"));
        }

        private bool HasAttribute(AssemblyDefinition assembly, string tag)
        {
            return GetPatchedAttributes(assembly).Any(ass => ass.Info == tag);
        }
    }
}