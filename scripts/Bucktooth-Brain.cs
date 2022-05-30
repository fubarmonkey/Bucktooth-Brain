using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;

using SpaceEngineers.Game.ModAPI.Ingame;

using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace InGameScript {
    partial class Program : MyGridProgram {
        /* Program Starts Here */

        // For labeling LCD Panels and their respective systems for the HUD
        string LABEL_CARGO = "cargo-Bucktooth";
        string LABEL_POWER = "power-Bucktooth";
        string LABEL_DRILL = "drill-Bucktooth";

        // Columns for Horizontal alignment on LCD Panels
        float COLUMN_L = -200.0f;
        float COLUMN_1 = -150.0f;
        float COLUMN_2 = -75.0f;
        float COLUMN_3 = 0.0f;
        float COLUMN_4 = 75.0f;
        float COLUMN_5 = 150.0f;
        float COLUMN_R = 200.0f;

        float ROW_T = -225.0f;

        // LCD Panel Text Sizes
        float TEXT_HEADING_SIZE = 1.5f;
        float TEXT_LABEL_SIZE = 1.0f;
        float TEXT_PERCENT_SIZE = 0.75f;
        float TEXT_LIST_SIZE = 0.75f;
        
        // Dimensions for Graphs on LCD Panels
        float GRAPH_INNER_WIDTH = 50.0f;
        float GRAPH_INNER_HEIGHT = 300.0f;
        float GRAPH_PADDING = 5.0f;

        // Colors for Graph Percent Indicators
        Color INDICATOR_SAFE = new Color(0, 64, 0, 255);
        Color INDICATOR_CAUTION = new Color(64, 64, 0, 255);
        Color INDICATOR_WARNING = new Color(64, 32, 0, 255);
        Color INDICATOR_ALERT = new Color(64, 0, 0, 255);

        // Color for all Text on LCD Panels
        Color textColor = new Color(64, 64, 64, 255);

        // For use with the Graph dimensions when drawing LCD Panels
        float GRAPH_OUTER_WIDTH;
        float GRAPH_OUTER_HEIGHT;

        class Display {
            public IMyTerminalBlock terminalBlock;
            public IMyTextSurfaceProvider surfaceProvider;
            public string customData;

            public Display(IMyTextSurfaceProvider provider) {
                surfaceProvider = provider;
                terminalBlock = (IMyTerminalBlock)provider;
                customData = ((IMyTerminalBlock)provider).CustomData;
            }
        }

        List<Display> Displays = new List<Display>();

        public Program() {

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            // Calculated Width and Height of Graphs
            GRAPH_OUTER_WIDTH = GRAPH_INNER_WIDTH + GRAPH_PADDING;
            GRAPH_OUTER_HEIGHT = GRAPH_INNER_HEIGHT + GRAPH_PADDING;

            GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(null, provider => {
                if (provider.SurfaceCount > 0)
                    Displays.Add(new Display(provider));
                return false;
            });
        }

        public void Main(string argument, UpdateType updateSource) {
            OutputDisplays();

            Echo ("Run Time of Last Run: " + Runtime.LastRunTimeMs.ToString() + "ms");
        }

        public void OutputDisplays() {
            Vector2 centerPos = new Vector2();

            Displays.ForEach(e => {
                MySpriteDrawFrame frame = e.surfaceProvider.GetSurface(0).DrawFrame();

                centerPos = e.surfaceProvider.GetSurface(0).TextureSize * 0.5f;

                if (e.customData == LABEL_CARGO) {
                    ClearDrawSurface(e.surfaceProvider.GetSurface(0));
                    PowerDisplay(ref frame, centerPos);
                } else if (e.customData == LABEL_CARGO) {
                    ClearDrawSurface(e.surfaceProvider.GetSurface(0));
                    CargoDisplay(ref frame, centerPos);
                } else if (e.customData == LABEL_DRILL) {
                    ClearDrawSurface(e.surfaceProvider.GetSurface(0));
                    DrillDisplay(ref frame, centerPos);
                }

                frame.Dispose();
            });
        }

        public float GetBatteryLevels() {
            List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries, c => c.CustomData.Contains(LABEL_POWER));
            
            float storedPower = 0.0f;
            float maxPower = 0.0f;

            foreach (IMyBatteryBlock e in batteries) {
                storedPower += e.CurrentStoredPower;
                maxPower += e.MaxStoredPower;
            }
            
            float percentUsed = 100.0f * storedPower / maxPower;

            return percentUsed;
        }

        public float GetHydrogenLevels() {
            List<IMyTerminalBlock> h2Tanks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(h2Tanks, c => c.CustomData.Contains(LABEL_POWER));
            
            double storedH2 = 0.0f;
            int tankCount = 0;

            foreach (IMyGasTank e in h2Tanks) {
                storedH2 += e.FilledRatio;
                tankCount++;
            }
            
            double percentUsed = storedH2 / tankCount * 100.0d;

            return (float)percentUsed;
        }

        public float GetCargoVolumes() {
            List<IMyTerminalBlock> cargoContainers = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargoContainers, c => c.CustomData.Contains(LABEL_CARGO));
            
            float usedVolume = 0.0f;
            float maxVolume = 0.0f;

            cargoContainers.ForEach(e => {
                usedVolume += (float)e.GetInventory(0).CurrentVolume;
                maxVolume += (float)e.GetInventory(0).MaxVolume;
            });

            float percentUsed = 100.0f * usedVolume / maxVolume;

            return percentUsed;
        }

        public Dictionary<string, int> GetCargoItems() {
            List<IMyTerminalBlock> cargoContainers = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargoContainers, c => c.CustomData.Contains(LABEL_CARGO));

            var items = new List<MyInventoryItem>();

            // List of Ores for the Cargo Manifest
            Dictionary<string, int> oreList = new Dictionary<string, int>{
                {"Ice", 0},
                {"Stone", 0},
                {"Iron", 0},
                {"Nickel", 0},
                {"Silicon", 0},
                {"Cobalt", 0},
                {"Magnesium", 0},
                {"Silver", 0},
                {"Gold", 0},
                {"Uranium", 0},
                {"Platinum", 0},
                {"Misc", 0}
            };

            cargoContainers.ForEach(e => {
                e.GetInventory().GetItems(items);
            });
            
            items.ForEach(e => {
                string item = e.Type.ToString().Substring(20);

                if (oreList.ContainsKey(item))
                    oreList[item] += e.Amount.ToIntSafe();
                else
                    oreList["Misc"] += e.Amount.ToIntSafe();
            });

            return oreList;
        }

        public bool GetDrillStatus() {
            List<IMyTerminalBlock> drills = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drills, c => c.CustomData.Contains(LABEL_DRILL));

            bool drillsOn = true;

            drills.ForEach(e => {
                if (!e.IsWorking) {
                    drillsOn = false;
                }
            });

            return drillsOn;
        }

        public float GetDrillProgress() {
            List<IMyTerminalBlock> drillPistons = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(drillPistons, c => c.CustomData.Contains(LABEL_DRILL));

            float currentLength = 0.0f;
            float maxLength = 0.0f;

            foreach (IMyPistonBase piston in drillPistons) {
                currentLength += piston.CurrentPosition;
                maxLength += piston.HighestPosition;
            }

            return 100.0f * currentLength / maxLength;
        }

        public bool GetJunkToggle() {
            List<IMyTerminalBlock> junkSorter = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyConveyorSorter>(junkSorter, c => c.CustomData.Contains(LABEL_DRILL));

            bool junkOn = true;

            junkSorter.ForEach(e => {
                if (!e.IsWorking)
                    junkOn = false;
            });
            
            return junkOn;
        }

        public bool GetHullLockState() {
            List<IMyTerminalBlock> hullBrakes = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyLandingGear>(hullBrakes, c => c.CustomData.Contains(LABEL_DRILL));
            
            bool lockOn = false;

            foreach (IMyLandingGear e in hullBrakes) {
                if (e.IsLocked) 
                    lockOn = true;
            }

            return lockOn;
        }

        public void PowerDisplay(ref MySpriteDrawFrame frame, Vector2 centerPos) {
            float batteryPercent = GetBatteryLevels();
            float batteryLevel = batteryPercent * 3.0f;
            float h2Percent = GetHydrogenLevels();
            float h2Level = h2Percent * 3.0f;
            Color batteryColor = INDICATOR_ALERT;
            Color h2Color = INDICATOR_ALERT;

            if (batteryPercent >= 25.0f && batteryPercent < 50.0f)
                batteryColor = INDICATOR_WARNING;
            else if (batteryPercent >= 50.0f && batteryPercent < 75.0f)
                batteryColor = INDICATOR_CAUTION;
            else if (batteryPercent >= 75.0f)
                batteryColor = INDICATOR_SAFE;
            
            if (h2Percent >= 25.0f && h2Percent < 50.0f)
                h2Color = INDICATOR_WARNING;
            else if (h2Percent >= 50.0f && h2Percent < 75.0f)
                h2Color = INDICATOR_CAUTION;
            else if (h2Percent >= 75.0f)
                h2Color = INDICATOR_SAFE;

            DrawHeading(ref frame, centerPos, "Reserves");

            DrawGraph(ref frame, centerPos, COLUMN_4, batteryColor, batteryPercent, "", "IconEnergy");
            DrawGraph(ref frame, centerPos, COLUMN_5, h2Color, h2Percent, "", "IconHydrogen");
        }

        public void CargoDisplay(ref MySpriteDrawFrame frame, Vector2 centerPos) {
            float percent = GetCargoVolumes();
            float cargoSize = percent * 3.0f;

            Color color = INDICATOR_SAFE;

            Dictionary<string, int> oreList = GetCargoItems();

            string oreLabels = "";
            string oreAmounts = "";
            foreach (KeyValuePair<string, int> ore in oreList) {
                oreLabels += ore.Key + ":\n";
                oreAmounts += ore.Value.ToString() + " kg\n";
            }

            if (percent >= 25.0f && percent < 50.0f)
                color = INDICATOR_CAUTION;
            else if (percent >= 50.0f && percent < 75.0f)
                color = INDICATOR_WARNING;
            else if (percent >= 75.0f)
                color = INDICATOR_ALERT;

            DrawHeading(ref frame, centerPos, "Cargo");

            DrawGraph(ref frame, centerPos, COLUMN_1, color, percent, "", "Textures\\FactionLogo\\Builders\\BuilderIcon_1.dds");

            DrawList(ref frame, centerPos, COLUMN_2, oreLabels, TextAlignment.LEFT);
            DrawList(ref frame, centerPos, COLUMN_R, oreAmounts, TextAlignment.RIGHT);
        }

        public void DrillDisplay(ref MySpriteDrawFrame frame, Vector2 centerPos) {
            bool hullLock = GetHullLockState();
            bool drillsOn = GetDrillStatus();
            bool junkOn = GetJunkToggle();
            float drillProgress = GetDrillProgress();

            DrawHeading(ref frame, centerPos, "Drill Status");

            DrawToggle(ref frame, centerPos, COLUMN_1, -125.0f, "Textures\\FactionLogo\\Others\\OtherIcon_5.dds", !hullLock, true);
            DrawToggle(ref frame, centerPos, COLUMN_2, -150.0f, !hullLock ? "Ready" : "Locked", !hullLock, false, TextAlignment.LEFT);

            DrawToggle(ref frame, centerPos, COLUMN_1, 0.0f, "Textures\\FactionLogo\\Miners\\MinerIcon_4.dds", drillsOn, true);
            DrawToggle(ref frame, centerPos, COLUMN_2, -25.0f, "Drills", drillsOn, false, TextAlignment.LEFT);

            DrawToggle(ref frame, centerPos, COLUMN_1, 125.0f, "MyObjectBuilder_Ore/Organic", junkOn, true);
            DrawToggle(ref frame, centerPos, COLUMN_2, 100.0f, "Junk", junkOn, false, TextAlignment.LEFT);

            DrawGraph(ref frame, centerPos, COLUMN_5, INDICATOR_CAUTION, drillProgress, "", "Textures\\FactionLogo\\Miners\\MinerIcon_3.dds", true);
        }

        public void DrawGraph(ref MySpriteDrawFrame frame, Vector2 centerPos, float column, Color color, float percent, string label = "", string icon = "", bool oscillate = false) {
            float level = percent * 3.0f;

            // Outer Frame
            frame.Add(new MySprite(){
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = "SquareSimple",
                Position = new Vector2(column, 0.0f) + centerPos,
                Size = new Vector2(GRAPH_OUTER_WIDTH, GRAPH_OUTER_HEIGHT),
                Color = color,
                RotationOrScale = 0.0f
            });
            // Inner Frame
            frame.Add(new MySprite(){
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = "SquareSimple",
                Position = new Vector2(column, 0.0f) + centerPos,
                Size = new Vector2(GRAPH_INNER_WIDTH, GRAPH_INNER_HEIGHT),
                Color = Color.Black,
                RotationOrScale = 0.0f
            });
            // Level
            frame.Add(new MySprite(){
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = "SquareSimple",
                Position = oscillate ? new Vector2(column, -(GRAPH_INNER_HEIGHT / 2) + level) + centerPos : new Vector2(column, (GRAPH_INNER_HEIGHT / 2) - (level / 2)) + centerPos,
                Size = oscillate ? new Vector2(GRAPH_INNER_WIDTH, GRAPH_PADDING) : new Vector2(GRAPH_INNER_WIDTH, level),
                Color = color,
                RotationOrScale = 0.0f
            });
            // Label
            if (label != "")
                frame.Add(new MySprite(){
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.CENTER,
                    Data = label,
                    Position = new Vector2(column, -125.0f) + centerPos,
                    Color = textColor,
                    FontId="DEBUG",
                    RotationOrScale = TEXT_LABEL_SIZE
                });
            // Icon
            if (icon != "")
                frame.Add(new MySprite(){
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = icon,
                    Position = new Vector2(column, 125.0f) + centerPos,
                    Size = new Vector2(40.0f, 40.0f),
                    Color = textColor,
                    RotationOrScale = 0.0f
                });
            // Percent
            frame.Add(new MySprite(){
                Type = SpriteType.TEXT,
                Alignment = TextAlignment.CENTER,
                Data = oscillate ? "" : String.Format("{0, 3:d}%", (int)percent),
                Position = new Vector2(column, 175.0f) + centerPos,
                Color = textColor,
                FontId="Monospace",
                RotationOrScale = TEXT_PERCENT_SIZE
            });            
        }

        public void DrawToggle(ref MySpriteDrawFrame frame, Vector2 centerPos, float column, float row, string data = "", bool state = false, bool icon = false, TextAlignment alignment = TextAlignment.CENTER) {
            frame.Add(new MySprite(){
                Type = icon ? SpriteType.TEXTURE : SpriteType.TEXT,
                Alignment = alignment,
                Data = data,
                Position = new Vector2(column, row) + centerPos,
                Size = new Vector2(GRAPH_INNER_WIDTH, GRAPH_INNER_WIDTH),
                Color = state ? INDICATOR_SAFE : INDICATOR_ALERT,
                RotationOrScale = icon ? 0.0f : 1.5f
            });
        }

        public void DrawHeading(ref MySpriteDrawFrame frame, Vector2 centerPos, string data = "") {
            frame.Add(new MySprite(){
                Type = SpriteType.TEXT,
                Alignment = TextAlignment.CENTER,
                Data = data,
                Position = new Vector2(0.0f, ROW_T) + centerPos,
                Color = textColor,
                FontId = "Monospace",
                RotationOrScale = TEXT_HEADING_SIZE
            });
        }

        public void DrawList(ref MySpriteDrawFrame frame, Vector2 centerPos, float column, string data = "", TextAlignment alignment = TextAlignment.LEFT) {
            frame.Add(new MySprite(){
                Type = SpriteType.TEXT,
                Alignment = alignment,
                Data = data,
                Position = new Vector2(column, -150.0f) + centerPos,
                Color = textColor,
                FontId = "Monospace",
                RotationOrScale = TEXT_LIST_SIZE
            });            
        }

        public void ClearDrawSurface(IMyTextSurface surface) {
            surface.PreserveAspectRatio = true;
            surface.ScriptBackgroundColor = Color.Black;
            surface.ContentType = ContentType.SCRIPT;
            surface.Script = "";
        }
        /* Program Ends Here */
    }
}