﻿// Copyright 2014-2017 ClassicalSharp | Licensed under BSD-3
// Based on: https://github.com/UnknownShadow200/ClassiCube/wiki/Minecraft-Classic-map-generation-algorithm
using System;
using MCGalaxy;

namespace ClassicalSharp.Generator
{

    public sealed partial class ClassicGenerator {
        
        int waterLevel, oneY, Width, Length, Height;
        byte[] blocks;
        short[] heightmap;
        JavaRandom rnd;
        int minHeight;
        string CurrentState;
        
        const string defHelp = "&HSeed affects how terrain is generated. If seed is the same, the generated level will be the same.";
        public static void RegisterGenerators() {                                                
            MCGalaxy.Generator.MapGen.Register("Classic", MCGalaxy.Generator.GenType.Advanced, Gen, defHelp);
        }
        
        static bool Gen(Player p, Level lvl, string seed) {      
            int seed_ = MCGalaxy.Generator.MapGen.MakeInt(seed);
            new ClassicGenerator().Generate(lvl, seed_);
            return true;
        }
        
        public byte[] Generate(Level lvl, int seed) {
            blocks = lvl.blocks;
            Width  = lvl.Width;
            Height = lvl.Height;
            Length = lvl.Length;
            
            rnd = new JavaRandom(seed);
            oneY = Width * Length;
            waterLevel = Height / 2;
            minHeight  = Height;
            
            CreateHeightmap();
            CreateStrata();
            CarveCaves();
            CarveOreVeins(0.9f, "coal ore", Block.CoalOre);
            CarveOreVeins(0.7f, "iron ore", Block.IronOre);
            CarveOreVeins(0.5f, "gold ore", Block.GoldOre);
            
            FloodFillWaterBorders();
            FloodFillWater();
            FloodFillLava();

            CreateSurfaceLayer();
            PlantFlowers();
            PlantMushrooms();
            PlantTrees();
            return blocks;
        }
        
        void CreateHeightmap() {
            CombinedNoise n1 = new CombinedNoise(
                new OctaveNoise(8, rnd), new OctaveNoise(8, rnd));
            CombinedNoise n2 = new CombinedNoise(
                new OctaveNoise(8, rnd), new OctaveNoise(8, rnd));
            OctaveNoise n3 = new OctaveNoise(6, rnd);
            int index = 0;
            short[] hMap = new short[Width * Length];
            CurrentState = "Building heightmap";
            
            for (int z = 0; z < Length; z++) {
                for (int x = 0; x < Width; x++) {
                    double hLow = n1.Compute(x * 1.3f, z * 1.3f) / 6 - 4, height = hLow;
                    
                    if (n3.Compute(x, z) <= 0) {
                        double hHigh = n2.Compute(x * 1.3f, z * 1.3f) / 5 + 6;
                        height = Math.Max(hLow, hHigh);
                    }
                    
                    height *= 0.5;
                    if (height < 0) height *= 0.8f;
                    
                    int adjHeight = (int)(height + waterLevel);
                    minHeight = adjHeight < minHeight ? adjHeight : minHeight;
                    hMap[index++] = (short)adjHeight;
                }
            }
            heightmap = hMap;
        }
        
        void CreateStrata() {
            OctaveNoise n = new OctaveNoise(8, rnd);
            CurrentState = "Creating strata";            
            int hMapIndex = 0, maxY = Height - 1, mapIndex = 0;
            // Try to bulk fill bottom of the map if possible
            int minStoneY = CreateStrataFast();

            for (int z = 0; z < Length; z++) {
                for (int x = 0; x < Width; x++) {
                    int dirtThickness = (int)(n.Compute(x, z) / 24 - 4);
                    int dirtHeight = heightmap[hMapIndex++];
                    int stoneHeight = dirtHeight + dirtThickness;    
                    
                    stoneHeight = Math.Min(stoneHeight, maxY);
                    dirtHeight  = Math.Min(dirtHeight,  maxY);
                    
                    mapIndex = minStoneY * oneY + z * Width + x;
                    for (int y = minStoneY; y <= stoneHeight; y++) {
                        blocks[mapIndex] = Block.Stone; mapIndex += oneY;
                    }
                    
                    stoneHeight = Math.Max(stoneHeight, 0);
                    mapIndex = (stoneHeight + 1) * oneY + z * Width + x;
                    for (int y = stoneHeight + 1; y <= dirtHeight; y++) {
                        blocks[mapIndex] = Block.Dirt; mapIndex += oneY;
                    }
                }
            }
        }
        
        int CreateStrataFast() {
            // Make lava layer at bottom
            int mapIndex = 0;
            for (int z = 0; z < Length; z++)
                for (int x = 0; x < Width; x++)
            {
                blocks[mapIndex++] = Block.Lava;
            }
            
            // Invariant: the lowest value dirtThickness can possible be is -14
            int stoneHeight = minHeight - 14;
            if (stoneHeight <= 0) return 1; // no layer is fully stone
            
            // We can quickly fill in bottom solid layers
            for (int y = 1; y <= stoneHeight; y++)
                for (int z = 0; z < Length; z++)
                    for (int x = 0; x < Width; x++)
            {
                blocks[mapIndex++] = Block.Stone;
            }
            return stoneHeight;
        }
        
        void CarveCaves() {
            int cavesCount = blocks.Length / 8192;
            CurrentState = "Carving caves";
            
            for (int i = 0; i < cavesCount; i++) {
                double caveX = rnd.Next(Width);
                double caveY = rnd.Next(Height);
                double caveZ = rnd.Next(Length);
                
                int caveLen  = (int)(rnd.NextFloat() * rnd.NextFloat() * 200);
                double theta = rnd.NextFloat() * 2 * Math.PI, deltaTheta = 0;
                double phi   = rnd.NextFloat() * 2 * Math.PI, deltaPhi = 0;
                double caveRadius = rnd.NextFloat() * rnd.NextFloat();
                
                for (int j = 0; j < caveLen; j++) {
                    caveX += Math.Sin(theta) * Math.Cos(phi);
                    caveZ += Math.Cos(theta) * Math.Cos(phi);
                    caveY += Math.Sin(phi);
                    
                    theta = theta + deltaTheta * 0.2;
                    deltaTheta = deltaTheta * 0.9 + rnd.NextFloat() - rnd.NextFloat();
                    phi = phi / 2 + deltaPhi / 4;
                    deltaPhi = deltaPhi * 0.75 + rnd.NextFloat() - rnd.NextFloat();
                    if (rnd.NextFloat() < 0.25) continue;
                    
                    int cenX = (int)(caveX + (rnd.Next(4) - 2) * 0.2);
                    int cenY = (int)(caveY + (rnd.Next(4) - 2) * 0.2);
                    int cenZ = (int)(caveZ + (rnd.Next(4) - 2) * 0.2);
                    
                    double radius = (Height - cenY) / (double)Height;
                    radius = 1.2 + (radius * 3.5 + 1) * caveRadius;
                    radius = radius * Math.Sin(j * Math.PI / caveLen);
                    FillOblateSpheroid(cenX, cenY, cenZ, (float)radius, Block.Air);
                }
            }
        }
        
        void CarveOreVeins(float abundance, string blockName, byte block) {
            int numVeins = (int)(blocks.Length * abundance / 16384);
            CurrentState = "Carving " + blockName;
            
            for (int i = 0; i < numVeins; i++) {
                double veinX = rnd.Next(Width);
                double veinY = rnd.Next(Height);
                double veinZ = rnd.Next(Length);
                
                int veinLen = (int)(rnd.NextFloat() * rnd.NextFloat() * 75 * abundance);
                double theta = rnd.NextFloat() * 2 * Math.PI, deltaTheta = 0;
                double phi = rnd.NextFloat() * 2 * Math.PI, deltaPhi = 0;
                
                for (int j = 0; j < veinLen; j++) {
                    veinX += Math.Sin(theta) * Math.Cos(phi);
                    veinZ += Math.Cos(theta) * Math.Cos(phi);
                    veinY += Math.Sin(phi);
                    
                    theta = deltaTheta * 0.2;
                    deltaTheta = deltaTheta * 0.9 + rnd.NextFloat() - rnd.NextFloat();
                    phi = phi / 2 + deltaPhi / 4;
                    deltaPhi = deltaPhi * 0.9 + rnd.NextFloat() - rnd.NextFloat();
                    
                    float radius = abundance * (float)Math.Sin(j * Math.PI / veinLen) + 1;
                    FillOblateSpheroid((int)veinX, (int)veinY, (int)veinZ, radius, block);
                }
            }
        }
        
        void FloodFillWaterBorders() {
            int waterY = waterLevel - 1;
            int index1 = (waterY * Length + 0) * Width + 0;
            int index2 = (waterY * Length + (Length - 1)) * Width + 0;
            CurrentState = "Flooding edge water";
            
            for (int x = 0; x < Width; x++) {
                FloodFill(index1, Block.Water);
                FloodFill(index2, Block.Water);
                index1++; index2++;
            }
            
            index1 = (waterY * Length + 0) * Width + 0;
            index2 = (waterY * Length + 0) * Width + (Width - 1);
            for (int z = 0; z < Length; z++) {
                FloodFill(index1, Block.Water);
                FloodFill(index2, Block.Water);
                index1 += Width; index2 += Width;
            }
        }
        
        void FloodFillWater() {
            int numSources = Width * Length / 800;
            CurrentState = "Flooding water";
            
            for (int i = 0; i < numSources; i++) {
                int x = rnd.Next(Width), z = rnd.Next(Length);
                int y = waterLevel - rnd.Next(1, 3);
                FloodFill((y * Length + z) * Width + x, Block.Water);
            }
        }
        
        void FloodFillLava() {
            int numSources = Width * Length / 20000;
            CurrentState = "Flooding lava";
            
            for (int i = 0; i < numSources; i++) {
                int x = rnd.Next(Width), z = rnd.Next(Length);
                int y = (int)((waterLevel - 3) * rnd.NextFloat() * rnd.NextFloat());
                FloodFill((y * Length + z) * Width + x, Block.Lava);
            }
        }
        
        void CreateSurfaceLayer() {
            OctaveNoise n1 = new OctaveNoise(8, rnd), n2 = new OctaveNoise(8, rnd);
            CurrentState = "Creating surface";
            // TODO: update heightmap
            
            int hMapIndex = 0;
            for (int z = 0; z < Length; z++) {
                for (int x = 0; x < Width; x++) {
                    int y = heightmap[hMapIndex++];
                    if (y < 0 || y >= Height) continue;
                    
                    int index = (y * Length + z) * Width + x;
                    byte blockAbove = y >= (Height - 1) ? Block.Air : blocks[index + oneY];
                    if (blockAbove == Block.Water && (n2.Compute(x, z) > 12)) {
                        blocks[index] = Block.Gravel;
                    } else if (blockAbove == Block.Air) {
                        blocks[index] = (y <= waterLevel && (n1.Compute(x, z) > 8)) ? Block.Sand : Block.Grass;
                    }
                }
            }
        }
        
        void PlantFlowers() {
            int numPatches = Width * Length / 3000;
            CurrentState = "Planting flowers";
            
            for (int i = 0; i < numPatches; i++) {
                byte type  = (byte)(Block.Dandelion + rnd.Next(2));
                int patchX = rnd.Next(Width), patchZ = rnd.Next(Length);
                for (int j = 0; j < 10; j++) {
                    int flowerX = patchX, flowerZ = patchZ;
                    for (int k = 0; k < 5; k++) {
                        flowerX += rnd.Next(6) - rnd.Next(6);
                        flowerZ += rnd.Next(6) - rnd.Next(6);
                        if (flowerX < 0 || flowerZ < 0 || flowerX >= Width || flowerZ >= Length)
                            continue;
                        
                        int flowerY = heightmap[flowerZ * Width + flowerX] + 1;
                        if (flowerY <= 0 || flowerY >= Height) continue;
                        
                        int index = (flowerY * Length + flowerZ) * Width + flowerX;
                        if (blocks[index] == Block.Air && blocks[index - oneY] == Block.Grass)
                            blocks[index] = type;
                    }
                }
            }
        }
        
        void PlantMushrooms() {
            int numPatches = blocks.Length / 2000;
            CurrentState = "Planting mushrooms";
            
            for (int i = 0; i < numPatches; i++) {
                byte type  = (byte)(Block.Mushroom + rnd.Next(2));
                int patchX = rnd.Next(Width);
                int patchY = rnd.Next(Height);
                int patchZ = rnd.Next(Length);
                
                for (int j = 0; j < 20; j++) {
                    int mushX = patchX, mushY = patchY, mushZ = patchZ;
                    for (int k = 0; k < 5; k++) {
                        mushX += rnd.Next(6) - rnd.Next(6);
                        mushZ += rnd.Next(6) - rnd.Next(6);
                        if (mushX < 0 || mushZ < 0 || mushX >= Width || mushZ >= Length)
                            continue;
                        int solidHeight = heightmap[mushZ * Width + mushX];
                        if (mushY >= (solidHeight - 1))
                            continue;
                        
                        int index = (mushY * Length + mushZ) * Width + mushX;
                        if (blocks[index] == Block.Air && blocks[index - oneY] == Block.Stone)
                            blocks[index] = type;
                    }
                }
            }
        }
        
        void PlantTrees() {
            int numPatches = Width * Length / 4000;
            CurrentState = "Planting trees";
            
            for (int i = 0; i < numPatches; i++) {
                int patchX = rnd.Next(Width), patchZ = rnd.Next(Length);
                
                for (int j = 0; j < 20; j++) {
                    int treeX = patchX, treeZ = patchZ;
                    for (int k = 0; k < 20; k++) {
                        treeX += rnd.Next(6) - rnd.Next(6);
                        treeZ += rnd.Next(6) - rnd.Next(6);
                        if (treeX < 0 || treeZ < 0 || treeX >= Width ||
                            treeZ >= Length || rnd.NextFloat() >= 0.25)
                            continue;
                        
                        int treeY = heightmap[treeZ * Width + treeX] + 1;
                        if (treeY >= Height) continue;
                        int treeHeight = 5 + rnd.Next(3);
                        
                        int index = (treeY * Length + treeZ) * Width + treeX;
                        byte blockUnder = treeY > 0 ? blocks[index - oneY] : Block.Air;
                        
                        if (blockUnder == Block.Grass && CanGrowTree(treeX, treeY, treeZ, treeHeight)) {
                            GrowTree(treeX, treeY, treeZ, treeHeight);
                        }
                    }
                }
            }
        }
        
        bool CanGrowTree(int treeX, int treeY, int treeZ, int treeHeight) {
            // check tree base
            int baseHeight = treeHeight - 4;
            for (int y = treeY; y < treeY + baseHeight; y++)
                for (int z = treeZ - 1; z <= treeZ + 1; z++)
                    for (int x = treeX - 1; x <= treeX + 1; x++)
            {
                if (x < 0 || y < 0 || z < 0 || x >= Width || y >= Height || z >= Length)
                    return false;
                int index = (y * Length + z) * Width + x;
                if (blocks[index] != 0) return false;
            }
            
            // and also check canopy
            for (int y = treeY + baseHeight; y < treeY + treeHeight; y++)
                for (int z = treeZ - 2; z <= treeZ + 2; z++)
                    for (int x = treeX - 2; x <= treeX + 2; x++)
            {
                if (x < 0 || y < 0 || z < 0 || x >= Width || y >= Height || z >= Length)
                    return false;
                int index = (y * Length + z) * Width + x;
                if (blocks[index] != 0) return false;
            }
            return true;
        }
        
        void GrowTree(int treeX, int treeY, int treeZ, int height) {
            int baseHeight = height - 4;
            int index = 0;
            
            // leaves bottom layer
            for (int y = treeY + baseHeight; y < treeY + baseHeight + 2; y++)
                for (int zz = -2; zz <= 2; zz++)
                    for (int xx = -2; xx <= 2; xx++)
            {
                int x = xx + treeX, z = zz + treeZ;
                index = (y * Length + z) * Width + x;
                
                if (Math.Abs(xx) == 2 && Math.Abs(zz) == 2) {
                    if (rnd.NextFloat() >= 0.5)
                        blocks[index] = Block.Leaves;
                } else {
                    blocks[index] = Block.Leaves;
                }
            }
            
            // leaves top layer
            int bottomY = treeY + baseHeight + 2;
            for (int y = treeY + baseHeight + 2; y < treeY + height; y++)
                for (int zz = -1; zz <= 1; zz++)
                    for (int xx = -1; xx <= 1; xx++)
            {
                int x = xx + treeX, z = zz + treeZ;
                index = (y * Length + z) * Width + x;

                if (xx == 0 || zz == 0) {
                    blocks[index] = Block.Leaves;
                } else if (y == bottomY && rnd.NextFloat() >= 0.5) {
                    blocks[index] = Block.Leaves;
                }
            }
            
            // then place trunk
            index = (treeY * Length + treeZ) * Width + treeX;
            for (int y = 0; y < height - 1; y++) {
                blocks[index] = Block.Log;
                index += oneY;
            }
        }
    }
}