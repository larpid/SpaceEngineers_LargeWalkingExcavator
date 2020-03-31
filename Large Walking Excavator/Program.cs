/* created 03/2020 by Lars P
 * This Script handles the movement of a Machine in the Game Space Engineers.
 * The Machine is called "Large Walking Excavator" and might at some Point be available to Steam Workshop
 */

using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public Dictionary<string, Leg> legs;
        public IMyCockpit cockpit;
        Dictionary<string, string> customData;
        private IMyTextSurface cockpitLcdLarge;

        public LegMovement activeMovementAction;
        private IMyGridTerminalSystem gts;

        public class Leg
        {
            public Dictionary<string, IMyMotorStator> rotors;
            public Dictionary<string, IMyPistonBase> pistons;
            public Dictionary<string, IMyShipMergeBlock> mergeBlocks;
            public IMyLandingGear landingGear;

            public Leg(IMyGridTerminalSystem gts, string id)  //id will here be "Left" or "Right" but could e.g. also be numerals
            {
                this.rotors = new Dictionary<string, IMyMotorStator>()
                {
                    ["yaw 1"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 1 (Yaw 1)") as IMyMotorStator,
                    ["yaw 2"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 4 (Yaw 2)") as IMyMotorStator,
                    ["pitch 1"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 2 (Pitch 1)") as IMyMotorStator,
                    ["pitch 2"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 5 (Pitch 2)") as IMyMotorStator,
                    ["pitch 3"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 6 (Pitch 3)") as IMyMotorStator,
                    ["roll 1"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 3 (Roll 1)") as IMyMotorStator,
                    ["roll 2"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 7 (Roll 2)") as IMyMotorStator
                };
                this.pistons = new Dictionary<string, IMyPistonBase>()
                {
                    ["forward"] = gts.GetBlockWithName($"LWE Walk Piston {id} 1 (Forward)") as IMyPistonBase,
                    ["forward support"] = gts.GetBlockWithName($"LWE Walk Piston {id} 3 (Forward Support)") as IMyPistonBase,
                    ["down"] = gts.GetBlockWithName($"LWE Walk Piston {id} 2 (Down)") as IMyPistonBase,
                    ["down support"] = gts.GetBlockWithName($"LWE Walk Piston {id} 4 (Down Support)") as IMyPistonBase
                };
                this.mergeBlocks = new Dictionary<string, IMyShipMergeBlock>()
                {
                    ["knee"] = gts.GetBlockWithName($"LWE Merge {id} Knee") as IMyShipMergeBlock,
                    ["foot"] = gts.GetBlockWithName($"LWE Merge {id} Foot") as IMyShipMergeBlock
                };
                this.landingGear = gts.GetBlockWithName($"LWE Landing Gear {id}") as IMyLandingGear;
            }

            public bool checkValidity()
            {
                return this.rotors.All(rotor => rotor.Value != null) &&
                    this.pistons.All(piston => piston.Value != null) &&
                    this.mergeBlocks.All(mergeBlock => mergeBlock.Value != null) &&
                    this.landingGear != null;  // true means valid
            }
        }

        public Program()  // init
        {
            this.gts = GridTerminalSystem;

            //init mechanical blocks
            this.legs = new Dictionary<string, Leg>()
            {
                ["right"] = new Leg(this.gts, "Right"),
                ["left"] = new Leg(this.gts, "Left")
            };
            this.cockpit = this.gts.GetBlockWithName("LWE Cockpit") as IMyCockpit;
            this.cockpitLcdLarge = Me.GetSurface(0);

            this.activeMovementAction = null;

            if (!(this.legs["right"].checkValidity() && this.legs["left"].checkValidity() && this.cockpit != null))
            {
                this.cockpitLcdLarge.WriteText("one ore more of the expected mechanical components not found!\n");
                Echo("one ore more of the expected mechanical components not found!\n");
            }
            else
            {
                this.cockpitLcdLarge.WriteText("all set up!\n");
                Echo("all set up!\n");
            }
        }

        public void Save()  // executed before server autosave
        {
            // nothing to save (thats already happening in the main script)
            // lets just hope things won't get completely messed up on a reload
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "")
            {
                if (argument == "start")
                {
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                }
                else if (argument == "stop")
                {
                    if (this.activeMovementAction != null)
                    {
                        this.activeMovementAction.StopMovement();
                        this.activeMovementAction = null;
                    }
                    
                    Runtime.UpdateFrequency = UpdateFrequency.None;

                    this.cockpitLcdLarge.WriteText("movement script stopped", true);
                    return;
                }
            }

            // is there an active action? then continue it
            if (this.activeMovementAction != null)
            {
                if (!this.activeMovementAction.Update())  // Update() continues the action and returns false on finish
                {
                    // active action finished
                    this.activeMovementAction = null;

                    // after finished action update walk state
                    ReadCustomData();
                    if (Convert.ToInt32(customData["walkState"]) >= 36)
                    {
                        customData["walkState"] = "1";  // start cycle at state "1". "0" could be used as initial state

                        // switch Legs
                        var activeLeg = customData["walkActiveLeg"];
                        customData["walkActiveLeg"] = customData["walkInactiveLeg"];
                        customData["walkInactiveLeg"] = activeLeg;
                    }
                    else
                    {
                        if (customData["walkState"] == "0")
                        {
                            Runtime.UpdateFrequency = UpdateFrequency.None;
                            this.cockpitLcdLarge.WriteText("Script Stopped after setup phase");
                        }
                        customData["walkState"] = (Convert.ToInt32(customData["walkState"]) + 1).ToString();
                    }
                    WriteCustomData();
                }
            }
            else
            {
                // choose and set up new active movement action
                ReadCustomData();
                var activeLeg = this.legs[customData["walkActiveLeg"]];
                var inactiveLeg = this.legs[customData["walkInactiveLeg"]];
                bool rightLegActive = customData["walkActiveLeg"] == "right";

                this.cockpitLcdLarge.WriteText("walk state : " + customData["walkActiveLeg"] + " " + customData["walkState"] + "\n");

                if(!(new List<string>() {"straight", "gps"}.Contains(customData["targetMode"])))
                {
                    this.cockpitLcdLarge.WriteText("ERROR: unknown target mode\n");
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    return;
                }

                switch (Convert.ToInt32(customData["walkState"]))
                {
                    case 0:  // initial state. not reached in normal execution
                        this.activeMovementAction = new PistonAndLandingGearExtendAndLock(activeLeg.pistons["down"], activeLeg.landingGear, activeLeg.pistons["down support"]);
                        break;
                    case 1:
                        this.activeMovementAction = new UnlockSafe(inactiveLeg.landingGear, activeLeg.landingGear);
                        break;
                    case 2:
                        this.activeMovementAction = new PistonMoveFull(inactiveLeg.pistons["down"], false, inactiveLeg.pistons["down support"]);
                        break;
                    case 3:
                        this.activeMovementAction = new PistonMoveFull(inactiveLeg.pistons["forward"], false, inactiveLeg.pistons["forward support"]);
                        break;
                    case 4:
                        this.activeMovementAction = new PistonAndLandingGearExtendAndLock(inactiveLeg.pistons["down"], inactiveLeg.landingGear, inactiveLeg.pistons["down support"]);
                        break;
                    case 5:
                        this.activeMovementAction = new UnlockSafe(activeLeg.landingGear, inactiveLeg.landingGear);
                        break;
                    case 6:
                        this.activeMovementAction = new PistonMoveFull(activeLeg.pistons["down"], false, activeLeg.pistons["down support"]);
                        break;
                    case 7:
                        this.activeMovementAction = new PistonMoveFull(activeLeg.pistons["forward"], false, activeLeg.pistons["forward support"]);
                        break;
                    case 8:
                        this.activeMovementAction = new PistonAndLandingGearExtendAndLock(activeLeg.pistons["down"], activeLeg.landingGear, activeLeg.pistons["down support"]);
                        break;
                    case 9:
                        activeLeg.mergeBlocks["foot"].ApplyAction("OnOff_Off");
                        this.activeMovementAction = new PistonMoveFull(activeLeg.pistons["down support"], false);
                        break;
                    case 10:
                        activeLeg.mergeBlocks["knee"].ApplyAction("OnOff_Off");
                        this.activeMovementAction = new PistonMoveFull(activeLeg.pistons["forward support"], false);
                        break;
                    case 11:
                        inactiveLeg.mergeBlocks["knee"].ApplyAction("OnOff_Off");
                        this.activeMovementAction = new PistonMoveFull(inactiveLeg.pistons["forward support"], false);
                        break;

                    case 12:  // set new Yaw
                        if (customData["targetMode"] == "straight")
                        {
                            this.activeMovementAction = new SetBodyAxis(activeLeg.pistons["forward"], inactiveLeg.pistons["forward"], inactiveLeg.rotors["yaw 1"],
                            activeLeg.rotors["yaw 1"], activeLeg.rotors["yaw 2"], -1 * Convert.ToDouble(customData["addYaw"]) * 2 * Math.PI / 360, rightLegActive);
                        }
                        else if (customData["targetMode"] == "gps")
                        {
                            throw new System.Exception("not implemented");
                        }
                        else throw new System.Exception($"unknown target mode: {customData["targetMode"]}");
                        break;

                    case 13:  // set new Pitch
                        if (customData["targetMode"] == "straight")
                        {
                            this.activeMovementAction = new SetBodyAxis(activeLeg.pistons["down"], inactiveLeg.pistons["down"], inactiveLeg.rotors["roll 1"],
                            activeLeg.rotors["roll 1"], activeLeg.rotors["roll 2"], inactiveLeg.rotors["roll 1"].Angle -1 * GetRoll(), !rightLegActive, inactiveLeg.pistons["down support"]);
                        }
                        else if (customData["targetMode"] == "gps")
                        {
                            throw new System.Exception("not implemented");
                        }
                        else throw new System.Exception($"unknown target mode: {customData["targetMode"]}");
                        break;

                    case 14:
                        var pitchCorrection = Convert.ToDouble(customData["setPitch"])*2*Math.PI/360 - GetPitch();  
                        if (!rightLegActive)
                        {
                            pitchCorrection *= -1;
                        }

                        this.activeMovementAction = new SetBodyAxis(activeLeg.pistons["down"], inactiveLeg.pistons["down"], inactiveLeg.rotors["pitch 1"],
                        activeLeg.rotors["pitch 2"], activeLeg.rotors["pitch 3"], pitchCorrection, !rightLegActive, inactiveLeg.pistons["down support"]);
                        break;
                    case 15:
                        this.activeMovementAction = new UnlockSafe(activeLeg.landingGear, inactiveLeg.landingGear);
                        break;
                    case 16:
                        this.activeMovementAction = new PistonMoveFull(activeLeg.pistons["down"], false);
                        break;
                    case 17:
                        this.activeMovementAction = new PistonMoveFull(activeLeg.pistons["forward"], false);
                        break;
                    case 18:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["roll 2"], 0);
                        break;
                    case 19:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["pitch 3"], 0);
                        break;
                    case 20:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["pitch 2"], 0);
                        break;
                    case 21:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["yaw 2"], 0);
                        break;
                    case 22:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["roll 1"], 0);
                        break;
                    case 23:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["pitch 1"], 0);
                        break;
                    case 24:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["yaw 1"], 0);
                        break;
                    case 25:
                        this.activeMovementAction = new PistonAndMergeBlockExtendAndMerge(activeLeg.pistons["forward support"], activeLeg.mergeBlocks["knee"], this.gts);
                        break;
                    case 26:
                        this.activeMovementAction = new PistonAndMergeBlockExtendAndMerge(activeLeg.pistons["down support"], activeLeg.mergeBlocks["foot"], this.gts);
                        break;
                    case 27:
                        this.activeMovementAction = new PistonAndLandingGearExtendAndLock(activeLeg.pistons["down"], activeLeg.landingGear, activeLeg.pistons["down support"]);
                        break;
                    case 28:
                        this.activeMovementAction = new UnlockSafe(inactiveLeg.landingGear, activeLeg.landingGear);
                        break;
                    case 29:
                        this.activeMovementAction = new PistonMoveFull(inactiveLeg.pistons["down"], false, inactiveLeg.pistons["down support"]);
                        break;
                    case 30:
                        this.activeMovementAction = new PistonMoveFull(inactiveLeg.pistons["forward"], false);
                        break;
                    case 31:
                        this.activeMovementAction = new RotorPositionSet(inactiveLeg.rotors["roll 1"], 0);
                        break;
                    case 32:
                        this.activeMovementAction = new RotorPositionSet(inactiveLeg.rotors["pitch 1"], 0);
                        break;
                    case 33:
                        this.activeMovementAction = new RotorPositionSet(inactiveLeg.rotors["yaw 1"], 0);
                        break;
                    case 34:
                        this.activeMovementAction = new PistonAndMergeBlockExtendAndMerge(inactiveLeg.pistons["forward support"], inactiveLeg.mergeBlocks["knee"], this.gts);
                        break;
                    case 35:
                        this.activeMovementAction = new PistonAndLandingGearExtendAndLock(inactiveLeg.pistons["down"], inactiveLeg.landingGear, inactiveLeg.pistons["down support"]);
                        break;
                    case 36:
                        // intensive drilling action (mabe add slow/ adaptive drilling and battery checks here)
                        this.activeMovementAction = new PistonQuadrupleExtend(activeLeg.pistons["forward"], activeLeg.pistons["forward support"],
                            inactiveLeg.pistons["forward"], inactiveLeg.pistons["forward support"]);
                        break;
                }
            }
        }

        // since there is no GetYaw() we define positive values as yaw to left

        private double GetPitch()
        {
            var gravityInCockpitCoordinates = Vector3D.TransformNormal(this.cockpit.GetNaturalGravity(), MatrixD.Transpose(this.cockpit.WorldMatrix));
            return Math.Atan(gravityInCockpitCoordinates.Z / gravityInCockpitCoordinates.Y);  // positive means pitch up
        }

        private double GetRoll()
        {
            var gravityInCockpitCoordinates = Vector3D.TransformNormal(this.cockpit.GetNaturalGravity(), MatrixD.Transpose(this.cockpit.WorldMatrix));
            return Math.Atan(gravityInCockpitCoordinates.X / gravityInCockpitCoordinates.Y);  // positive means roll left
        }

        private void ReadCustomData()
        {
            // read the command blocks custom data field
            this.customData = new Dictionary<string, string>();
            foreach (string dataLine in Me.CustomData.Split('\n'))
            {
                var splittedDataLine = dataLine.Split(new char[] { '=' }, 2);
                if (splittedDataLine.Length == 1)
                {
                    customData[splittedDataLine[0]] = "";
                }
                else
                {
                    customData[splittedDataLine[0]] = splittedDataLine[1];
                }
            }
        }

        private void WriteCustomData()
        {
            // write the command blocks custom data field
            Me.CustomData =
                "### Settings ###\n" +
                "# Turns by this angle every walk step (straight targetMode)\n" +
                $"addYaw={customData["addYaw"]}\n" +
                "# keeps This Pitch relative to Gravity (straight targetMode)\n" +
                $"setPitch={customData["setPitch"]}\n" +
                "\n" +
                "# target mode can be straight or gps\n" +
                $"targetMode={customData["targetMode"]}\n" +
                "\n" +
                "# coordinate and absolute max values\n" +
                $"gpsX={customData["gpsX"]}\n" +
                $"gpsY={customData["gpsY"]}\n" +
                $"gpsZ={customData["gpsZ"]}\n" +
                $"gpsMaxYaw={customData["gpsMaxYaw"]}\n" +
                $"gpsMaxPitch={customData["gpsMaxPitch"]}\n" +
                "\n" +
                "\n" +
                "### States handled by Script ###\n" +
                $"walkActiveLeg={customData["walkActiveLeg"]}\n" +
                $"walkInactiveLeg={customData["walkInactiveLeg"]}\n" +
                $"walkState={customData["walkState"]}\n" +
                "";
        }
    }
}
