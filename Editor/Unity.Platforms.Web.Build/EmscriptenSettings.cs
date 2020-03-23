using Unity.Build.DotsRuntime;
using Unity.Properties;
using Unity.Serialization;
using Unity.Serialization.Json;

namespace Unity.Platforms.Web.Build
{
    [FormerName("Unity.Platforms.Web.EmscriptenSettings, Unity.Platforms.Web")]
    public class EmscriptenSettings : IDotsRuntimeBuildModifier
    {
        [CreateProperty] public string EmccCmdLine = "";

        public void Modify(JsonObject jsonObject)
        {
            jsonObject["EmscriptenCmdLine"] = EmccCmdLine;
        }
    }
}
