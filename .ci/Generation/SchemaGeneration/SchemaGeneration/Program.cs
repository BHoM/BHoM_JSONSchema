

using BH.oM.JsonSchema;
using SchemaGeneration;
using System.Reflection;

string rootFolder = Path.Combine(Environment.CurrentDirectory.Split(".ci")[0]); //Assumes the git folder is in the parent directory of the .ci folder
Console.WriteLine($"Root folder: {rootFolder}");

Generate.Path = rootFolder; // Set the path for schema generation

Console.WriteLine("Loading BHoM assemblies...");
List<Assembly> assemblies = AssemblyLoader.LoadAlloMAssemblies(new List<string> { "BHoM" });

ConvertConfig config = new ConvertConfig
{
    IncludeId = true,
    IncludeInnerIds = false,
    TypesAsRef = true,
    Branch = "develop"
};

foreach (Assembly assembly in assemblies)
{
    Console.WriteLine($"Generating schema for {assembly.FullName}...");
    Generate.GenerateAssembly(assembly, config);
}

