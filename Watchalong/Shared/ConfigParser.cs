using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using Watchalong.Utils;

namespace Watchalong.Config
{
    /// <summary>
    /// Provides methods for saving to and loading from YAML files
    /// </summary>
    public static class ConfigParser
    {
        /// <summary>
        /// Load a configuration from a YAML file
        /// </summary>
        /// <typeparam name="T">The class to deserialize the YAML file into</typeparam>
        /// <param name="fileLocation">The location of the config file</param>
        /// <returns>An object of type T containing the data loaded from the config file</returns>
        public static T LoadConfig<T>(string fileLocation) where T : new()
        {
            ConLog.Log("Configuration", "Loading configuration from " + Path.GetFullPath(fileLocation), LogType.Info);

            Deserializer convert = new Deserializer();
            T returnObj = new T();

            //Check that the file exists
            if (!File.Exists(fileLocation))
            {
                SaveConfig(fileLocation, returnObj);
                ConLog.Log("Configuration", "The configuration file was not found, so one was generated. Please review the configuration before re-launching the program", LogType.Fatal);
            }

            //Try to deserialise it
            try
            {
                using StreamReader reader = new StreamReader(fileLocation);
                returnObj = convert.Deserialize<T>(reader);
            }
            catch (YamlDotNet.Core.YamlException e)
            {
                ConLog.Log("Configuration", "An error occurred when reading from the configuration file. To regenerate the config file, delete it and re-launch the program\nException:\n" + e.Message, LogType.Fatal);
            }

            ConLog.Log("Configuration", "Configuration loaded", LogType.Ok);
            return returnObj;
        }

        /// <summary>
        /// Save a configuration to a YAML file
        /// </summary>
        /// <param name="fileLocation">The location of the config file</param>
        /// <param name="configToSave">The data to save into the config file</param>
        public static void SaveConfig(string fileLocation, object configToSave)
        {
            ConLog.Log("Configuration", "Saving configuration to " + Path.GetFullPath(fileLocation), LogType.Info);

            Serializer convert = new Serializer();

            using StreamWriter writer = new StreamWriter(fileLocation);
            convert.Serialize(writer, configToSave);

            ConLog.Log("Configuration", "Configuration saved", LogType.Ok);
        }
    }
}
