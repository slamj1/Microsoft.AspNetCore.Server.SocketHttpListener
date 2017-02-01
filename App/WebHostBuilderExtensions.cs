using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.AspNetCore.Mono;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace Microsoft.AspNetCore.Hosting
{
    public static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder UseMonoCompatibility(this IWebHostBuilder hostBuilder)
        {
            // Just return if we are not using Mono
            if (Type.GetType("Mono.Runtime") == null)
                return hostBuilder;

            // Register to assembly resolve event
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            // Access default resolver
            Type compilationLibraryType = typeof(CompilationLibrary);
            PropertyInfo defaultResolverProperty = compilationLibraryType.GetProperty("DefaultResolver", BindingFlags.NonPublic | BindingFlags.Static);

            CompositeCompilationAssemblyResolver defaultResolver = defaultResolverProperty.GetValue(null) as CompositeCompilationAssemblyResolver;

            // Access resolvers array
            Type compositeCompilationAssemblyResolverType = typeof(CompositeCompilationAssemblyResolver);
            FieldInfo resolversField = compositeCompilationAssemblyResolverType.GetField("_resolvers", BindingFlags.NonPublic | BindingFlags.Instance);

            ICompilationAssemblyResolver[] resolvers = resolversField.GetValue(defaultResolver) as ICompilationAssemblyResolver[];

            // Replace .NET resolver with a Mono one
            for (int i = 0; i < resolvers.Length; i++)
            {
                ReferenceAssemblyPathResolver referenceAssemblyPathResolver = resolvers[i] as ReferenceAssemblyPathResolver;

                if (referenceAssemblyPathResolver != null)
                    resolvers[i] = new MonoCompilationAssemblyResolver(referenceAssemblyPathResolver);
            }

            return hostBuilder;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);

            string hintPath = null;
            if (args.RequestingAssembly?.IsDynamic == false)
            {
                string requestingAssemblyPath = args.RequestingAssembly.Location;

                if (!string.IsNullOrEmpty(requestingAssemblyPath))
                    hintPath = Path.GetDirectoryName(requestingAssemblyPath);
            }

            string assemblyPath = MonoAssemblyResolver.FindAssembly(assemblyName.Name, hintPath);
            if (assemblyPath != null)
                return Assembly.LoadFile(assemblyPath);

            return null;
        }
    }
}