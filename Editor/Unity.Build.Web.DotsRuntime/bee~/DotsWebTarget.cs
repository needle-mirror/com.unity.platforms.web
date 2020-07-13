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
}

abstract class DotsWebTarget : DotsBuildSystemTarget
{
    public override bool CanUseBurst => true;

    protected abstract bool UseWasm { get; }

    protected abstract bool SupportsManagedDebugging { get; }

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
        var executableFormat = GetExecutableFormatForConfig(DotsConfigs.DotsConfigForSettings(settings, out _),
                SupportsManagedDebugging && DotsConfigs.ShouldEnableDevelopmentOptionForSetting("EnableManagedDebugging", new[]
                {
                    DotsConfiguration.Debug
                }, settings))
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
        config.PlatformBuildConfig = new WebBuildConfig { SingleFile = settings.GetBool("SingleFile") };
        return config;
    }
}

class DotsAsmJSTarget : DotsWebTarget
{
    protected override bool UseWasm => false;

    public override string Identifier => "asmjs";

    public override ToolChain ToolChain => TinyEmscripten.ToolChain_AsmJS;

    // Wasm2JS does not support pthreads, so the asmjs build cannot support managed debugging.
    protected override bool SupportsManagedDebugging => false;
}

class DotsWasmTarget : DotsWebTarget
{
    protected override bool UseWasm => true;

    public override string Identifier => "wasm";

    public override ToolChain ToolChain => TinyEmscripten.ToolChain_Wasm;

    protected override bool SupportsManagedDebugging => true;
}
