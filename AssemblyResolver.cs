using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Okin
{
    public class AssemblyResolver
    {
        private static volatile AssemblyResolver instance;
        private static readonly object SyncRoot = new object();
        private readonly List<string> assembliesLocation;
        private readonly List<int> domainsResolverRegistration;
        private readonly List<string> languagesDirectories;

        /// <summary>
        /// List of locations currently used for runtime assembly resolution.
        /// If you want to add a new path, use <see cref="AddPathToLocations(string)"/>
        /// </summary>
        public string[] AssembliesLocations
        {
            get { return this.assembliesLocation.ToArray(); }
        }

        /// <summary>
        /// AssemblyResolver is a thread safe singleton. Use this property to access its instance.
        /// </summary>
        public static AssemblyResolver Instance
        {
            get
            {
                if (instance != null)
                    return instance;
                lock(SyncRoot)
                {
                    if (instance == null)
                        instance = new AssemblyResolver();
                }
                return instance;
            }
        }

        private AssemblyResolver()
        {
            this.assembliesLocation = new List<string>();
            this.languagesDirectories = new List<string>();
            this.domainsResolverRegistration = new List<int>();
        }

        /// <summary>
        /// Add the given path to assemblies locations used for runtime resolution
        /// </summary>
        /// <param name="path">Full path to use</param>
        public void AddPathToLocations(string path)
        {
            path = Path.GetFullPath(path);
            if (this.assembliesLocation.Contains(path) || !Directory.Exists(path))
                return;
            this.assembliesLocation.Add(path);
        }

        /// <summary>
        /// Initialize the assembly resolver:
        /// - Subscribe to current domain AssemblyResolve event
        /// - Parse the configuration to resolve assemblies directories
        /// </summary>
        /// <param name="openExeConfiguration">The .config to use for reading configuration. If null, then the default .config will be used</param>
        public void Initialize(Configuration openExeConfiguration = null)
        {
            this.RegisterAssemblyResolverInDomain();
            this.DirectoriesInitialization(openExeConfiguration);
        }

        /// <summary>
        /// Subscribe to AssemblyResolver event in domain.
        /// </summary>
        /// <param name="domain">Domain to register in, if null, then the current AppDomain will be used</param>
        public void RegisterAssemblyResolverInDomain(AppDomain domain = null)
        {
            if (domain == null)
                domain = AppDomain.CurrentDomain;
            if (this.domainsResolverRegistration.Contains(domain.Id))
                return;
            domain.AssemblyResolve += this.RuntimeAssemblyResolver;
            this.domainsResolverRegistration.Add(domain.Id);
        }

        /// <summary>
        /// The method called when an AssemblyResolve event is raised
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public Assembly RuntimeAssemblyResolver(object sender, ResolveEventArgs args)
        {
            var fields = args.Name.Split(',');
            if (fields.Length == 0)
                return null;

            var assemblyName = fields[0];
            var assemblyFileName = assemblyName + ".dll";
            string assemblyPath;

            // Target assembly is a language resource, let's use LanguagesDirectories to resolve it
            if (assemblyName.EndsWith(".resources"))
            {
                var assemblyCulture = fields.Length < 2 ? null : this.AssemblyCultureResolver(fields[2]);
                if (assemblyCulture == null)
                    return null;
                foreach (var languageDirectory in this.languagesDirectories)
                {
                    if (!Directory.Exists(languageDirectory))
                        continue;
                    var resourceDirectory = Path.Combine(languageDirectory, assemblyCulture);
                    if (!Directory.Exists(resourceDirectory))
                        continue;
                    assemblyPath = Path.Combine(resourceDirectory, assemblyFileName);
                    if (!File.Exists(assemblyPath))
                        continue;
                    var loadingAssembly = Assembly.LoadFrom(assemblyPath);
                    return loadingAssembly;
                }
            }
            else
            {
                foreach (var assemblyDirectory in this.assembliesLocation)
                {
                    assemblyPath = Path.Combine(assemblyDirectory, assemblyFileName);
                    if (!File.Exists(assemblyPath))
                        continue;
                    var loadingAssembly = Assembly.LoadFrom(assemblyPath);
                    return loadingAssembly;
                }
            }
            return null;
        }

        private string AssemblyCultureResolver(string requestedAssemblyVersion)
        {
            var assemblyCulture = requestedAssemblyVersion.Substring(requestedAssemblyVersion.IndexOf('=') + 1);
            if (this.languagesDirectories == null)
                return null;
            foreach (var languageDirectory in this.languagesDirectories)
            {
                var directories = Directory.GetDirectories(languageDirectory);
                assemblyCulture = directories.FirstOrDefault(dir => assemblyCulture != null && assemblyCulture.StartsWith(new DirectoryInfo(dir).Name));
                if (assemblyCulture != null)
                    return assemblyCulture;
            }
            return null;
        }

        private void DirectoriesInitialization(Configuration openExeConfiguration = null)
        {
            this.assembliesLocation.Clear();
            this.languagesDirectories.Clear();
            var locationsString = openExeConfiguration == null
                                      ? ConfigurationManager.AppSettings["AssembliesSource"]
                                      : openExeConfiguration.AppSettings.Settings["AssembliesSource"].Value;
            if (locationsString != null)
            {
                var locations = locationsString.Split(';').ToList();
                foreach (var location in locations.Where(e => !string.IsNullOrEmpty(e)))
                {
                    if (location.EndsWith("*"))
                        this.RecursiveDirectoryExplorer(location.Remove(location.Length - 1), true);
                    else
                        this.RecursiveDirectoryExplorer(location, false);
                }
            }

            var langs = openExeConfiguration == null
                            ? ConfigurationManager.AppSettings["LanguagesDirectories"]
                            : openExeConfiguration.AppSettings.Settings["LanguagesDirectories"].Value;
            if (this.languagesDirectories != null)
                this.languagesDirectories.AddRange(langs.Split(';'));
        }

        private void RecursiveDirectoryExplorer(string directory, bool recursive)
        {
            if (!Directory.Exists(directory))
                return;
            this.assembliesLocation.Add(Path.GetFullPath(directory));
            if (!recursive)
                return;
            var subDirectories = Directory.GetDirectories(directory);
            foreach (var subDirectory in subDirectories)
                this.RecursiveDirectoryExplorer(subDirectory, true);
        }
    }
}
