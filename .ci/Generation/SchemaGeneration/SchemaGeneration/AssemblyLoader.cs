using BH.Engine.Base;
using BH.Engine.JsonSchema;
using BH.oM.JsonSchema;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SchemaGeneration
{
    public static class AssemblyLoader
    {
        /***************************************************/

        public static List<Assembly> LoadAlloMAssemblies(ConvertConfig config)
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
                    try
                    {
                        Assembly loaded = Assembly.LoadFrom(file);
                        if (loaded != null && loaded.IsOmAssembly() && loaded.IsInOrg(config))
                            result.Add(loaded);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error loading assembly {name}: {e.Message}");
                    }

                }
            }

            return result;
        }

        /***************************************************/

    }
}
