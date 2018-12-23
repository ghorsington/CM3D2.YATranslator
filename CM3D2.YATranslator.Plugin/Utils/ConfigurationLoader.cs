using System;
using System.Linq;
using System.Reflection;
using ExIni;

namespace CM3D2.YATranslator.Plugin.Utils
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigSectionAttribute : Attribute
    {
        public ConfigSectionAttribute(string section)
        {
            Section = section;
        }

        public string Section { get; }
    }

    public class ConfigurationLoader
    {
        public static T LoadConfig<T>(IniFile ini) where T : new()
        {
            var configObject = new T();
            LoadConfig(configObject, ini, "Config");
            return configObject;
        }

        public static void LoadConfig(object configObject, IniFile ini, string configSection)
        {
            var configType = configObject.GetType();

            var fields = configType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !f.IsInitOnly);
            var properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                       .Where(p => p.GetGetMethod() != null && p.GetSetMethod() != null);

            var section = ini[configSection];

            foreach (var field in fields)
            {
                if (TryInitSubsection(field.FieldType, ini, out var subsection))
                {
                    field.SetValue(configObject, subsection);
                    continue;
                }

                string fieldName = field.Name;
                string defaultValue = Convert.ToString(field.GetValue(configObject));

                if (!section.HasKey(fieldName))
                {
                    section[fieldName].Value = defaultValue;
                    continue;
                }

                try
                {
                    field.SetValue(configObject, Convert.ChangeType(section[fieldName].Value, field.FieldType));
                }
                catch (Exception)
                {
                    section[fieldName].Value = defaultValue;
                }
            }

            foreach (var property in properties)
            {
                if (TryInitSubsection(property.PropertyType, ini, out var subsection))
                {
                    property.SetValue(configObject, subsection, null);
                    continue;
                }

                string propertyName = property.Name;
                string defaultValue = Convert.ToString(property.GetValue(configObject, null));

                if (!section.HasKey(propertyName))
                {
                    section[propertyName].Value = defaultValue;
                    continue;
                }

                try
                {
                    property.SetValue(configObject, Convert.ChangeType(section[propertyName].Value, property.PropertyType), null);
                }
                catch (Exception)
                {
                    section[propertyName].Value = defaultValue;
                }
            }
        }

        private static bool TryInitSubsection(Type subsectionType, IniFile ini, out object subsection)
        {
            subsection = null;
            var sectionAttr = subsectionType.GetCustomAttributes(typeof(ConfigSectionAttribute), true);

            if (sectionAttr.Length <= 0)
                return false;
            var attr = (ConfigSectionAttribute) sectionAttr[0];
            if (string.IsNullOrEmpty(attr.Section))
                return true;
            try
            {
                subsection = Activator.CreateInstance(subsectionType);
                LoadConfig(subsection, ini, attr.Section);
            }
            catch (Exception)
            {
                subsection = null;
                return true;
            }

            return true;
        }
    }
}