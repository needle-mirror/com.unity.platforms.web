using Unity.Build.DotsRuntime;
using Unity.Properties;
using Unity.Serialization;
using Unity.Serialization.Json;

namespace Unity.Build.Web.DotsRuntime
{
    [FormerName("Unity.Platforms.Web.EmscriptenSettings, Unity.Platforms.Web")]
    [FormerName("Unity.Platforms.Web.Build.EmscriptenSettings, Unity.Platforms.Web.Build")]
    public class EmscriptenSettings : IDotsRuntimeBuildModifier
    {
        [CreateProperty] public string EmccCmdLine = "";
        [CreateProperty] public bool SingleFileOutput = false;

        public void Modify(JsonObject jsonObject)
        {
            jsonObject["EmscriptenCmdLine"] = EmccCmdLine;
            jsonObject["SingleFile"] = SingleFileOutput;
        }
    }
}
