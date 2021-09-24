using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    /// <summary>
    /// Models a plugin specification.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class PluginAttribute : Attribute
    {

        /// <summary>
        /// Initializes a new instance of the PluginAttribute class.
        /// </summary>
        /// <param name="name">The long name of the verb command.</param>
        public PluginAttribute(string name, bool isDefault = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name");

            Name = name;
            IsDefault = isDefault;
        }

        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets whether this plugin is the default plugin.
        /// </summary>
        public bool IsDefault { get; private set; }
    }
}
