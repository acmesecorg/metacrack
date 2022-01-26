using CommandLine;
using System.Reflection;

namespace Metacrack
{
    public class Program
    {
        private static Type[] _optionTypes;
        private static Type[] _pluginTypes;

        private static string[] InternalPlugins = new string[] { "catalog", "cut", "export", "lookup", "map", "parse", "rank", "sort", "split", "validate", "help", "hash" };

        public static void Main(string[] args)
        {
            if (args.Length == 0) return;

            var name = args[0].ToLower();

            Assembly pluginAssembly = null;

            //Determine if we are using an internal plugin
            if (InternalPlugins.Contains(name))
            {
                pluginAssembly = Assembly.GetExecutingAssembly();
            }
            else
            {
                //Try get an assembly matching the first argument
                pluginAssembly = LoadPlugin(name);
                if (pluginAssembly == null) return;
            }

            //https://github.com/commandlineparser/commandline/wiki/Verbs
            LoadTypes(pluginAssembly);

            var parsed = Parser.Default.ParseArguments(args, _optionTypes);

            parsed.WithParsed(Run);
            parsed.WithNotParsed(HandleErrors);
        }

        private static void Run(object obj)
        {
            //Map the verb name in the option to the name in the plugin, and start the plugin
            foreach (var type in _optionTypes)
            {
                if (obj.GetType() == type)
                {
                    var attr = type.GetCustomAttribute<VerbAttribute>();
                    
                    foreach (var type2 in _pluginTypes)
                    {                      
                        var name = type2.Name.Replace("Plugin", "").ToLower();
                        var attr2 = type2.GetCustomAttribute<PluginAttribute>();

                        if (attr2 != null) name = attr2.Name;

                        if (attr.Name == name)
                        {
                            ConsoleUtil.WriteMessage($"Using {name} plugin.", ConsoleColor.DarkYellow);

                            //Try regular synchronous invoke
                            var method = type2.GetMethod("Process", BindingFlags.Public | BindingFlags.Static);

                            if (method != null)
                            {
                                method.Invoke(null, new object[] { obj });
                                return;
                            }

                            //Try async method
                            method = type2.GetMethod("ProcessAsync", BindingFlags.Public | BindingFlags.Static);
                            if (method != null)
                            {
                                var task = (Task) method.Invoke(null, new object[] { obj });
                                task.GetAwaiter().GetResult();
                                return;
                            }
                        }
                    }
                }
            }

            //Display exception message here
            ConsoleUtil.WriteMessage($"Could not find plugin or options.", ConsoleColor.DarkRed);
        }

        private static void HandleErrors(IEnumerable<Error> obj)
        {
            //Display exception message here
            //foreach (var error in obj) ConsoleUtil.WriteMessage($"{error}", ConsoleColor.DarkRed);
        }

        //load all types using Reflection
        private static void LoadTypes(Assembly assembly)
        {
            _optionTypes = assembly.GetTypes().Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
            _pluginTypes = assembly.GetTypes().Where(t => t.Name.EndsWith("Plugin")).ToArray();
        }

        //dll must be located in the plugins folder and match the pattern with *<name>Plugin.dll
        private static Assembly LoadPlugin(string name)
        {
            // Navigate up to the solution root
            //var pluginFolder = $"{Directory.GetCurrentDirectory()}\\Plugins\\";
            var pluginFolder = $"{AppDomain.CurrentDomain.BaseDirectory}\\Plugins\\{name}\\";

            if (!Directory.Exists(pluginFolder))
            {
                ConsoleUtil.WriteMessage($"Could not find a plugin folder for {name}.", ConsoleColor.DarkRed);
                ConsoleUtil.WriteMessage($"{pluginFolder}", ConsoleColor.DarkRed);
                return null;
            }

            var files = Directory.GetFiles(pluginFolder, $"*Plugin.dll");

            if (files.Length == 0)
            {
                ConsoleUtil.WriteMessage($"Could not find a plugin dll matching {name}.", ConsoleColor.DarkRed);
                ConsoleUtil.WriteMessage($"Expected to find plugin *Plugin.dll in {pluginFolder}.", ConsoleColor.DarkRed);
                return null;
            }

            var pluginLocation = files[0];
            var loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }
    }
}