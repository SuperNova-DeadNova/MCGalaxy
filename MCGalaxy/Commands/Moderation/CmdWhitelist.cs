﻿/*
    Copyright 2010 MCLawl Team - Written by Valek (Modified for use with MCGalaxy)
 
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
namespace MCGalaxy.Commands.Moderation
{
    public sealed class CmdWhitelist : Command2 {
        public override string name { get { return "Whitelist"; } }
        public override string shortcut { get { return "w"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data) {
            if (!Server.Config.WhitelistedOnly) { p.Message("Whitelist is not enabled."); return; }
            if (message.Length == 0) { List(p, ""); return; }
            string[] args = message.SplitSpaces();
            string cmd = args[0];
            
            if (cmd.CaselessEq("add")) {
                if (args.Length < 2) { Help(p); return; }
                Add(p, args[1]);
            } else if (IsDeleteCommand(cmd)) {
                if (args.Length < 2) { Help(p); return; }
                Remove(p, args[1]);
            } else if (IsListCommand(cmd)) {
                string modifier = args.Length > 1 ? args[1] : "";
                List(p, modifier);
            } else if (args.Length == 1) {
                Add(p, cmd);
            } else {
                Help(p);
            }
        }
        
        static void Add(Player p, string player) {
            if (!Server.whiteList.Add(player)) {
                p.Message("{0} &Sis already on the whitelist!", p.FormatNick(player));
            } else {
                Chat.MessageFromOps(p, "λNICK &Sadded &f" + player + " &Sto the whitelist.");
                Server.whiteList.Save();
                Logger.Log(LogType.UserActivity, "WHITELIST: Added " + player);
            }
        }
        
        static void Remove(Player p, string player) {
            if (!Server.whiteList.Remove(player)) {
                p.Message("{0} &Sis not on the whitelist!", p.FormatNick(player));
            } else {
                Server.whiteList.Save();
                Chat.MessageFromOps(p, "λNICK &Sremoved &f" + player + " &Sfrom the whitelist.");
                Logger.Log(LogType.UserActivity, "WHITELIST: Removed " + player);
            }
        }
        
        static void List(Player p, string modifier) {
            Server.whiteList.Output(p, "whitelisted players", "Whitelist list", modifier);
        }

        public override void Help(Player p) {
            p.Message("&T/Whitelist add/del [player]");
            p.Message("&HAdds or removes [player] from the whitelist.");
            p.Message("&T/Whitelist list");
            p.Message("&HLists all players who are on the whitelist.");
        }
    }
}
