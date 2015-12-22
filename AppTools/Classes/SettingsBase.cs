using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;

namespace AppTools
{

    public class SettingsBase
    {
        public static SettingsBase Load()
        {
            return (SettingsBase.Load(buildSettingsFilename()));
        }

        public static SettingsBase Load(string filename)
        {
            SettingsBase returnValue = new SettingsBase();

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SettingsBase));
                TextReader tr = new StreamReader(filename);
                returnValue = (SettingsBase)serializer.Deserialize(tr);
                tr.Close();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Error loading settings from {0}.", Path.GetFileName(filename)), ex);
            }

            return (returnValue);
        }

        public bool Save()
        {
            return (this.Save(buildSettingsFilename()));
        }

        public bool Save(string filename)
        {
            bool returnValue = false;

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SettingsBase));
                TextWriter tw = new StreamWriter(filename);
                serializer.Serialize(tw, this);
                tw.Close();
                returnValue = true;
            }
            catch (Exception ex)
            {
                throw new Exception("Error saving settings.", ex);
            }

            return (returnValue);
        }

        private static string buildSettingsFilename()
        {
            var settingsPathname = Path.Combine(@".\", System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            settingsPathname = Path.Combine(Path.GetDirectoryName(settingsPathname), Path.GetFileNameWithoutExtension(settingsPathname));

            if (settingsPathname.EndsWith(".vshost"))
                settingsPathname = settingsPathname.Substring(0, settingsPathname.IndexOf(".vshost"));

            settingsPathname = settingsPathname + ".xml";
            return settingsPathname;
        } 
    }
}
