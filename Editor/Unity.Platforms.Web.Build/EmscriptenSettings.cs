using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Unity.Platforms.Build.DotsRuntime;
using Unity.Properties;

namespace Unity.Platforms.Web.Build
{
    [FormerlySerializedAs("Unity.Platforms.Web.EmscriptenSettings, Unity.Platforms.Web")]
    public class EmscriptenSettings : IDotsRuntimeBuildModifier
    {
        [Property] public List<string> EmccArgs = new List<string>();

        public void Modify(JObject settingsJObject)
        {
            var dict = new JObject();
            foreach (var arg in EmccArgs)
            {
                var separated = arg.Split('=');
                dict[separated[0]] = separated[1];
            }
            settingsJObject["EmscriptenSettings"] = dict;
        }
    }
}
