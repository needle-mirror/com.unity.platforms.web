using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Unity.Build.DotsRuntime;
using UnityEditor;
using UnityEngine;
using BuildTarget = Unity.Build.DotsRuntime.BuildTarget;

namespace Unity.Build.Web.DotsRuntime
{
    public abstract class WebBuildTarget : BuildTarget
    {
        public override bool CanBuild => true;
        public override string UnityPlatformName => nameof(UnityEditor.BuildTarget.WebGL);
        public override string ExecutableExtension => ".html";
        public override bool UsesIL2CPP => true;

        private static bool quitCallbackAdded = false;
        private static Process serverProcess;
        private static Process wsProcess;

        internal void EnsureProcessDead(Process process)
        {
            if (process == null)
                return;

            try
            {
                process.Kill();
                process.WaitForExit();
            }
            catch (InvalidOperationException) // it's already dead
            {
            }
        }

        public override bool Run(FileInfo buildTarget)
        {
            if (!quitCallbackAdded)
            {
                EditorApplication.quitting += OnEditorQuit;
                quitCallbackAdded = true;
            }

            PosixSocketBridgeRunner.EnsureRunning();

            var guids = AssetDatabase.FindAssets("websockify");
            string websockifyPath = "";
            foreach (var g in guids)
            {
                var jsPath = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileName(jsPath) == "websockify.js")
                    websockifyPath = Path.GetFullPath(jsPath);
            }

            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string stevePath = Path.Combine(projectPath, "Library", "DotsRuntimeBuild", "artifacts", "Stevedore");

            string httpServerPath = Path.Combine(stevePath, "http-server", "bin", "http-server");

#if UNITY_EDITOR_OSX
            string nodePath = Path.Combine(stevePath, "node-mac-x64", "bin", "node");
#elif UNITY_EDITOR_LINUX
            string nodePath = Path.Combine(stevePath, "node-linux-x64", "bin", "node");
#else
            string nodePath = Path.Combine(stevePath, "node-win-x64", "node.exe");
#endif

            if (!File.Exists(nodePath) || !File.Exists(httpServerPath))
                return ReportSuccessWithWarning(buildTarget.FullName, $"Unable to run web build: can't find either {nodePath} or {httpServerPath}");

            string serverArgs = $"\"{httpServerPath}\" -c-1 -s -p 8084 .";
            string websockifyArgs = $"\"{websockifyPath}\" 54998 localhost:34999";

            // Start http-server
            var serverStartInfo = new ProcessStartInfo();
            serverStartInfo.FileName = nodePath;
            serverStartInfo.Arguments = serverArgs;
            serverStartInfo.WorkingDirectory = buildTarget.Directory.FullName;
            serverStartInfo.CreateNoWindow = true;
            serverStartInfo.UseShellExecute = false;

            EnsureProcessDead(serverProcess);
            serverProcess = new Process() { StartInfo = serverStartInfo };
            var success = serverProcess.Start();
            if (!success)
            {
                serverProcess = null;
                return ReportSuccessWithWarning(buildTarget.FullName, "Error starting local server. Unable to run web build.");
            }

            // Start the websockify proxy server
            var wsStartInfo = new ProcessStartInfo();
            wsStartInfo.FileName = nodePath;
            wsStartInfo.Arguments = websockifyArgs;
            wsStartInfo.CreateNoWindow = true;
            wsStartInfo.UseShellExecute = false;

            EnsureProcessDead(wsProcess);
            wsProcess = new Process() { StartInfo = wsStartInfo };
            success = wsProcess.Start();
            if (!success)
            {
                wsProcess = null;
                return ReportSuccessWithWarning(buildTarget.FullName, "Error starting websockify proxy server. Unable to run web build.");
            }

            Application.OpenURL("http://localhost:8084/" + buildTarget.Name);

            return true;
        }

        public override ShellProcessOutput RunTestMode(string exeName, string workingDirPath, int timeout)
        {
            return new ShellProcessOutput
            {
                Succeeded = false,
                ExitCode = 0,
                FullOutput = "Test mode is not supported for web yet"
            };
        }

        private bool ReportSuccessWithWarning(string buildPath, string message)
        {
            EditorUtility.RevealInFinder(buildPath);
            UnityEngine.Debug.LogWarning(message);
            return true;
        }

        private static void OnEditorQuit()
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                serverProcess.Kill();
                serverProcess.WaitForExit();
                serverProcess = null;
            }

            if (wsProcess != null && !wsProcess.HasExited)
            {
                wsProcess.Kill();
                wsProcess.WaitForExit();
                wsProcess = null;
            }

            PosixSocketBridgeRunner.StopRunning();
        }
    }

    class AsmJSBuildTarget : WebBuildTarget
    {
        public override string DisplayName => "Web (AsmJS)";
        public override string BeeTargetName => "asmjs";
        public override bool SupportsManagedDebugging => false;
    }

    class WasmBuildTarget : WebBuildTarget
    {
        public override string DisplayName => "Web (Wasm)";
        public override string BeeTargetName => "wasm";
    }

}
