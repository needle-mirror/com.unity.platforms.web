using System;
using System.Collections.Generic;
using Bee.NativeProgramSupport.Building;
using Bee.Stevedore;
using Bee.Toolchain.Emscripten;
using Bee.Tools;
using NiceIO;
using Unity.BuildSystem.NativeProgramSupport;
using Unity.BuildTools;

internal static class TinyEmscripten
{
    public static ToolChain ToolChain_AsmJS { get; } = MakeEmscripten(new AsmJsArchitecture());

    public static ToolChain ToolChain_Wasm { get; } = MakeEmscripten(new WasmArchitecture());

    public static NPath NodeExe;

    // If this boolean is set to true, the new upstream Wasm backend is used for Wasm codegen.
    // If false, the older Emscripten "fastcomp" backend will be used.
    // This option is provided for a transitional period to enable flipping between the two backends for
    // profiling and debugging purposes.
    public static bool UseWasmBackend => false;

    public static EmscriptenToolchain MakeEmscripten(EmscriptenArchitecture arch)
    {
        var emscripten = new StevedoreArtifact(HostPlatform.IsWindows ? "emscripten-win" : "emscripten-unix");
        var emscriptenVersion = new Version(1, 39, 8);
        var emscriptenRoot = emscripten.Path;

        EmscriptenSdk sdk = null;

        if (Environment.GetEnvironmentVariable("EMSDK") != null)
        {
            Console.WriteLine("Using pre-set environment EMSDK=" + Environment.GetEnvironmentVariable("EMSDK") +
                              ". This should only be used for local development. Unset EMSDK env. variable to use tagged Emscripten version from Stevedore.");
            NodeExe = Environment.GetEnvironmentVariable("EMSDK_NODE");
            return new EmscriptenToolchain(new EmscriptenSdk(
                Environment.GetEnvironmentVariable("EMSCRIPTEN"),
                llvmRoot: Environment.GetEnvironmentVariable("LLVM_ROOT"),
                pythonExe: Environment.GetEnvironmentVariable("EMSDK_PYTHON"),
                nodeExe: Environment.GetEnvironmentVariable("EMSDK_NODE"),
                architecture: arch,
                // Use a dummy/hardcoded version string to represent Emscripten "incoming" branch (it should be always considered
                // a "dirty" branch that does not correspond to any tagged release)
                version: new Version(9, 9, 9),
                isDownloadable: false
            ));
        }

        if (HostPlatform.IsWindows)
        {
            var llvm = new StevedoreArtifact(UseWasmBackend ? "emscripten-wasm-llvm-win" : "emscripten-fc-llvm-win");

            var python = new StevedoreArtifact("winpython2-x64");
            var node = new StevedoreArtifact("node-win-x64");
            NodeExe = node.Path.Combine("node.exe");

            sdk = new EmscriptenSdk(
                emscriptenRoot,
                llvmRoot: llvm.Path,
                pythonExe: python.Path.Combine("WinPython-64bit-2.7.13.1Zero/python-2.7.13.amd64/python.exe"),
                nodeExe: NodeExe,
                architecture: arch,
                version: emscriptenVersion,
                isDownloadable: true,
                backendRegistrables: new[] {emscripten, llvm, python, node});
        }

        if (HostPlatform.IsLinux)
        {
            var llvm = new StevedoreArtifact(UseWasmBackend ? "emscripten-wasm-llvm-linux" : "emscripten-fc-llvm-linux");
            var node = new StevedoreArtifact("node-linux-x64");
            NodeExe = node.Path.Combine("bin/node");

            sdk = new EmscriptenSdk(
                emscriptenRoot,
                llvmRoot: llvm.Path,
                pythonExe: "/usr/bin/python2",
                nodeExe: NodeExe,
                architecture: arch,
                version: emscriptenVersion,
                isDownloadable: true,
                backendRegistrables: new[] {emscripten, llvm, node});
        }

        if (HostPlatform.IsOSX)
        {
            var llvm = new StevedoreArtifact(UseWasmBackend ? "emscripten-wasm-llvm-mac" : "emscripten-fc-llvm-mac");
            var node = new StevedoreArtifact("node-mac-x64");
            NodeExe = node.Path.Combine("bin/node");

            sdk = new EmscriptenSdk(
                emscriptenRoot: emscriptenRoot,
                llvmRoot: llvm.Path,
                pythonExe: "/usr/bin/python",
                nodeExe: NodeExe,
                architecture: arch,
                version: emscriptenVersion,
                isDownloadable: true,
                backendRegistrables: new[] {emscripten, llvm, node});
        }

        // All Emsdk components are already pre-setup, so no need to verify the environment.
        // This avoids issues reported in https://github.com/emscripten-core/emscripten/issues/5042 
        // (macOS Java check dialog popping up and slight slowdown in compiler invocation times)
        if (Environment.GetEnvironmentVariable("EMCC_SKIP_SANITY_CHECK") == null)
            Environment.SetEnvironmentVariable("EMCC_SKIP_SANITY_CHECK", "1");

        if (sdk == null)
            return null;

        return new EmscriptenToolchain(sdk);
    }
    
    // Development time configuration: Set to true to generate a HTML5 build that runs in a Web Worker instead of the (default) main browser thread.
    public static bool RunInBackgroundWorker { get; } = false;

    public static EmscriptenDynamicLinker ConfigureEmscriptenLinkerFor(EmscriptenDynamicLinker e,
        string variation, bool enableManagedDebugger)
    {
        var linkflags = new Dictionary<string, string>
        {
            // Bee defaults to PRECISE_F32=2, which is not an interesting feature for Dots. In Dots asm.js builds, we don't
            // care about single-precision floats, but care more about code size.
            {"PRECISE_F32", "0"},
            // No exceptions machinery needed, saves code size
            {"DISABLE_EXCEPTION_CATCHING", "1"},
            //// No virtual filesystem needed, saves code size
            {"NO_FILESYSTEM", "1"},
            // Make generated builds only ever executable from web, saves code size.
            // TODO: if/when we are generating a build for node.js test harness purposes, remove this line.
            {"ENVIRONMENT", "web"},
            // In -Oz builds, Emscripten does compile time global initializer evaluation in hope that it can
            // optimize away some ctors that can be compile time executed. This does not really happen often,
            // and with MINIMAL_RUNTIME we have a better "super-constructor" approach that groups all ctors
            // together into one, and that saves more code size. Unfortunately grouping constructors is
            // not possible if EVAL_CTORS is used, so disable EVAL_CTORS to enable grouping.
            {"EVAL_CTORS", "0"},
            // By default the musl C runtime used by Emscripten is POSIX errno aware. We do not care about
            // errno, so opt out from errno management to save a tiny bit of performance and code size.
            {"SUPPORT_ERRNO", "0"},
            // Remove support for OES_texture_half_float and OES_texture_half_float_linear extensions if
            // they are broken. See https://bugs.webkit.org/show_bug.cgi?id=183321,
            // https://bugs.webkit.org/show_bug.cgi?id=169999,
            // https://stackoverflow.com/questions/54248633/cannot-create-half-float-oes-texture-from-uint16array-on-ipad
            {"GL_DISABLE_HALF_FLOAT_EXTENSION_IF_BROKEN", "1"}
        };

        if (enableManagedDebugger)
            linkflags["PROXY_POSIX_SOCKETS"] = "1";

        if (e.Toolchain.Architecture is AsmJsArchitecture)
        {
            linkflags["LEGACY_VM_SUPPORT"] = "1";
            // In old fastcomp backend, we can separate the unreadable .asm.js content to its own .asm.js file.
            // In new LLVM backend, it is currently always separated if -s WASM=2 is set, or embedded inline
            // if -s WASM=0 is set, so this option does not apply there.
            if (!UseWasmBackend)
                e = e.WithSeparateAsm(true);
        }

        if (variation == "debug" || variation == "develop")
        {
            linkflags["ASSERTIONS"] = "2";
            linkflags["DEMANGLE_SUPPORT"] = "1";
        }
        else
        {
            linkflags["ASSERTIONS"] = "0";
            linkflags["AGGRESSIVE_VARIABLE_ELIMINATION"] = "1";
            if (!UseWasmBackend) // This optimization pass only exists for the old fastcomp backend.
                linkflags["ELIMINATE_DUPLICATE_FUNCTIONS"] = "1";
        }

        e = e.WithEmscriptenSettings(linkflags);
        e = e.WithNoExitRuntime(true);

        switch (variation)
        {
            case "debug":
                e = e.WithDebugLevel("3");
                e = e.WithOptLevel("0");
                e = e.WithLinkTimeOptLevel(0);
                e = e.WithEmitSymbolMap(true);
                break;
            case "develop":
                e = e.WithDebugLevel("2");
                e = e.WithOptLevel("1");
                e = e.WithLinkTimeOptLevel(0);
                e = e.WithEmitSymbolMap(false); 
                break;
            case "release":
                e = e.WithDebugLevel("0");
                e = e.WithOptLevel("z");
                e = e.WithLinkTimeOptLevel(3);
                e = e.WithEmitSymbolMap(!UseWasmBackend); // TODO: wasm backend is not generating symbol maps properly
                break;
            default:
                throw new ArgumentException();
        }

        e = e.WithMinimalRuntime(EmscriptenMinimalRuntimeMode.EnableDangerouslyAggressive);

        e = e.WithCustomFlags_workaround(new[]
        {
            "--closure-args", ("--externs " + BuildProgram.BeeRoot.Combine("closure_externs.js").ToString()).QuoteForProcessStart()
        });

        // TODO: Remove this line once Bee fix is in to support SystemLibrary() objects on web builds. Then restore
        // the line Libraries.Add(c => c.ToolChain.Platform is WebGLPlatform, new SystemLibrary("GL")); at the top of this file
        e = e.WithCustomFlags_workaround(new[] {"-lGL"});

        e=e.WithMemoryInitFile(e.Toolchain.Architecture is AsmJsArchitecture || RunInBackgroundWorker);
        
        if (RunInBackgroundWorker)
        {
            // Specify Emscripten -s USE_PTHREADS=1 at compile time, so that C++ code that is compiled will have
            // the __EMSCRIPTEN_PTHREADS__ preprocessor #define available to it to detect if code will be compiled
            // single- or multithreaded.
            e=e.WithCustomFlags_workaround(new[] { "-s USE_PTHREADS=1 " });
        }

        // Enables async requests for web IO and Disabling IndexDB support as this is not fully implemented yet in emscripten
        // Using custom flags as there appears to be no standard way to set the option in bee and passing the flags to the linker settings
        // normally will cause bee to error
        e = e.WithCustomFlags_workaround(new[] { "-s FETCH=1 -s FETCH_SUPPORT_INDEXEDDB=0" });

        return e;
    }
}
