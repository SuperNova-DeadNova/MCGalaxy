﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using MCGalaxy;
using MCGalaxy.Tasks;

using Priority_Queue;
using NasBlockAction = System.Action<NotAwesomeSurvival.NasLevel, NotAwesomeSurvival.NasBlock, int, int, int>;
namespace NotAwesomeSurvival
{

    public partial class NasLevel
    {
        public int biome;
        public bool dungeons = false;
        public bool deepslateGenerated = true;
        public static Scheduler TickScheduler;
        public static TimeSpan tickDelay = TimeSpan.FromMilliseconds(100);
        public static Random r = new Random();
        public class BlockLocation
        {
            public int X, Y, Z;
            public BlockLocation() { }
            public BlockLocation(QueuedBlockUpdate qb)
            {
                X = qb.x; Y = qb.y; Z = qb.z;
            }
            public BlockLocation(int x, int y, int z)
            {
                X = x; Y = y; Z = z;
            }
        }
        public struct QueuedBlockUpdate
        {
            public int x, y, z;
            public NasBlock nb;
            public DateTime date;
            public NasBlockAction da;
        }

        [JsonIgnore] public static Dictionary<string, NasLevel> all = new Dictionary<string, NasLevel>();
        [JsonIgnore] public Level lvl;
        public ushort[,] heightmap;
        public ushort height;
        public List<BlockLocation> blocksThatMustBeDisturbed = new List<BlockLocation>();
        public Dictionary<string, NasBlock.Entity> blockEntities = new Dictionary<string, NasBlock.Entity>();
        [JsonIgnore] public SimplePriorityQueue<QueuedBlockUpdate, DateTime> tickQueue = new SimplePriorityQueue<QueuedBlockUpdate, DateTime>();
        [JsonIgnore] public SchedulerTask schedulerTask;

        public void BeginTickTask()
        {
            if (TickScheduler == null) TickScheduler = new Scheduler("NasLevelTickScheduler");

            Player.Console.Message("Re-disturbing {0} blocks.", blocksThatMustBeDisturbed.Count);
            foreach (BlockLocation blockLoc in blocksThatMustBeDisturbed)
            {
                DisturbBlock(blockLoc.X, blockLoc.Y, blockLoc.Z);
            }
            blocksThatMustBeDisturbed.Clear();
            schedulerTask = TickScheduler.QueueRepeat(TickLevelCallback, this, tickDelay);
        }
        // /newlvl eb_0,0 384 256 384 nasgen eef
        public void EndTickTask()
        {
            if (TickScheduler == null) TickScheduler = new Scheduler("NasLevelTickScheduler");
            TickScheduler.Cancel(schedulerTask);

            Player.Console.Message("Saving {0} blocks to re-disturb later.", tickQueue.Count);
            if (tickQueue.Count == 0) { return; }

            blocksThatMustBeDisturbed = new List<BlockLocation>();
            foreach (QueuedBlockUpdate qb in tickQueue)
            {
                BlockLocation blockLoc = new BlockLocation(qb);
                if (blocksThatMustBeDisturbed.Contains(blockLoc)) { continue; }
                blocksThatMustBeDisturbed.Add(blockLoc);
            }
            tickQueue.Clear();
        }
        public static void TickLevelCallback(SchedulerTask task)
        {
            NasLevel nl = (NasLevel)task.State;
            nl.Tick();
        }
        public void Tick()
        {
            if (tickQueue.Count < 1) { return; }
            int actions = 0;
            while (tickQueue.First.date < DateTime.UtcNow)
            {
                if (actions > 64)
                {
                    //Player.Console.Message("falling behind on ticks");
                    break;
                }
                QueuedBlockUpdate qb = tickQueue.First;
                if (NasBlock.blocksIndexedByServerushort[lvl.GetBlock((ushort)qb.x, (ushort)qb.y, (ushort)qb.z)].selfID == qb.nb.selfID)
                {
                    qb.da(this, qb.nb, qb.x, qb.y, qb.z);
                }
                tickQueue.Dequeue();
                actions++;
                if (tickQueue.Count < 1) { break; }
            }
        }

        public void SetBlock(int x, int y, int z, ushort serverushort, bool disturbDiagonals = false)
        {
            if (
                x >= lvl.Width ||
                x < 0 ||
                y >= lvl.Height ||
                y < 0 ||
                z >= lvl.Length ||
                z < 0 ||
                serverushort == 255
               )
            { return; }
            lvl.Blockchange((ushort)x, (ushort)y, (ushort)z, serverushort);
            DisturbBlocks(x, y, z, disturbDiagonals);
        }

        public void FastSetBlock(int x, int y, int z, ushort serverushort, bool disturbDiagonals = false)
        {
            lvl.Blockchange((ushort)x, (ushort)y, (ushort)z, serverushort);
            DisturbBlocks(x, y, z, disturbDiagonals);
        }

        public void SimulateSetBlock(int x, int y, int z, bool disturbDiagonals = false)
        {
            if (
                x >= lvl.Width ||
                x < 0 ||
                y >= lvl.Height ||
                y < 0 ||
                z >= lvl.Length ||
                z < 0
               )
            { return; }
            DisturbBlocks(x, y, z, disturbDiagonals);
        }
        public void DisturbBlocks(int x, int y, int z, bool diagonals = false)
        {
            if (diagonals)
            {
                for (int xOff = -1; xOff <= 1; xOff++)
                    for (int yOff = -1; yOff <= 1; yOff++)
                        for (int zOff = -1; zOff <= 1; zOff++)
                        {
                            DisturbBlock(x, y, z, xOff, yOff, zOff);
                        }
                return;
            }
            if (NasBlock.IsPartOfSet(observers, lvl.GetBlock((ushort)x, (ushort)y, (ushort)z)) == -1)
            {
                DisturbBlock(x, y, z);
            }
            DisturbBlock(x, y, z, 1, 0, 0);
            DisturbBlock(x, y, z, -1, 0, 0);

            DisturbBlock(x, y, z, 0, 1, 0);
            DisturbBlock(x, y, z, 0, -1, 0);

            DisturbBlock(x, y, z, 0, 0, 1);
            DisturbBlock(x, y, z, 0, 0, -1);
        }
        /// <summary>
        /// Call to make the nasBlock at this location queue its "whatHappensWhenDisturbed" function.
        /// </summary>
        /// 

        public ushort[] observers = {
            Block.FromRaw(415),
            Block.FromRaw(416),
            Block.FromRaw(417),
            Block.FromRaw(418),
            Block.FromRaw(419),
            Block.FromRaw(420),

        };

        public ushort[] repeatersOff = {
            Block.FromRaw(176),
            Block.FromRaw(177),
            Block.FromRaw(174),
            Block.FromRaw(175),
            Block.FromRaw(172),
            Block.FromRaw(173),

        };

        public ushort[] repeatersOn = {
            Block.FromRaw(617),
            Block.FromRaw(618),
            Block.FromRaw(615),
            Block.FromRaw(616),
            Block.FromRaw(613),
            Block.FromRaw(614),

        };

        public void DisturbBlock(int x, int y, int z, int changeX = 0, int changeY = 0, int changeZ = 0)
        {

            if (
                x + changeX >= lvl.Width ||
                x + changeX < 0 ||
                y + changeY >= lvl.Height ||
                y + changeY < 0 ||
                z + changeZ >= lvl.Length ||
                z + changeZ < 0
               )
            { return; }
            ushort block = lvl.FastGetBlock((ushort)(x + changeX), (ushort)(y + changeY), (ushort)(z + changeZ));
            int index = NasBlock.IsPartOfSet(observers, block);
            if (index == -1) { index = NasBlock.IsPartOfSet(repeatersOff, block); }
            if (index == -1) { index = NasBlock.IsPartOfSet(repeatersOn, block); }
            if (index != -1 && Math.Abs(changeX) + Math.Abs(changeY) + Math.Abs(changeZ) == 1)
            {
                bool cancel = true;
                if (index == 0 && changeZ == 1) { cancel = false; }
                if (index == 1 && changeX == -1) { cancel = false; }
                if (index == 2 && changeZ == -1) { cancel = false; }
                if (index == 3 && changeX == 1) { cancel = false; }
                if (index == 4 && changeY == -1) { cancel = false; }
                if (index == 5 && changeY == 1) { cancel = false; }
                if (cancel) { return; }
            }
            x += changeX;
            y += changeY;
            z += changeZ;

            NasBlock nb = NasBlock.blocksIndexedByServerushort[block];
            if (nb.disturbedAction == null) { return; }
            QueuedBlockUpdate qb = new QueuedBlockUpdate();
            qb.x = x;
            qb.y = y;
            qb.z = z;
            float seconds = (float)(r.NextDouble() * (nb.disturbDelayMax - nb.disturbDelayMin) + nb.disturbDelayMin);
            qb.date = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            qb.date = qb.date.Floor(tickDelay);
            qb.nb = nb;
            qb.da = nb.disturbedAction;
            //lvl.Message("queueing thing "+qb.date.ToString("hh:mm:ss.fff tt"));
            tickQueue.Enqueue(qb, qb.date);

            if (nb.instantAction == null) { return; }
            qb = new QueuedBlockUpdate();
            qb.x = x;
            qb.y = y;
            qb.z = z;
            qb.nb = nb;
            qb.date = DateTime.UtcNow;
            qb.date = qb.date.Floor(tickDelay);
            qb.da = nb.instantAction;
            tickQueue.Enqueue(qb, DateTime.UtcNow.Floor(tickDelay));
        }
        public ushort GetBlock(int x, int y, int z)
        {
            return lvl.GetBlock((ushort)x, (ushort)y, (ushort)z);
        }

    }

    public static class DateExtensions
    {
        public static DateTime Round(this DateTime date, TimeSpan span)
        {
            long ticks = (date.Ticks + (span.Ticks / 2) + 1) / span.Ticks;
            return new DateTime(ticks * span.Ticks);
        }
        public static DateTime Floor(this DateTime date, TimeSpan span)
        {
            long ticks = date.Ticks / span.Ticks;
            return new DateTime(ticks * span.Ticks);
        }
        public static DateTime Ceil(this DateTime date, TimeSpan span)
        {
            long ticks = (date.Ticks + span.Ticks - 1) / span.Ticks;
            return new DateTime(ticks * span.Ticks);
        }
    }

}
