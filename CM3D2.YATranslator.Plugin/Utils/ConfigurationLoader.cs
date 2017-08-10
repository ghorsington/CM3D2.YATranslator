using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExIni;

namespace CM3D2.YATranslator.Plugin.Utils
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigSectionAttribute : Attribute
    {
        public string Section { get; }

        public ConfigSectionAttribute(string section)
        {
            Section = section;
        }
    }

    public class ConfigurationLoader
    {
        public static T LoadConfig<T>(IniFile ini) where T : new()
        {
            T configObject = new T();
            LoadConfig(configObject, ini, "Config");
            return configObject;
        }

        private static bool TryInitSubsection(Type subsectionType, IniFile ini, out object subsection)
        {
            subsection = null;
            object[] sectionAttr = subsectionType.GetCustomAttributes(typeof(ConfigSectionAttribute), true);

            if (sectionAttr.Length <= 0)
                return false;
            ConfigSectionAttribute attr = (ConfigSectionAttribute) sectionAttr[0];
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

        public static void LoadConfig(object configObject, IniFile ini, string configSection)
        {
            Type configType = configObject.GetType();

            IEnumerable<FieldInfo> fields = configType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                                                      .Where(f => !f.IsInitOnly);
            IEnumerable<PropertyInfo> properties = configType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetGetMethod() != null && p.GetSetMethod() != null);

            IniSection section = ini[configSection];

            foreach (FieldInfo field in fields)
            {
                if (TryInitSubsection(field.FieldType, ini, out object subsection))
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

            foreach (PropertyInfo property in properties)
            {
                if (TryInitSubsection(property.PropertyType, ini, out object subsection))
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
                    property.SetValue(configObject,
                                      Convert.ChangeType(section[propertyName].Value, property.PropertyType),
                                      null);
                }
                catch (Exception)
                {
                    section[propertyName].Value = defaultValue;
                }
            }
        }
    }
}