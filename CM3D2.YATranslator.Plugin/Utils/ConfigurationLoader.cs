using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExIni;

namespace CM3D2.YATranslator.Plugin.Utils
{
    public class ConfigurationLoader
    {
        public static T LoadConfig<T>(IniFile ini) where T : new()
        {
            T configObject = new T();
            LoadConfig(configObject, ini, "Config");
            return configObject;
        }

        public static void LoadConfig<T>(T configObject, IniFile ini, string configSection)
        {
            Type configType = typeof(T);

            IEnumerable<FieldInfo> fields = configType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                                                      .Where(f => !f.IsInitOnly);
            IEnumerable<PropertyInfo> properties = configType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetGetMethod() != null && p.GetSetMethod() != null);

            IniSection section = ini[configSection];

            foreach (FieldInfo field in fields)
            {
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