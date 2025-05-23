/*
    Copyright 2011 MCForge
    
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
using MCGalaxy.Scripting;

namespace MCGalaxy.Commands.Scripting
{
    public sealed class CmdCompile : Command2 {        
        public override string name { get { return "Compile"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Nobody; } }
        public override bool MessageBlockRestricted { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces();
            if (!Formatter.CheckFilenameOnly(p, args[0])) return;

            string language    = args.Length > 1 ? args[1] : "";
            ICompiler compiler = ICompiler.Lookup(language, p);
            if (compiler == null) return;
 
            // either "source" or "source1,source2,source3"
            string[] paths = args[0].SplitComma();
            string dstPath = IScripting.CommandPath(paths[0]);
            
            for (int i = 0; i < paths.Length; i++) {
                 paths[i] = compiler.CommandPath(paths[i]);
            }
            compiler.Compile(p, "Command", paths, dstPath);
        }

        public override void Help(Player p) {
            p.Message("&T/Compile [class name]");
            p.Message("&HCompiles a command class file into a DLL.");
            p.Message("&T/Compile [class name] vb");
            p.Message("&HCompiles a command class (written in visual basic) file into a DLL.");
            p.Message("&H  class name: &9Cmd&e<class name>&9.cs");
        }
    }
}
