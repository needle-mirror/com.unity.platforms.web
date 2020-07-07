using Bee.NativeProgramSupport.Building;
using Bee.Toolchain.Emscripten;
using System;
using System.Collections.Generic;
using DotsBuildTargets;
using Newtonsoft.Json.Linq;
using Unity.BuildSystem.NativeProgramSupport;

class WebBuildConfig : IPlatformBuildConfig
{
    public bool SingleFile = false;
    public bool ExportWebPFallback = false;
}

abstract class DotsWebTarget : DotsBuildSystemTarget
{
    protected abstract bool UseWasm { get; }

    protected override NativeProgramFormat GetExecutableFormatForConfig(DotsConfiguration config,
        bool enableManagedDebugger)
    {
        var format = new EmscriptenExecutableFormat(ToolChain, "html");

        switch (config)
        {
            case DotsConfiguration.Debug:
                return format.WithLinkerSetting<EmscriptenDynamicLinker>(d =>
                    TinyEmscripten.ConfigureEmscriptenLinkerFor(d,
                        "debug",
                        enableManagedDebugger));

            case DotsConfiguration.Develop:
                return format.WithLinkerSetting<EmscriptenDynamicLinker>(d =>
                    TinyEmscripten.ConfigureEmscriptenLinkerFor(d,
                        "develop",
                        enableManagedDebugger));

            case DotsConfiguration.Release:
                return format.WithLinkerSetting<EmscriptenDynamicLinker>(d =>
                    TinyEmscripten.ConfigureEmscriptenLinkerFor(d,
                        "release",
                        enableManagedDebugger));

            default:
                throw new NotImplementedException("Unknown config: " + config);
        }
    }

    public override DotsRuntimeCSharpProgramConfiguration CustomizeConfigForSettings(DotsRuntimeCSharpProgramConfiguration config, FriendlyJObject settings)
    {
        var executableFormat = GetExecutableFormatForConfig(DotsConfigs.DotsConfigForSettings(settings, out _), false)
            .WithLinkerSetting<EmscriptenDynamicLinker>(e => e
                .WithCustomFlags_workaround(new[] {settings.GetString("EmscriptenCmdLine")})
                .WithSingleFile(settings.GetBool("SingleFile"))
            );
        config.NativeProgramConfiguration = new DotsRuntimeNativeProgramConfiguration(
            config.NativeProgramConfiguration.CodeGen,
            config.NativeProgramConfiguration.ToolChain,
            config.Identifier,
            config,
            executableFormat: executableFormat);
        config.PlatformBuildConfig = new WebBuildConfig { SingleFile = settings.GetBool("SingleFile"), ExportWebPFallback = settings.GetBool("ExportWebPFallback") };
        return config;
    }
}

class DotsAsmJSTarget : DotsWebTarget
{
    protected override bool UseWasm => false;

    public override string Identifier => "asmjs";

    public override ToolChain ToolChain => TinyEmscripten.ToolChain_AsmJS;
}

class DotsWasmTarget : DotsWebTarget
{
    protected override bool UseWasm => true;

    public override string Identifier => "wasm";

    public override ToolChain ToolChain => TinyEmscripten.ToolChain_Wasm;
}
