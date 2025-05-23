﻿using System;
using System.IO;
using System.Drawing;
using MCGalaxy;
using MCGalaxy.Blocks;
using MCGalaxy.Events.PlayerEvents;

using MCGalaxy.Tasks;
using MCGalaxy.DB;
namespace NotAwesomeSurvival
{

    public static class NasBlockChange
    {
        public static Scheduler breakScheduler;
        public static Scheduler repeaterScheduler;
        public static Scheduler fishingScheduler;
        public static Color[] blockColors = new Color[Block.MaxRaw + 1];
        public const string terrainImageName = "terrain.png";

        public static bool Setup()
        {
            if (File.Exists("plugins/" + terrainImageName))
            {
                File.Move("plugins/" + terrainImageName, Nas.Path + terrainImageName);
            }
            if (!File.Exists(Nas.Path + terrainImageName))
            {
                Player.Console.Message("Could not locate {0} (needed for block particle colors)", terrainImageName);
                return false;
            }

            if (breakScheduler == null) breakScheduler = new Scheduler("BlockBreakScheduler");
            if (repeaterScheduler == null) repeaterScheduler = new Scheduler("RepeaterScheduler");
            if (fishingScheduler == null) fishingScheduler = new Scheduler("FishingScheduler");
            Bitmap terrain;
            terrain = new Bitmap(Nas.Path + terrainImageName);
            terrain = new Bitmap(terrain, terrain.Width / 16, terrain.Height / 16);
            //terrain.Save("plugins/nas/smolTerrain.png", System.Drawing.Imaging.ImageFormat.Png);

            for (ushort blockID = 0; blockID <= Block.MaxRaw; blockID++)
            {
                BlockDefinition def = BlockDefinition.GlobalDefs[Block.FromRaw(blockID)];

                if (def == null && blockID < Block.CPE_COUNT) { def = DefaultSet.MakeCustomBlock(Block.FromRaw(blockID)); }
                if (def == null)
                {
                    blockColors[blockID] = Color.White; continue;
                }
                int x = def.BackTex % 16;
                int y = def.BackTex / 16;
                blockColors[blockID] = terrain.GetPixel(x, y);

                //Logger.Log(LogType.Debug, "success color block "+blockID+".");
            }
            terrain.Dispose();
            return true;
        }

        public const string ClickableBlocksKey = "__clickableBlocks_";
        public const string LastClickedCoordsKey = ClickableBlocksKey + "lastClickedCoords";
        public const string BreakAmountKey = ClickableBlocksKey + "breakAmount";
        public const string BreakIDKey = ClickableBlocksKey + "breakID";
        public const byte BreakEffectIDcount = 6;
        public static byte BreakEffectID = 255;
        public const byte BreakMeterID = byte.MaxValue - BreakEffectIDcount;
        public const int BreakMeterSpawnDelay = 100;

        public static object breakIDLocker = new object();

        public static string LastClickedCoords(Player p)
        {
            return p.Extras.GetString(LastClickedCoordsKey, "-1 -1 -1");
        }
        public static void SetLastClickedCoords(Player p, ushort x, ushort y, ushort z)
        {
            p.Extras[LastClickedCoordsKey] = x + " " + y + " " + z;
        }
        public static int BreakAmount(Player p)
        {
            return p.Extras.GetInt(BreakAmountKey, 0);
        }
        public static void SetBreakAmount(Player p, int amount)
        {
            p.Extras[BreakAmountKey] = amount;
        }

        public static byte GetBreakID()
        {
            lock (breakIDLocker)
            {
                return BreakEffectID;
            }
        }
        public static void SetBreakID(byte value)
        {
            lock (breakIDLocker)
            {
                if (value <= byte.MaxValue - BreakEffectIDcount) { value = 255; }
                BreakEffectID = value;
            }
        }

        public static void BreakBlock(NasPlayer np, ushort x, ushort y, ushort z, ushort serverushort, NasBlock nasBlock)
        {
            if (np.nl == null) { return; }
            ushort here = np.p.level.GetBlock(x, y, z);
            if (here != serverushort) { return; } //don't let them break it if the block changed since we've started

            //If there's a container and it's not empty or locked by someone else, it can't be broken
            //COPY PASTED IN 2 PLACES
            if (nasBlock.container != null &&
                np.nl.blockEntities.ContainsKey(x + " " + y + " " + z) &&
                (np.nl.blockEntities[x + " " + y + " " + z].drop != null || !np.nl.blockEntities[x + " " + y + " " + z].CanAccess(np.p))
               )
            {
                return;
            }

            if (np.isInserting)
            {
                np.p.Message("&ePlease insert items into the container before breaking blocks."); 
                return;
            }

            if (nasBlock.parentID != 0)
            {
                Drop drop = nasBlock.dropHandler(np, nasBlock.parentID);
                np.inventory.GetDrop(drop);
                Random r = new Random();
                np.GiveExp(r.Next(nasBlock.expGivenMin, nasBlock.expGivenMax + 1));

            }
            else
            {
                np.p.Message("Why the hell are you trying to get {0}? It's not even a real block..",
                          Block.GetName(np.p, serverushort)
                         );
            }

            if (nasBlock.existAction != null)
            {
                nasBlock.existAction(np, nasBlock, false, x, y, z);
            }
            np.p.level.BlockDB.Cache.Add(np.p, x, y, z, BlockDBFlags.ManualPlace, here, Block.Air);
            np.nl.SetBlock(x, y, z, Block.Air);

            foreach (Player pl in np.p.level.players)
            {
                NassEffect.Define(pl, GetBreakID(), NassEffect.breakEffects[(int)nasBlock.material], blockColors[nasBlock.selfID]);
                NassEffect.Spawn(pl, GetBreakID(), NassEffect.breakEffects[(int)nasBlock.material], x, y, z, x, y, z);
            }
            SetBreakID((byte)(GetBreakID() - 1));
            np.justBrokeOrPlaced = true;

            if (!np.hasBeenSpawned)
            {
                np.p.Message("&chasBeenSpawned is &cfalse&S, this shouldn't happen if you didn't just die.");
                np.p.Message("&bPlease report to randomstrangers on Discord what you were doing before this happened");
            }
        }
        public static void CancelPlacedBlock(Player p, ushort x, ushort y, ushort z, NasPlayer np, ref bool cancel)
        {
            cancel = true;
            p.RevertBlock(x, y, z);
            if (!np.isDead) { Command.Find("tp").Use(p, "-precise ~ ~ ~"); }
        }




        public static void PlaceBlock(Player p, ushort x, ushort y, ushort z, ushort serverushort, bool placing, ref bool cancel)
        {
            if (p.level.Config.Deletable && p.level.Config.Buildable) { return; }
            if (!placing) { p.Message("&cYou shouldn't be allowed to do this."); cancel = true; return; }

            NasPlayer np = (NasPlayer)p.Extras[Nas.PlayerKey];
            ushort clientushort = p.ConvertBlock(serverushort);
            NasBlock nasBlock = NasBlock.Get(clientushort);

            if (nasBlock.parentID == 0)
            {
                p.Message("You can't place undefined blocks.");
                CancelPlacedBlock(p, x, y, z, np, ref cancel);
                return;
            }
            if ((nasBlock.selfID == 10 || nasBlock.selfID == 476 || nasBlock.selfID == 178) && p.level.name.Contains("0,0") && !p.level.name.Contains("nether"))
            {
                np.p.Message("&mCan't do that at 0,0.");
                CancelPlacedBlock(p, x, y, z, np, ref cancel);
                return;
            }

            if (np.nl.GetBlock(x, y, z + 1) == Block.FromRaw(703) || np.nl.GetBlock(x, y - 1, z) == Block.FromRaw(703))
            {
                np.p.Message("&mCan't obstruct a bed!");
                CancelPlacedBlock(p, x, y, z, np, ref cancel);
                return;
            }

            if (nasBlock.selfID == 703 && (np.nl.GetBlock(x, y, z - 1) != Block.Air || np.nl.GetBlock(x, y + 1, z) != Block.Air))
            {
                np.p.Message("&mCan't place a bed in an obstructed location!");
                CancelPlacedBlock(p, x, y, z, np, ref cancel);
                return;
            }

            int amount = np.inventory.GetAmount(nasBlock.parentID);
            if (amount < 1)
            {
                p.Message("&cYou don't have any {0}.", nasBlock.GetName(p));
                CancelPlacedBlock(p, x, y, z, np, ref cancel);
                return;
            }
            if (amount < nasBlock.resourceCost)
            {
                p.Message("&cYou need at least {0} {1} to place {2}.",
                          nasBlock.resourceCost, nasBlock.GetName(p), nasBlock.GetName(p, clientushort));
                CancelPlacedBlock(p, x, y, z, np, ref cancel);
                return;
            }


            np.inventory.SetAmount(nasBlock.parentID, -nasBlock.resourceCost);
            np.justBrokeOrPlaced = true;
        }

        public static void OnBlockChanged(Player p, ushort x, ushort y, ushort z, ChangeResult result)
        {
            if (p.level.Config.Deletable && p.level.Config.Buildable) { return; }
            NasPlayer np = (NasPlayer)p.Extras[Nas.PlayerKey];
            NasLevel nl;
            if (!NasLevel.all.ContainsKey(np.p.level.name)) return;
            else
            {
                nl = NasLevel.all[np.p.level.name];
            }
            NasBlock nasBlock = NasBlock.blocksIndexedByServerushort[p.level.GetBlock(x, y, z)];
            nasBlock.existAction?.Invoke(np, nasBlock, true, x, y, z);
            nl?.SimulateSetBlock(x, y, z);
        }

        static void BreakTask(SchedulerTask task)
        {
            BreakInfo breakInfo = (BreakInfo)task.State;
            NasPlayer np = breakInfo.np;
            NasBlock nasBlock = breakInfo.nasBlock;
            bool coordsMatch =
                np.breakX == breakInfo.x &&
                np.breakY == breakInfo.y &&
                np.breakZ == breakInfo.z;

            if (coordsMatch &&
                np.breakAttempt == breakInfo.breakAttempt &&
                np.inventory.HeldItem == breakInfo.toolUsed
               )
            {
                if ((np.inventory.GetAmount(696) >= 5) && nasBlock.selfID == 696) { np.p.Message("&mYou have too many lava barrels!"); return; }
                BreakBlock(np, breakInfo.x, breakInfo.y, breakInfo.z, breakInfo.serverushort, nasBlock);

                double toolDamageChance = 1.0 / (breakInfo.toolUsed.enchant("Unbreaking") + 1);
                Random r = new Random();

                if (r.NextDouble() < toolDamageChance && breakInfo.toolUsed.TakeDamage(nasBlock.damageDoneToTool))
                {
                    np.inventory.BreakItem(ref breakInfo.toolUsed);
                }
                np.inventory.UpdateItemDisplay();

                np.lastLeftClickReleaseDate = DateTime.UtcNow;
                np.ResetBreaking();
                return;
            }
        }
        public static void MeterTask(SchedulerTask task)
        {
            MeterInfo info = (MeterInfo)task.State;
            Player p = info.p;
            int millisecs = info.milliseconds;
            millisecs -= BreakMeterSpawnDelay;
            NassEffect.Define(p, BreakMeterID, NassEffect.breakMeter, Color.White, (float)(millisecs / 1000.0f));
            NassEffect.Spawn(p, BreakMeterID, NassEffect.breakMeter, info.x, info.y, info.z, info.x, info.y, info.z);
        }

        public class BreakInfo
        {
            public NasPlayer np;
            public ushort x, y, z;
            public ushort serverushort;
            public NasBlock nasBlock;
            public int breakAttempt;
            public Item toolUsed;
        }
        public class RepeaterInfo
        {
            public NasLevel nl;
            public int x, y, z;
            public NasBlock.Entity b, strength1;
            public int type;
            public int direction;
            public ushort oldBlock;

        }
        public class FishingInfo
        {
            public Player p, who;

        }

        public class InvInfo
        {
            public NasPlayer np;
            public bool inv;

        }

        public class MeterInfo
        {
            public Player p;
            public int milliseconds;
            public float x, y, z;
        }

        public static void HandleLeftClick (Player p, MouseButton button, 
            MouseAction action, ushort yaw, ushort pitch, byte entity, 
            ushort x, ushort y, ushort z, TargetBlockFace face)
        {
            if (!p.agreed) { p.Message("You need to read and agree to the &b/rules&S to play"); return; }

            NasPlayer np = (NasPlayer)p.Extras[Nas.PlayerKey];

            if (action == MouseAction.Released)
            {
                np.ResetBreaking();
                np.lastAirClickDate = null;
                np.lastLeftClickReleaseDate = DateTime.UtcNow;
                NassEffect.UndefineEffect(p, BreakMeterID);
            }
            if (action == MouseAction.Pressed)
            {
                if (x == ushort.MaxValue ||
                    y == ushort.MaxValue ||
                    z == ushort.MaxValue)
                {
                    //p.Message("reset breaking since you clicked out of bounds");
                    np.ResetBreaking();
                    NassEffect.UndefineEffect(p, BreakMeterID);
                    return;
                }

                ushort serverushort = p.level.GetBlock(x, y, z);
                ushort clientushort = p.ConvertBlock(serverushort);
                NasBlock nasBlock = NasBlock.Get(clientushort);
                if (nasBlock.durability == int.MaxValue)
                {
                    //p.Message("This block can't be broken");
                    np.ResetBreaking();
                    NassEffect.UndefineEffect(p, BreakMeterID);
                    return;
                }

                //If there's a container and it's not empty or locked by someone else, it can't be broken
                //COPY PASTED IN 2 PLACES
                if (nasBlock.container != null &&
                    np.nl.blockEntities.ContainsKey(x + " " + y + " " + z) &&
                    (np.nl.blockEntities[x + " " + y + " " + z].drop != null || !np.nl.blockEntities[x + " " + y + " " + z].CanAccess(p))
                   )
                {
                    np.ResetBreaking();
                    NassEffect.UndefineEffect(p, BreakMeterID);
                    return;
                }

                Item heldItem = np.inventory.HeldItem;
                bool toolEffective = false;
                if (heldItem.prop.materialsEffectiveAgainst != null)
                {
                    foreach (NasBlock.Material mat in heldItem.prop.materialsEffectiveAgainst)
                    {
                        //p.Message("heldItem is {0}, it is effective against {1} and this block is {2}",
                        //         heldItem.name, mat, nasBlock.material);
                        if (nasBlock.material == mat)
                        {
                            toolEffective = true; 
                            break;
                        }
                    }
                }
                bool canBreakBlock = heldItem.prop.tier >= nasBlock.tierOfToolNeededToBreak && toolEffective;
                if (nasBlock.tierOfToolNeededToBreak <= 0) { canBreakBlock = true; }
                if (!canBreakBlock)
                {
                    //p.Message("This block is too strong for your current tool.");
                    np.ResetBreaking();
                    NassEffect.UndefineEffect(p, BreakMeterID);
                    return;
                }


                if (serverushort == Block.Air)
                {
                    if (np.lastAirClickDate == null)
                    {
                        //p.Message("Whacked air");
                        np.lastAirClickDate = DateTime.UtcNow;
                    }

                    np.ResetBreaking();
                    NassEffect.UndefineEffect(p, BreakMeterID);
                    return;
                }
                if (np.breakX == x && np.breakY == y && np.breakZ == z)
                {
                    //p.Message("Not starting another break on this block because you already are breaking it");
                    return;
                }

                NassEffect.UndefineEffect(p, BreakMeterID);


                np.breakX = x;
                np.breakY = y;
                np.breakZ = z;

                np.breakAttempt++;


                //initial calculation
                const int swingTime = 260;
                int millisecs = (nasBlock.durability * swingTime) - swingTime;
                if (toolEffective)
                {
                    float multiplier = 1 - heldItem.prop.percentageOfTimeSaved;
                    switch (heldItem.enchant("Efficiency"))
                    {
                        case 1:
                            multiplier *= 0.75f;
                            break;
                        case 2:
                            multiplier *= 0.55f;
                            break;
                        case 3:
                            multiplier *= 0.375f;
                            break;
                        case 4:
                            multiplier *= 0.25f;
                            break;
                        case 5:
                            multiplier *= 0.1875f;
                            break;
                    }
                    millisecs = (int)(millisecs * multiplier);
                }
                if (millisecs < 0) { millisecs = 0; }
                //millisecs = 0;
                TimeSpan breakTime = TimeSpan.FromMilliseconds(np.searchItem("helmet").enchant("Aqua Affinity") == 0 && np.holdingBreath ? millisecs * 8 : millisecs);
                //initial calculation

                //lag compensation
                if (np.lastAirClickDate != null)
                {
                    //p.Message("subtracting LastAirBreakTime from breakTime");
                    TimeSpan sub = DateTime.UtcNow.Subtract((DateTime)np.lastAirClickDate);
                    breakTime -= sub;
                    millisecs = (int)breakTime.TotalMilliseconds;
                }
                else
                {
                    TimeSpan timeSinceLastBlockBroken = DateTime.UtcNow.Subtract(np.lastLeftClickReleaseDate);
                    int ping = p.Ping.AveragePing();
                    if (timeSinceLastBlockBroken.TotalMilliseconds >= ping)
                    {
                        //p.Message("subtracting ping");
                        breakTime -= TimeSpan.FromMilliseconds(ping + (ping / 2));
                        millisecs = (int)breakTime.TotalMilliseconds;
                    }
                }
                np.lastAirClickDate = null;
                //lag compensation


                //Unk's Tunnel: 178.62.37.103:25570
                BreakInfo breakInfo = new BreakInfo();
                breakInfo.np = np;
                breakInfo.x = x;
                breakInfo.y = y;
                breakInfo.z = z;
                breakInfo.serverushort = serverushort;
                breakInfo.nasBlock = nasBlock;
                breakInfo.breakAttempt = np.breakAttempt;
                breakInfo.toolUsed = heldItem;

                SchedulerTask taskBreakBlock;
                taskBreakBlock = breakScheduler.QueueOnce(BreakTask, breakInfo, breakTime);



                MeterInfo meterInfo = new MeterInfo();
                meterInfo.p = p;
                meterInfo.milliseconds = millisecs;
                meterInfo.x = x;
                meterInfo.y = y;
                meterInfo.z = z;

                BlockDefinition def = BlockDefinition.GlobalDefs[Block.FromRaw(clientushort)];
                if (def == null && clientushort < Block.CPE_COUNT) { def = DefaultSet.MakeCustomBlock(Block.FromRaw(clientushort)); }
                if (def != null)
                {
                    if (face == TargetBlockFace.AwayX) { DoOffset(def.MaxX, true, ref meterInfo.x); }
                    if (face == TargetBlockFace.TowardsX) { DoOffset(def.MinX, false, ref meterInfo.x); }

                    if (face == TargetBlockFace.AwayY) { DoOffset(def.MaxZ, true, ref meterInfo.y); }
                    if (face == TargetBlockFace.TowardsY) { DoOffset(def.MinZ, false, ref meterInfo.y); }
                    //blockdefinition's Y and Z bounds are swapped around
                    if (face == TargetBlockFace.AwayZ) { DoOffset(def.MaxY, true, ref meterInfo.z); }
                    if (face == TargetBlockFace.TowardsZ) { DoOffset(def.MinY, false, ref meterInfo.z); }
                }

                SchedulerTask taskDisplayMeter;
                taskDisplayMeter = breakScheduler.QueueOnce(MeterTask, meterInfo, TimeSpan.FromMilliseconds(BreakMeterSpawnDelay));
                p.Extras["nas_taskDisplayMeter"] = taskDisplayMeter;
            }
        }
        public static void DoOffset(byte minOrMax, bool positive, ref float coord)
        {
            const float offset = 0.125f;

            if (positive)
            {
                coord -= 0.5f; //pull to minimum edge
                coord += (float)(minOrMax / 16.0f); //push to maximum edge
                coord += offset; //nudge out by offset
                return;
            }
            coord -= 0.5f; //pull to minimum edge
            coord += (float)(minOrMax / 16.0f); //push to minimum edge
            coord -= offset; //nudge out by offset
        }

    } //class NasBlockChange

}
