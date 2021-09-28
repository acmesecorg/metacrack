using CommandLine;
using System.Reflection;

namespace Malfoy
{
    public class Program
    {
        private static Type[] _optionTypes;
        private static Type[] _pluginTypes;

        public static void Main(string[] args)
        {
            //https://github.com/commandlineparser/commandline/wiki/Verbs
            LoadTypes();

            Parser.Default.ParseArguments(args, _optionTypes)
                .WithParsed(Run)
                .WithNotParsed(HandleErrors);
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
                            ConsoleUtil.WriteMessage($"Using {name} plugin", ConsoleColor.DarkYellow);

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

                    //Display exception message here
                }
            }
        }

        private static void HandleErrors(IEnumerable<Error> obj)
        {
            //throw new NotImplementedException();
        }

        //load all types using Reflection
        private static void LoadTypes()
        {
            _optionTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
            _pluginTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Name.EndsWith("Plugin")).ToArray();
        }
    }
}