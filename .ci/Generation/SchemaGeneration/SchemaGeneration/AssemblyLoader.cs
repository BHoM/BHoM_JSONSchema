using BH.Engine.Base;
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
                    if (loaded != null && loaded.IsOmAssembly() && loaded.IsInOrg(organisations))
                        result.Add(loaded);
                }
            }

            return result;
        }

        /***************************************************/

        [Description("Checks whether a given assembly is tagged as part of the BHoM organisation.")]
        public static bool IsInOrg(this Assembly assembly, List<string> orgs)
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
