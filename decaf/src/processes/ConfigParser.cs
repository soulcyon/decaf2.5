using System;
using System.IO;
using Newtonsoft.Json;

namespace decaf
{
    public class ConfigParser
    {
        public static void Parse(String input)
        {
            // Read JSON file in as text
            var streamReader = new StreamReader(input);
            string text = streamReader.ReadToEnd();
            streamReader.Close();

            // Setup the simulation
            var jss = new JsonSerializerSettings
                        {
                            MaxDepth = 4, ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
                        };
            var t = JsonConvert.DeserializeObject<Json>(text, jss);
            Simulation.Setup(t);
        }
    }
}