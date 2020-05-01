using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Unity.Platforms.Web
{
    public abstract class WebBuildTarget : BuildTarget
    {
        public override bool CanBuild => true;
        public override string UnityPlatformName => "WebGL";
        public override string ExecutableExtension => ".html";
        public override bool UsesIL2CPP => true;

        public override bool Run(FileInfo buildTarget)
        {
            var guids = AssetDatabase.FindAssets("websockify");
            string websockifyPath = "";
            foreach (var g in guids)
            {
                var jsPath = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileName(jsPath) == "websockify.js")
                    websockifyPath = Path.GetFullPath(jsPath);
            }
            
            string root = Path.GetDirectoryName(EditorApplication.applicationPath);

#if UNITY_EDITOR_OSX
            string monoPath = Path.Combine(root, "Unity.app", "Contents", "MonoBleedingEdge", "bin", "mono");
            string nodePath = Path.Combine(root, "Unity.app", "Contents", "Tools", "nodejs", "bin", "node");
            string rootWeb = Path.Combine(root, "PlaybackEngines", "WebGLSupport");
#else
            root = Path.Combine(root, "Data");
            string monoPath = "\"" + Path.Combine(root, "MonoBleedingEdge", "bin", "mono.exe") + "\"";
            string nodePath = "\"" + Path.Combine(root, "Tools", "nodejs", "node.exe") + "\"";
            string rootWeb = Path.Combine(root, "PlaybackEngines", "WebGLSupport");
#endif

            if (!Directory.Exists(rootWeb))
                return ReportSuccessWithWarning(buildTarget.FullName, "WebGL module not installed! Unable to run web build.");

            string serverArgs = "\"" + Path.Combine(rootWeb, "BuildTools", "SimpleWebServer.exe") + "\" . 8084";
            string websockifyArgs = "\"" + websockifyPath + "\" 54998 localhost:34999";
            
            // Start the server
            // Note that the server included with Unity will run but will not function properly
            // if executed in a .net environment. It must be ran with mono to function properly.
            var serverStartInfo = new ProcessStartInfo();
            serverStartInfo.FileName = monoPath;
            serverStartInfo.Arguments = serverArgs;
            serverStartInfo.WorkingDirectory = buildTarget.Directory.FullName;
            serverStartInfo.CreateNoWindow = true;
            serverStartInfo.UseShellExecute = false;

            var serverProcess = new Process();
            serverProcess.StartInfo = serverStartInfo;
            var success = serverProcess.Start();
            if (!success)
                return ReportSuccessWithWarning(buildTarget.FullName, "Error starting local server. Unable to run web build.");
            
            // Start the websockify proxy server
            var wsStartInfo = new ProcessStartInfo();
            wsStartInfo.FileName = nodePath;
            wsStartInfo.Arguments = websockifyArgs;
            wsStartInfo.CreateNoWindow = true;
            wsStartInfo.UseShellExecute = false;

            var wsProcess = new Process();
            wsProcess.StartInfo = wsStartInfo;
            success = wsProcess.Start();
            if (!success)
                return ReportSuccessWithWarning(buildTarget.FullName, "Error starting websockify proxy server. Unable to run web build.");
            
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
            UnityEngine.Debug.LogWarning("WebGL module not installed! Unable to run web build");
            return true;
        }
    }

    class AsmJSBuildTarget : WebBuildTarget
    {
        public override string DisplayName => "Web (AsmJS)";
        public override string BeeTargetName => "asmjs";
    }

    class WasmBuildTarget : WebBuildTarget
    {
        public override string DisplayName => "Web (Wasm)";
        public override string BeeTargetName => "wasm";
    }

}
