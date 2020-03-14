using Newtonsoft.Json.Linq;
using Unity.Build.DotsRuntime;
using Unity.Properties;
using Unity.Serialization;

namespace Unity.Platforms.Web.Build
{
    [FormerName("Unity.Platforms.Web.EmscriptenSettings, Unity.Platforms.Web")]
    public class EmscriptenSettings : IDotsRuntimeBuildModifier
    {
        [CreateProperty] public string EmccCmdLine = "";

        public void Modify(JObject settingsJObject)
        {
            settingsJObject["EmscriptenCmdLine"] = EmccCmdLine;
        }
    }
}
