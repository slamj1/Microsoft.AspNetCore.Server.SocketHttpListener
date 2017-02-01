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
    public class MonoCompilationAssemblyResolver : ICompilationAssemblyResolver
    {
        public ICompilationAssemblyResolver Resolver { get; }

        public MonoCompilationAssemblyResolver(ICompilationAssemblyResolver resolver)
        {
            Resolver = resolver;
        }

        public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string> assemblies)
        {
            string assemblyPath = MonoAssemblyResolver.FindAssembly(library.Name);

            if (assemblyPath != null)
            {
                assemblies.Add(assemblyPath);
                return true;
            }

            return Resolver.TryResolveAssemblyPaths(library, assemblies);
        }
    }
}
