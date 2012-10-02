using System;
using System.IO;
using Newtonsoft.Json;

namespace decaf
{
    internal class ConfigParser
    {
        public static Simulation Parse(String input)
        {
            // Read JSON file in as text
            var streamReader = new StreamReader(input);
            string text = streamReader.ReadToEnd();
            streamReader.Close();

            // Setup the simulation and return the object
            var result = JsonConvert.DeserializeObject<Simulation>(text);
            if (result.Initialized)
            {
                throw new Exception("Hack attempt!");
            }
            result.Initialized = true;
            result.Setup();
            return result;
        }
    }
}