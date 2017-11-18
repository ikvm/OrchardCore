using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;
using OrchardCore.Modules.FileProviders;

namespace OrchardCore.DisplayManagement.Liquid
{
    /// <summary>
    /// This custom <see cref="IFileProvider"/> implementation provides the file contents
    /// of Module Project Liquid files while in a development environment.
    /// </summary>
    public class ModuleProjectLiquidFileProvider : IFileProvider
    {
        private const string ModuleAssetsMap = "module.assets.map";
        private const string ModulesNamesMap = "module.names.map";

        private static Dictionary<string, string> _paths;
        private static object _synLock = new object();

        public ModuleProjectLiquidFileProvider(IHostingEnvironment hostingEnvironment)
        {
            if (_paths != null)
            {
                return;
            }

            lock (_synLock)
            {
                if (_paths == null)
                {
                    if (_paths == null)
                    {
                        var mainAssembly = Assembly.Load(new AssemblyName(hostingEnvironment.ApplicationName));
                        var fileProvider = new EmbeddedFileProvider(mainAssembly);

                        var fileInfo = fileProvider.GetFileInfo(ModulesNamesMap);
                        var modules = fileInfo.ReadAllLines().ToList();
                        var paths = new List<string>();

                        foreach (var module in modules)
                        {
                            var assembly = Assembly.Load(module);

                            if (Path.GetDirectoryName(assembly.Location)
                                != Path.GetDirectoryName(mainAssembly.Location))
                            {
                                continue;
                            }

                            fileProvider = new EmbeddedFileProvider(Assembly.Load(module));
                            fileInfo = fileProvider.GetFileInfo(ModuleAssetsMap);

                            var assetPaths = fileInfo.ReadAllLines().Select(x => x.Replace('\\', '/'));
                            var projectFolder = assetPaths.FirstOrDefault();

                            if (Directory.Exists(projectFolder))
                            {
                                assetPaths = assetPaths.Skip(1).Where(x => x.EndsWith(".liquid")).ToList();

                                paths.AddRange(assetPaths.Select(x => projectFolder + "/"
                                    + x.Substring(("Modules/" + module).Length) + "|/" + x));
                            }
                        }

                        _paths = new Dictionary<string, string>(paths
                            .Select(x => x.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                            .Where(x => x.Length == 2).ToDictionary(x => x[1].Replace('\\', '/'), x => x[0]));
                    }
                }
            }
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return NotFoundDirectoryContents.Singleton;
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            if (subpath == null)
            {
                return new NotFoundFileInfo(subpath);
            }

            subpath = subpath.Replace("\\", "/");

            if (_paths.ContainsKey(subpath))
            {
                return new PhysicalFileInfo(new FileInfo(_paths[subpath]));
            }

            return new NotFoundFileInfo(subpath);
        }

        public IChangeToken Watch(string filter)
        {
            if (filter == null)
            {
                return NullChangeToken.Singleton;
            }

            filter = filter.Replace("\\", "/");

            if (_paths.ContainsKey(filter))
            {
                return new PollingFileChangeToken(new FileInfo(_paths[filter]));
            }

            return NullChangeToken.Singleton;
        }
    }
}
