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
    public static bool UseWasmBackend => true;

    // If set to true, Closure compiler minification is enabled for the code in release builds. If false,
    // weaker UglifyJS based minification is used instead. TODO: enable this by default.
    public static bool EnableClosureCompiler => false;

    // Returns the naming scheme for OS component for Emscripten packages hosted on artifactory
    public static string EmscriptenPackageOSName()
    {
        if (HostPlatform.IsWindows) return "win";
        if (HostPlatform.IsLinux) return "linux";
        if (HostPlatform.IsOSX) return "mac";
        throw new Exception("Emscripten build support only works from Windows, Linux or macOS hosts!");
    }

    public static EmscriptenToolchain MakeEmscripten(EmscriptenArchitecture arch)
    {
        var emscripten = new StevedoreArtifact("emscripten-" + EmscriptenPackageOSName());
        var llvm = new StevedoreArtifact("emscripten-" + (UseWasmBackend ? "wasm" : "fc") + "-llvm-" + EmscriptenPackageOSName());
        var emscriptenVersion = new Version(1, 39, 15);
        var emscriptenRoot = emscripten.Path;

        EmscriptenSdk sdk = null;

        if (Environment.GetEnvironmentVariable("EMSDK") != null)
        {
            Console.WriteLine("Using pre-set environment EMSDK=" + Environment.GetEnvironmentVariable("EMSDK") +
                              ". This should only be used for local development. Unset EMSDK env. variable to use tagged Emscripten version from Stevedore.");
            NodeExe = Environment.GetEnvironmentVariable("EMSDK_NODE");
            sdk = new EmscriptenSdk(
                Environment.GetEnvironmentVariable("EMSCRIPTEN"),
                llvmRoot: Environment.GetEnvironmentVariable("LLVM_ROOT"),
                pythonExe: Environment.GetEnvironmentVariable("EMSDK_PYTHON"),
                nodeExe: NodeExe,
                architecture: arch,
                // Use a dummy/hardcoded version string to represent Emscripten "incoming" branch (it should be always considered
                // a "dirty" branch that does not correspond to any tagged release)
                version: new Version(9, 9, 9),
                isDownloadable: false
            );
        }
        else if (HostPlatform.IsWindows)
        {
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
        else if (HostPlatform.IsLinux)
        {
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
        else if (HostPlatform.IsOSX)
        {
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

        if (sdk == null)
            return null;

        // All Emsdk components are already pre-setup, so no need to verify the environment.
        // This avoids issues reported in https://github.com/emscripten-core/emscripten/issues/5042 
        // (macOS Java check dialog popping up and slight slowdown in compiler invocation times)
        // BUG: this does not actually work. Emcc is not a child of the Bee build process.
        // Switch to use something else.
//        if (Environment.GetEnvironmentVariable("EMCC_SKIP_SANITY_CHECK") == null)
//            Environment.SetEnvironmentVariable("EMCC_SKIP_SANITY_CHECK", "1");

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
            {"GL_DISABLE_HALF_FLOAT_EXTENSION_IF_BROKEN", "1"},
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
            linkflags["ASSERTIONS"] = "1";
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
                e = e.WithDebugLevel("3"); // Preserve JS whitespace, function names, and LLVM debug info
                e = e.WithOptLevel("1"); // -O0 generates too much code from IL2CPP, must apply optimizations.
                e = e.WithLinkTimeOptLevel(0);
                e = e.WithEmitSymbolMap(false); // At -g3 no name minification occurs -> no symbols present
                e = e.WithCustomFlags_workaround(new[] {
                    "-fno-inline" // Disable inlining in debug builds for easier stepping through code.
                });
                break;
            case "develop":
                e = e.WithDebugLevel("2"); // Preserve JS whitespace and function names
                e = e.WithOptLevel("1");
                e = e.WithLinkTimeOptLevel(0);
                e = e.WithEmitSymbolMap(false); // At -g2 no name minification occurs -> no symbols present
                break;
            case "release":
                e = e.WithDebugLevel("0");
                e = e.WithOptLevel("z");
                e = e.WithLinkTimeOptLevel(3);
                e = e.WithEmitSymbolMap(false); // TODO: re-enable this after Emscripten update
                break;
            default:
                throw new ArgumentException();
        }

        e = e.WithMinimalRuntime(EmscriptenMinimalRuntimeMode.EnableDangerouslyAggressive);

        if (EnableClosureCompiler)
        {
            e = e.WithCustomFlags_workaround(new[]
            {
                "--closure-args", ("--platform native,javascript --externs " + BuildProgram.BeeRoot.Combine("closure_externs.js").ToString()).QuoteForProcessStart(),
                "--closure", "1"
            });
        }

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

        if (UseWasmBackend && e.Toolchain.Architecture is AsmJsArchitecture)
        {
            // Work around Binaryen multithreading bug: using more than 1 core is slower than using a single core!
            // TODO: Remove this after Emscripten update, where the issue has been fixed.
            // BUG: this does not actually work. Emcc is not a child of the Bee build process.
            // Switch to use something else.
//            Environment.SetEnvironmentVariable("BINARYEN_CORES", "1");
        }

        return e;
    }
}
