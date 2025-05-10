﻿using MCGalaxy;
using MCGalaxy.Blocks;
using MCGalaxy.Network;
using MCGalaxy.Tasks;
using System;
using Newtonsoft.Json;
namespace NotAwesomeSurvival
{
    public partial class Inventory
    {
        public Player p;
        public int[] blocks = new int[Block.MaxRaw + 1];
        public Inventory(Player p) 
        { 
            this.p = p; 
        }
        public void SetPlayer(Player p) 
        { 
            this.p = p; 
        }
        public void Setup()
        {
            Player.Console.Message("setting up inventory");
            //hide all blocks
            for (ushort clientushort = 1; clientushort <= Block.MaxRaw; clientushort++)
            {
                p.Send(Packet.BlockPermission(clientushort, false, false, true));
                p.Send(Packet.SetInventoryOrder(clientushort, 0, true));
            }
            //unhide blocks you have access to
            for (ushort clientushort = 1; clientushort <= Block.MaxRaw; clientushort++)
            {
                if (GetAmount(clientushort) > 0)
                {
                    UnhideBlock(clientushort);
                }
            }
            SetupItems();
        }
        public void ClearHotbar()
        {
            for (byte i = 0; i <= 9; i++)
            {
                p.Send(Packet.SetHotbar(0, i, true));
            }
        }
        /// <summary>
        /// Returns a drop that contains the items the player was unable to pickup due to full inventory. If the drop is null, the player fit everything.
        /// </summary>
        public Drop GetDrop(Drop drop, bool showToNormalChat = false, bool overrideBool = false)
        {
            if (drop == null) 
            { 
                return null;
            }
            if (drop.exp > 0)
            {
                NasPlayer.GetNasPlayer(p).GiveExp(drop.exp);
                drop.exp = 0;
            }
            if (drop.blockStacks != null)
            {
                for (int i = 0; i < drop.blockStacks.Count; i++)
                {
                    BlockStack bs = drop.blockStacks[i];
                    SetAmount(bs.ID, bs.amount, false);
                    DisplayInfo info = new DisplayInfo();
                    info.inv = this;
                    info.nasBlock = NasBlock.Get(bs.ID);
                    info.amountChanged = bs.amount;
                    if (drop.blockStacks.Count == 1 || overrideBool)
                    {
                        info.showToNormalChat = showToNormalChat;
                    }
                    else
                    {
                        info.showToNormalChat = true;
                    }
                    SchedulerTask taskDisplayHeldBlock;
                    taskDisplayHeldBlock = Server.MainScheduler.QueueOnce(DisplayHeldBlockTask, info, TimeSpan.FromMilliseconds(i * 125));
                }
            }
            Drop leftovers = null;
            if (drop.items != null)
            {
                foreach (Item item in drop.items)
                {
                    if (!GetItem(item))
                    {
                        if (leftovers == null)
                        {
                            leftovers = new Drop(item);
                        }
                        else
                        {
                            leftovers.items.Add(item);
                        }
                    }
                }
                UpdateItemDisplay();
            }
            return leftovers;
        }
        public void SetAmount(ushort clientushort, int amount, bool displayChange = true, bool showToNormalChat = false)
        {
            //TODO threadsafe
            blocks[clientushort] += amount;
            if (displayChange)
            {
                NasBlock nb = NasBlock.Get(clientushort);
                DisplayHeldBlock(nb, amount, showToNormalChat);
            }
            if (blocks[clientushort] > 0)
            {
                //more than zero? unhide the block
                UnhideBlock(clientushort);
                return;
            }
            else
            {
                //0 or less? hide the block
                HideBlock(clientushort);
            }
        }
        public int GetAmount(ushort clientushort)
        {
            //TODO threadsafe
            return blocks[clientushort];
        }

        [JsonIgnore] public CpeMessageType whereHeldBlockIsDisplayed = CpeMessageType.BottomRight3;
        public void DisplayHeldBlock(NasBlock nasBlock, int amountChanged = 0, bool showToNormalChat = false)
        {
            string display = DisplayedBlockString(nasBlock);
            if (amountChanged > 0)
            {
                display = "&a+" + amountChanged + " &f" + display;
            }
            if (amountChanged < 0)
            {
                display = "&c" + amountChanged + " &f" + display;
            }
            if (showToNormalChat)
            {
                p.Message(display);
            }
            p.SendCpeMessage(whereHeldBlockIsDisplayed, display);
        }
        public string DisplayedBlockString(NasBlock nasBlock)
        {
            if (nasBlock.parentID == 0)
            {
                return "┤";
            }
            int amount = GetAmount(nasBlock.parentID);
            string hand = amount <= 0 ? "┤" : "╕¼";
            return "[" + amount + "] " + nasBlock.GetName(p) + " " + hand;
        }
        public class DisplayInfo
        {
            public Inventory inv;
            public NasBlock nasBlock;
            public int amountChanged;
            public bool showToNormalChat;
        }
        public static void DisplayHeldBlockTask(SchedulerTask task)
        {
            DisplayInfo info = (DisplayInfo)task.State;
            info.inv.DisplayHeldBlock(info.nasBlock, info.amountChanged, info.showToNormalChat);
        }
        public void HideBlock(ushort clientushort)
        {
            p.Send(Packet.BlockPermission(clientushort, false, false, true));
            p.Send(Packet.SetInventoryOrder(clientushort, 0, true));
            NasBlock nasBlock = NasBlock.blocks[clientushort];
            if (nasBlock.childIDs != null)
            {
                foreach (ushort childID in nasBlock.childIDs)
                {
                    p.Send(Packet.BlockPermission(childID, false, false, true));
                    p.Send(Packet.SetInventoryOrder(childID, 0, true));
                }
            }
        }
        public void UnhideBlock(ushort clientushort)
        {
            BlockDefinition def = BlockDefinition.GlobalDefs[Block.FromRaw(clientushort)];
            if (def == null && clientushort < Block.CPE_COUNT) 
            { 
                def = DefaultSet.MakeCustomBlock(Block.FromRaw(clientushort)); 
            }
            if (def == null) 
            { 
                return; 
            }
            p.Send(Packet.BlockPermission(clientushort, true, false, true));
            p.Send(Packet.SetInventoryOrder(clientushort, (def.InventoryOrder == -1) ? clientushort : (ushort)def.InventoryOrder, true));
            NasBlock nasBlock = NasBlock.blocks[clientushort];
            if (nasBlock.childIDs != null)
            {
                foreach (ushort childID in nasBlock.childIDs)
                {
                    def = BlockDefinition.GlobalDefs[Block.FromRaw(childID)];
                    if (def == null && childID < Block.CPE_COUNT) 
                    { 
                        def = DefaultSet.MakeCustomBlock(Block.FromRaw(childID)); 
                    }
                    if (def == null) 
                    { 
                        continue; 
                    }
                    p.Send(Packet.BlockPermission(childID, true, false, true));
                    p.Send(Packet.SetInventoryOrder(childID, (def.InventoryOrder == -1) ? childID : (ushort)def.InventoryOrder, true));
                }
            }
        }
    }
}