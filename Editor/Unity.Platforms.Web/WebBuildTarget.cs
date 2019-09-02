using System.IO;

namespace Unity.Platforms.Web
{
    public abstract class WebBuildTarget : BuildTarget
    {
        protected string GetPlatformName()
        {
            return "Web";
        }

        public override string GetUnityPlatformName()
        {
            return "WebGL";
        }

        public override string GetExecutableExtension()
        {
            return ".html";
        }

        public override bool Run(FileInfo buildTarget)
        {
            return HTTPServer.Instance.HostAndOpen(
                buildTarget.Directory.FullName,
                buildTarget.Name,
                19050);
        }
    }

    class AsmJSBuildTarget : WebBuildTarget
    {
        public override string GetDisplayName()
        {
            return GetPlatformName() + " (AsmJS)";
        }

        public override string GetBeeTargetName()
        {
            return "asmjs";
        }
    }

    class WasmBuildTarget : WebBuildTarget
    {
        public override string GetDisplayName()
        {
            return GetPlatformName() + " (Wasm)";
        }

        public override string GetBeeTargetName()
        {
            return "wasm";
        }
    }
}
