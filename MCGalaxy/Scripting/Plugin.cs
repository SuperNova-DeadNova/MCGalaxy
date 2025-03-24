/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCGalaxy)
    
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
*/
using System;
using System.Collections.Generic;
using MCGalaxy.Core;
using MCGalaxy.Modules.Moderation.Notes;
using MCGalaxy.Modules.Relay.Discord;
using MCGalaxy.Modules.Relay.IRC;
using MCGalaxy.Modules.Security;
using MCGalaxy.Scripting;
using NotAwesomeSurvival;
namespace MCGalaxy 
{
    /// <summary> This class provides for more advanced modification to MCGalaxy </summary>
    public abstract class Plugin 
    {
        /// <summary> Hooks into events and initalises states/resources etc </summary>
        /// <param name="auto"> True if plugin is being automatically loaded (e.g. on server startup), false if manually. </param>
        public abstract void Load(bool auto);
        
        /// <summary> Unhooks from events and disposes of state/resources etc </summary>
        /// <param name="auto"> True if plugin is being auto unloaded (e.g. on server shutdown), false if manually. </param>
        public abstract void Unload(bool auto);
        
        /// <summary> Called when a player does /Help on the plugin. Typically tells the player what this plugin is about. </summary>
        /// <param name="p"> Player who is doing /Help. </param>
        public virtual void Help(Player p) {
            p.Message("No help is available for this plugin.");
        }
        
        /// <summary> Name of the plugin. </summary>
        public abstract string name { get; }
        /// <summary> Oldest version of MCGalaxy this plugin is compatible with. </summary>
        public abstract string MCGalaxy_Version { get; }
        /// <summary> Version of this plugin. </summary>
        public virtual int build { get { return 0; } }
        /// <summary> Message to display once this plugin is loaded. </summary>
        public virtual string welcome { get { return ""; } }
        /// <summary> The creator/author of this plugin. (Your name) </summary>
        public virtual string creator { get { return ""; } }
        /// <summary> Whether or not to auto load this plugin on server startup. </summary>
        public virtual bool LoadAtStartup { get { return true; } }
        
        
        internal static List<Plugin> core = new List<Plugin>();
        public static List<Plugin> all = new List<Plugin>();
        
        public static bool Load(Plugin p, bool auto) {
            try {
                string ver = p.MCGalaxy_Version;
                if (!String.IsNullOrEmpty(ver) && new Version(ver) > new Version(Server.Version)) {
                    Logger.Log(LogType.Warning, "Plugin ({0}) requires a more recent version of {1}!", p.name, Server.SoftwareName);
                    return false;
                }
                all.Add(p);
                
                if (p.LoadAtStartup || !auto) {
                    p.Load(auto);
                    Logger.Log(LogType.SystemActivity, "Plugin {0} loaded...build: {1}", p.name, p.build);
                } else {
                    Logger.Log(LogType.SystemActivity, "Plugin {0} was not loaded, you can load it with /pload", p.name);
                }
                
                if (!String.IsNullOrEmpty(p.welcome)) Logger.Log(LogType.SystemActivity, p.welcome);
                return true;
            } catch (Exception ex) {
                Logger.LogError("Error loading plugin " + p.name, ex);               
                if (!String.IsNullOrEmpty(p.creator)) Logger.Log(LogType.Warning, "You can go bug {0} about it.", p.creator);
                return false;
            }
        }

        public static bool Unload(Plugin p, bool auto) {
            bool success = true;
            try {
                p.Unload(auto);
                Logger.Log(LogType.SystemActivity, "Plugin {0} was unloaded.", p.name);
            } catch (Exception ex) {
                Logger.LogError("Error unloading plugin " + p.name, ex);
                success = false;
            }
            
            all.Remove(p);
            return success;
        }

        public static void UnloadAll() {
            for (int i = 0; i < all.Count; i++) {
                Unload(all[i], true); i--;
            }
        }

        public static void LoadAll() {
            LoadCorePlugin(new CorePlugin());
            LoadCorePlugin(new NotesPlugin());
            LoadCorePlugin(new DiscordPlugin());
            LoadCorePlugin(new IRCPlugin());
            LoadCorePlugin(new IPThrottler());
            LoadCorePlugin(new Nas());
            IScripting.AutoloadPlugins();
        }
        
        static void LoadCorePlugin(Plugin plugin) {
            plugin.Load(true);
            Plugin.all.Add(plugin);
            Plugin.core.Add(plugin);
        }
    }

    // This class is just kept around for backwards compatibility    
    //   Plugin used to be completely abstract, with Plugin_Simple having virtual methods
    //   However this is now obsolete as the virtual methods were moved into Plugin
    [Obsolete("Derive from Plugin instead")]
    public abstract class Plugin_Simple : Plugin 
    {
        public override void Load(bool auto)
        {
            load(auto);
        }
    /// <summary> Hooks into events and initalises states/resources etc </summary>
        /// <param name="auto"> True if plugin is being automatically loaded (e.g. on server startup), false if manually. </param>
        public abstract void load(bool auto);
        
        /// <summary> Unhooks from events and disposes of state/resources etc </summary>
        /// <param name="auto"> True if plugin is being auto unloaded (e.g. on server shutdown), false if manually. </param>
        public abstract void unload(bool auto);
        public override void Unload(bool auto)
        {
            unload(auto);
        }
        
        /// <summary> Called when a player does /Help on the plugin. Typically tells the player what this plugin is about. </summary>
        /// <param name="p"> Player who is doing /Help. </param>
        public override void Help(Player p)
        {
            help(p);
        }
        public virtual void help(Player p) {
            p.Message("No help is available for this plugin.");
        }
        
        /// <summary> Name of the plugin. </summary>
        public override string name { get { return Name; } }
        public abstract string Name { get; }
        /// <summary> Oldest version of MCGalaxy this plugin is compatible with. </summary>
        public override string MCGalaxy_Version { get { return Version; } }
        public abstract string Version { get; }
        /// <summary> Version of this plugin. </summary>
        public override int build { get { return Build; } }
        public virtual int Build { get { return 0; } }
        /// <summary> Message to display once this plugin is loaded. </summary>
        public override string welcome { get { return Welcome; } }
        public virtual string Welcome { get { return ""; } }
        /// <summary> The creator/author of this plugin. (Your name) </summary>
        public override string creator { get { return Creator; } }
        public virtual string Creator { get { return ""; } }
        /// <summary> Whether or not to auto load this plugin on server startup. </summary>
        public override bool LoadAtStartup { get { return loadAtStartup; } }
        public virtual bool loadAtStartup { get { return true; } }
    }
}

