using BH.Engine.Base;
using BH.Engine.JsonSchema;
using BH.Engine.Serialiser;
using BH.oM.Base;
using BH.oM.JsonSchema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BH.Test.JsonSchema
{
    public class ValidateSchemas
    {
        /***************************************************/
        /**** Test Methods                              ****/
        /***************************************************/

        [Description("Validates that all schemas are valid against the dummy objects created by the Compute.DummyObject method.")]
        [TestCaseSource(nameof(OmTypes), new object[] {false,true})]
        public void ValidateSchema(Type type)
        {
            string fullName = type.FullName;
            Console.WriteLine($"---{fullName}---");

            var dummy = BH.Engine.Test.Compute.DummyObject(type);
            string json = BH.Engine.Serialiser.Convert.ToJson(dummy);
            System.Text.Json.Nodes.JsonNode node = System.Text.Json.Nodes.JsonNode.Parse(json);

            Uri id = type.SchemaId(BranchName);
            Assert.Multiple(() =>
            {
                Console.WriteLine($"As {type.Name}");
                bool success = CheckAgainstSchema(node, id);
                Assert.That(success, Is.True, $"{type.Name} failed schema validation");

                bool isCustomObject = type == typeof(CustomObject);
                if (isCustomObject)
                    Warn.If(type, Is.EqualTo(typeof(CustomObject)), "CustomObject is a special case, it does not work with interface schema inheritance as it is missing \"_t\"");
                else
                {
                    foreach (Type subType in type.GetInterfaces())
                    {
                        if (typeof(IObject).IsAssignableFrom(subType))
                        {
                            Console.WriteLine($"As {subType.Name}");
                            success = CheckAgainstSchema(node, subType.SchemaId(BranchName));
                            Assert.That(success, Is.True, $"{type.Name} failed schema validation");
                        }
                    }
                }
            });
        }

        /***************************************************/

        [TestCaseSource(nameof(OmTypes), new object[] { false, true })]
        [Description("Checks that all schemas evaluates as invalid if an invalid property type has been set. For enum types it instead sets an invalid enum value.")]
        public void TestInvalidPropertyTypeEvaluatesAsInvalid(Type type)
        {
            Assume.That(type != typeof(CustomObject), "CustomObject is a special case, it always evaluates as true as it is used as the fallback case for deserialisation");
            Assume.That(type != typeof(FragmentSet), "FragmentSet is a special case that cant be validated by the simplified approach in this method.");

            string fullName = type.FullName;
            Console.WriteLine($"---{fullName}---");

            var dummy = BH.Engine.Test.Compute.DummyObject(type);
            string json = BH.Engine.Serialiser.Convert.ToJson(dummy);
            System.Text.Json.Nodes.JsonNode node = System.Text.Json.Nodes.JsonNode.Parse(json);

            if (type.IsEnum)
            {
                node["Value"] = System.Text.Json.Nodes.JsonValue.Create("InvalidEnumValue"); // Set an invalid enum value
            }
            else
            {
                var propsToUpdate = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(p => (p.PropertyType != typeof(object)) && !p.PropertyType.IsGenericParameter && !typeof(Delegate).IsAssignableFrom(p.PropertyType)).ToList();

                Assume.That(propsToUpdate.Count > 0, $"{type.Name} has no properties to update for invalid data test");

                foreach (var property in propsToUpdate)
                {
                    Type propType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType; // Get the underlying type if nullable
                    if (propType == typeof(bool))
                    {
                        node[property.Name] = System.Text.Json.Nodes.JsonValue.Create(45.2); ; // Set a double value to all properties
                    }
                    else
                        node[property.Name] = System.Text.Json.Nodes.JsonValue.Create(true); // Set a bool value to all properties

                }
            }

            bool success = CheckAgainstSchema(node, type.SchemaId(BranchName));
            Assert.That(success, Is.False, $"{type.Name} should have failed schema validation with invalid data");
        }

        /***************************************************/

        [TestCaseSource(nameof(OmTypes), new object[] { false, false })]
        [Description("Checks that all schemas evaluates as invalid if an invalid property type has been set to a subtype. Types that do not have subtypes are skipped.")]
        public void TestInvalidPropertyTypeOnSubObjectsEvaluatesAsInvalid(Type type)
        {
            Assume.That(type != typeof(CustomObject), "CustomObject is a special case, it always evaluates as true as it is used as the fallback case for deserialisation");
            Assume.That(type != typeof(FragmentSet), "FragmentSet is a special case that cant be validated by the simplified approach in this method.");
            Assume.That(!type.IsEnum, "This test is not applicable to enum types as they do not have sub objects properties.");


            string fullName = type.FullName;
            Console.WriteLine($"---{fullName}---");

            var dummy = BH.Engine.Test.Compute.DummyObject(type);
            string json = BH.Engine.Serialiser.Convert.ToJson(dummy);
            System.Text.Json.Nodes.JsonNode node = System.Text.Json.Nodes.JsonNode.Parse(json);


            var propsToUpdate = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(p => typeof(IObject).IsAssignableFrom(p.PropertyType) && !p.PropertyType.IsAbstract)
                                    .Where(p => p.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(pi => (pi.PropertyType != typeof(object)) && !pi.PropertyType.IsGenericParameter && !typeof(Delegate).IsAssignableFrom(pi.PropertyType)).Count() > 0).ToList();

            bool wasUpdated = false;

            foreach (var property in propsToUpdate)
            {
                System.Text.Json.Nodes.JsonObject innerObject = node[property.Name] as System.Text.Json.Nodes.JsonObject;
                if (innerObject != null)
                {
                    foreach (var innerProp in property.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(p => (p.PropertyType != typeof(object)) && !p.PropertyType.IsGenericParameter && !typeof(Delegate).IsAssignableFrom(p.PropertyType)))
                    {
                        Type propType = Nullable.GetUnderlyingType(innerProp.PropertyType) ?? innerProp.PropertyType; // Get the underlying type if nullable

                        if (propType == typeof(bool))
                        {
                            innerObject[innerProp.Name] = System.Text.Json.Nodes.JsonValue.Create(1.0); ; // Set a double value to all properties
                        }
                        else
                            innerObject[innerProp.Name] = System.Text.Json.Nodes.JsonValue.Create(true); // Set a bool value to all properties

                        wasUpdated = true;
                    }
                }
            }

            Assume.That(wasUpdated, $"{type.Name} has no inner properties to update for invalid data test");

            bool success = CheckAgainstSchema(node, type.SchemaId(BranchName));
            Assert.That(success, Is.False, $"{type.Name} should have failed schema validation with invalid data");
        }

        /***************************************************/

        [TestCaseSource(nameof(OmTypes), new object[] { false, false })]
        [Description("CChecks that all schemas evaluates as invalid if an invalid if required properties are missing. Skipped for enum types")]
        public void TestMissingRequiredPropertiesEvaluatesAsInvalid(Type type)
        {
            Assume.That(type != typeof(BHoMObject), "BHoMObject is a special case which does not have any required properties");
            Assume.That(type != typeof(CustomObject), "CustomObject is a special case, it always evaluates as true as it is used as the fallback case for deserialisation");
            Assume.That(type != typeof(FragmentSet), "FragmentSet is a special case that cant be validated by the simplified approach in this method.");
            Assume.That(!type.IsEnum, "This test is not applicable to enum types as they do not have properties.");
            Assume.That(!typeof(IDynamicObject).IsAssignableFrom(type), "This test is not applicable to dynamic objects as all of their properties are optional.");

            string fullName = type.FullName;
            Console.WriteLine($"---{fullName}---");

            var dummy = BH.Engine.Test.Compute.DummyObject(type);
            string json = BH.Engine.Serialiser.Convert.ToJson(dummy);
            System.Text.Json.Nodes.JsonNode node = System.Text.Json.Nodes.JsonNode.Parse(json);


            var propsToUpdate = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                    .Where(p => (p.PropertyType != typeof(object)) && !p.PropertyType.IsGenericParameter && !typeof(Delegate).IsAssignableFrom(p.PropertyType)).ToList();

            Assume.That(propsToUpdate.Count > 0, $"{type.Name} has no properties to update for invalid data test");

            foreach (var property in propsToUpdate)
            {
                node.AsObject().Remove(property.Name); // Remove all properties to simulate missing required properties
            }


            bool success = CheckAgainstSchema(node, type.SchemaId(BranchName));
            Assert.That(success, Is.False, $"{type.Name} should have failed schema validation with invalid data");
        }

        /***************************************************/

        [Test]
        [Description("Checks that all assemblies are available in the BHoM folder to ensure the schema valiadtion actually checks all schemas.")]
        public void TestAllTypesAvailable()
        { 
            List<Type> types = OmTypes(true, true).ToList();
            Dictionary<Uri, Json.Schema.JsonSchema> schemas = Schemas.ToDictionary(x => x.Key, x => x.Value);

            List<Type> typesWithMissingSchemas = new List<Type>();

            foreach (var type in types)
            {
                Uri id = type.SchemaId(BranchName);
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

        private static bool CheckAgainstSchema(System.Text.Json.Nodes.JsonNode node, Uri id)
        {
            bool success = false;
            if (Schemas.TryGetValue(id, out Json.Schema.JsonSchema schema))
            {
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

        [Description("Returns all types in the BHoM assemblies that are either enums or implement IObject.")]
        public static IEnumerable<Type> OmTypes(bool includeAbstract, bool includeEnums)
        {
            foreach (var assembly in LoadAlloMAssemblies(new List<string> { "BHoM" }))
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.Namespace.IsOmNamespace())
                        continue;

                    if (!(type.IsEnum || typeof(IObject).IsAssignableFrom(type)))
                        continue;

                    if (!includeEnums && type.IsEnum)
                        continue;

                    if (!includeAbstract && type.IsAbstract)
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
        public static string BranchName { get; set; } = null;
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
                if (BranchName == null) //Extract branch name from the schema file path
                {
                    string[] idPaths = id.Id.AbsolutePath.Split("/");
                    int branchIndex = Array.IndexOf(idPaths, "BHoM_JSONSchema") + 1; // Assumes the branch name is the next folder after the repository name
                    if (branchIndex < idPaths.Length && !string.IsNullOrWhiteSpace(idPaths[branchIndex]))
                    {
                        BranchName = idPaths[branchIndex];
                        Console.WriteLine($"Branch name set to: {BranchName}");
                    }
                }
                Schemas[id.Id] = schema;
            }

            if (BranchName == null) //Should not happen, but setting to develop as a fallback
            {
                BranchName = "develop"; // Default branch name if not found
                Console.WriteLine($"Branch name not found, defaulting to: {BranchName}");
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
            if(m_LoadedoMAssemblies != null && m_LoadedoMAssemblies.Count > 0)
                return m_LoadedoMAssemblies; // Already loaded

            string regexFilter = @"oM$";
            m_LoadedoMAssemblies = new List<Assembly>();

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
                        m_LoadedoMAssemblies.Add(loaded);
                }
            }

            return m_LoadedoMAssemblies;
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
        /**** Assembly Loader Fields                    ****/
        /***************************************************/

        private static List<Assembly> m_LoadedoMAssemblies = null;

        /***************************************************/

    }
}
