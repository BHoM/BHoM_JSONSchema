using BH.Engine.Base;
using BH.Engine.JsonSchema;
using BH.oM.Base;
using BH.oM.JsonSchema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BH.Test.JsonSchema
{
    public class ValidateSchemas
    {
        /***************************************************/
        /**** Test Methods                              ****/
        /***************************************************/

        [TestCaseSource(nameof(OmTypes))]
        public void ValidateSchema(Type type)
        {
            string fullName = type.FullName;
            Console.WriteLine($"---{fullName}---");

            var dummy = BH.Engine.Test.Compute.DummyObject(type);
            Uri id = type.SchemaId();
            Assert.Multiple(() =>
            {
                Console.WriteLine($"As {type.Name}");
                bool success = CheckAgainstSchema(dummy, id);
                Assert.That(success, Is.True, $"{type.Name} failed schema validation");


                bool isCustomObject = type == typeof(CustomObject);
                if (isCustomObject)
                    Warn.If(isCustomObject, "CustomObject is a special case, it does not work with interface schema inheritance as it is missing \"_t\"");
                else
                {
                    foreach (Type subType in type.GetInterfaces())
                    {
                        if (typeof(IObject).IsAssignableFrom(subType))
                        {
                            Console.WriteLine($"As {subType.Name}");
                            success = CheckAgainstSchema(dummy, subType.SchemaId());
                            Assert.That(success, Is.True, $"{type.Name} failed schema validation");
                        }
                    }
                }
            });
        }

        /***************************************************/

        [Test]
        [Description("Checks that all assemblies are available in the BHoM folder to ensure the schema valiadtion actually checks all schemas.")]
        public void TestAllTypesAvailable()
        { 
            List<Type> types = OmTypesIncludingAbstractAndInterfaces().ToList();
            Dictionary<Uri, Json.Schema.JsonSchema> schemas = Schemas.ToDictionary(x => x.Key, x => x.Value);

            List<Type> typesWithMissingSchemas = new List<Type>();

            foreach (var type in types)
            {
                Uri id = type.SchemaId();
                if (schemas.TryGetValue(id, out Json.Schema.JsonSchema schema))
                    schemas.Remove(id);
                else
                    typesWithMissingSchemas.Add(type);
            }

            Assert.Multiple(() =>
            {
                Assert.That(typesWithMissingSchemas.Count, Is.EqualTo(0), $"The following types are missing schemas: {string.Join("\n", typesWithMissingSchemas.Select(x => x.FullName))}");
                Assert.That(schemas.Count, Is.EqualTo(0), $"The following schemas are not used: {string.Join("\n", schemas.Keys.Select(x => x.ToString()))}");
            });
        }

        /***************************************************/

        private static bool CheckAgainstSchema(object obj, Uri id)
        {
            bool success = false;
            if (Schemas.TryGetValue(id, out Json.Schema.JsonSchema schema))
            {
                string json = BH.Engine.Serialiser.Convert.ToJson(obj);
                System.Text.Json.Nodes.JsonNode node = System.Text.Json.Nodes.JsonNode.Parse(json);

                var res = schema.Evaluate(node, Options);
                success = res.IsValid;
                if (res.IsValid)
                    Console.WriteLine("Valid");
                else
                {
                    Console.WriteLine("Invalid");
                    PrintInvalid(res);
                    List<Json.Schema.EvaluationResults> results = res.Details?.Where(x => !x.IsValid).ToList() ?? new List<Json.Schema.EvaluationResults>();
                }
            }
            else
            {
                Console.WriteLine("No schema");
            }
            return success;
        }

        /***************************************************/

        private static void PrintInvalid(Json.Schema.EvaluationResults results)
        {
            if (results == null) return;

            if (results.Errors != null)
            {
                Console.WriteLine("- " + results.EvaluationPath);
                foreach (var error in results.Errors)
                {
                    Console.WriteLine($"{error.Key}: {error.Value}");
                }
            }

            if (results.HasDetails && !results.IsValid)
                foreach (var details in results.Details)
                {
                    PrintInvalid(details);
                }
        }

        /***************************************************/
        /**** Test Data methods                         ****/
        /***************************************************/

        public static IEnumerable<Type> OmTypes()
        {
            return OmTypesIncludingAbstractAndInterfaces().Where(x => !x.IsAbstract);
        }

        /***************************************************/

        public static IEnumerable<Type> OmTypesIncludingAbstractAndInterfaces()
        {
            foreach (var assembly in LoadAlloMAssemblies(new List<string> { "BHoM" }))
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.Namespace.IsOmNamespace())
                        continue;

                    if (!(type.IsEnum || typeof(IObject).IsAssignableFrom(type)))
                        continue;

                    yield return type;
                }
            }
        }

        /***************************************************/
        /**** Properties                                ****/
        /***************************************************/

        public static Dictionary<Uri, Json.Schema.JsonSchema> Schemas { get; set; } = null;
        public static Json.Schema.EvaluationOptions Options { get; set; } = new Json.Schema.EvaluationOptions { OutputFormat = Json.Schema.OutputFormat.Hierarchical };

        /***************************************************/
        /**** Setup methods                             ****/
        /***************************************************/

        [OneTimeSetUp]
        public static void ReadAllSchemas()
        {
            if(Schemas != null)
                return; // Already loaded

            Schemas = new Dictionary<Uri, Json.Schema.JsonSchema>();

            foreach (var schemaFile in Directory.EnumerateFiles(SchemasPath(), "*.json", SearchOption.AllDirectories))
            {
                if(schemaFile.Contains("\\Build\\") || 
                   schemaFile.Contains("\\bin\\") || 
                   schemaFile.Contains("\\obj\\") || 
                   schemaFile.Contains("\\.ci\\") || 
                   schemaFile.Contains("\\.vs\\") || 
                   schemaFile.Contains("\\.git\\") || 
                   schemaFile.Contains("\\.gitbub\\"))
                    continue; // Skip build folders

                string schemaJson = File.ReadAllText(schemaFile);
                var schema = Json.Schema.JsonSchema.FromFile(schemaFile);
                Json.Schema.SchemaRegistry.Global.Register(schema);
                Options.SchemaRegistry.Register(schema);
                Json.Schema.IdKeyword id = schema.Keywords.OfType<Json.Schema.IdKeyword>().First();
                Schemas[id.Id] = schema;
            }
        }

        /***************************************************/

        private static string SchemasPath()
        {
            string rootFolder = Path.Combine(Environment.CurrentDirectory.Split(".ci")[0]); //Assumes the git folder is in the parent directory of the .ci folder
            return rootFolder;
        }

        /***************************************************/
        /**** Assembly Loader Methods                   ****/
        /***************************************************/

        [Description("Loads all assemblies that end with oM from the BHoM folder.")]
        public static List<Assembly> LoadAlloMAssemblies(List<string> organisations)
        {
            string regexFilter = @"oM$";
            List<Assembly> result = new List<Assembly>();

            string folder = BH.Engine.Base.Query.BHoMFolder();

            Regex regex;
            if (!string.IsNullOrWhiteSpace(regexFilter))
                regex = new Regex(regexFilter);
            else
                regex = new Regex(".*");

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            foreach (string file in Directory.GetFiles(folder, "*.dll", searchOption))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (regex.IsMatch(name))
                {
                    Assembly loaded = Assembly.LoadFrom(file);
                    if (loaded != null && loaded.IsOmAssembly() && IsInOrg(loaded, organisations))
                        result.Add(loaded);
                }
            }

            return result;
        }

        /***************************************************/

        [Description("Checks if the assembly is a BHoM assembly.")]
        public static bool IsInOrg(Assembly assembly, List<string> orgs)
        {
            AssemblyDescriptionAttribute atr = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
            if (atr != null)
            {
                return orgs.Any(x => atr.Description.Contains($"github.com/{x}"));
            }
            return false;
        }

        /***************************************************/

    }
}
