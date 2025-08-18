using BH.Engine.JsonSchema;
using BH.Engine.Base;
using BH.oM.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BH.oM.JsonSchema;

namespace SchemaGeneration
{
    public static class Generate
    {
        public static void GenerateAssembly(Assembly assembly, ConvertConfig config)
        {
            var types = assembly.GetTypes();

            //Clean the folder before generating new schemas        
            if (Directory.Exists(System.IO.Path.Combine(Path, assembly.GetName().Name)))
            {
                Directory.Delete(System.IO.Path.Combine(Path, assembly.GetName().Name), true);
            }

            foreach (var type in types)
            {
                if (!type.Namespace.IsOmNamespace())
                    continue;

                if (type.IsAbstract && type.IsSealed)
                    continue;

                if (!(type.IsEnum || typeof(IObject).IsAssignableFrom(type)))
                    continue;
                string fullName = type.FullName;
                Console.WriteLine(fullName);
                BH.oM.JsonSchema.JsonSchema schema = type.ToJsonSchema(config);
                if (schema == null)
                {
                    Console.WriteLine($"Schema for {fullName} is null, skipping.");
                    continue;
                }
                string json = schema.ToText();
                string path = type.TypePath();
                CreatePathDirectory(path);
                File.WriteAllText(path, json);

            }
        }

        private static void CreatePathDirectory(string path)
        {
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                CreatePathDirectory(dir);

                Directory.CreateDirectory(dir);
            }
        }

        private static string TypePath(this Type type)
        {
            return System.IO.Path.Combine(Path, type.RelativeSchemaId());
        }

        public static string Path { get; set; }
    }
}
