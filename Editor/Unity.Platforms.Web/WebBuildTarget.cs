using System.IO;

namespace Unity.Platforms.Web
{
    public abstract class WebBuildTarget : BuildTarget
    {
        public override bool CanBuild => true;
        public override string UnityPlatformName => "WebGL";
        public override string ExecutableExtension => ".html";

        public override bool Run(FileInfo buildTarget)
        {
            UnityEditor.EditorUtility.RevealInFinder(buildTarget.FullName);
            return true;

            // Currently we don't have a server to run
            //return HTTPServer.Instance.HostAndOpen(
            //    buildTarget.Directory.FullName,
            //    buildTarget.Name,
            //    19050);
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
