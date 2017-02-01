using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace Microsoft.AspNetCore.Mono
{
    public class MonoAssemblyResolver
    {
        private static string[] searchDirectories;

        static MonoAssemblyResolver()
        {
            var runtimePath = Path.GetDirectoryName(typeof(int).Assembly.Location);
            var facadesPath = Path.Combine(runtimePath, "Facades");

            searchDirectories = new string[]
            {
                Environment.CurrentDirectory,
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                runtimePath,
                facadesPath,
            };
        }

        public static string FindAssembly(string assemblyName, string hintPath = null)
        {
            foreach (string directory in GetSearchDirectories(hintPath))
            {
                FileInfo fileInfo = new FileInfo(Path.Combine(directory, assemblyName + ".dll"));
                if (fileInfo.Exists)
                    return fileInfo.FullName;
            }

            return null;
        }

        private static IEnumerable<string> GetSearchDirectories(string hintPath = null)
        {
            if (!string.IsNullOrEmpty(hintPath))
                yield return hintPath;

            foreach (string directory in searchDirectories)
                yield return directory;
        }
    }
}
