using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Unity.Build.DotsRuntime;
using Unity.Properties;

namespace Unity.Platforms.Web.Build
{
    [FormerlySerializedAs("Unity.Platforms.Web.EmscriptenSettings, Unity.Platforms.Web")]
    public class EmscriptenSettings : IDotsRuntimeBuildModifier
    {
        [Property] public string EmccCmdLine = "";

        public void Modify(JObject settingsJObject)
        {
            settingsJObject["EmscriptenCmdLine"] = EmccCmdLine;
        }
    }
}
