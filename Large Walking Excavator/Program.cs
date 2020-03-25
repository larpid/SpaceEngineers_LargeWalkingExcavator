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

        public class Leg
        {
            public Dictionary<string, IMyMotorStator> rotors;
            public Dictionary<string, IMyPistonBase> pistons;
            public IMyLandingGear landingGear;

            public Leg(IMyGridTerminalSystem gts, string id)  //id will here be "Left" or "Right" but could e.g. also be numerals
            {
                this.rotors = new Dictionary<string, IMyMotorStator>()
                {
                    ["yaw leg"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 1 (Yaw Leg)") as IMyMotorStator,
                    ["yaw body"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 2 (Yaw Body)") as IMyMotorStator,
                    ["pitch leg"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 3 (Pitch Leg)") as IMyMotorStator,
                    ["pitch body"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 4 (Pitch Body)") as IMyMotorStator,
                    ["roll leg"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 5 (Roll Leg)") as IMyMotorStator,
                    ["roll body"] = gts.GetBlockWithName($"LWE Walk Rotor {id} 6 (Roll Body)") as IMyMotorStator
                };
                this.pistons = new Dictionary<string, IMyPistonBase>()
                {
                    ["forward"] = gts.GetBlockWithName($"LWE Walk Piston {id} 1 (Forward)") as IMyPistonBase,
                    ["down"] = gts.GetBlockWithName($"LWE Walk Piston {id} 2 (Down)") as IMyPistonBase
                };
                this.landingGear = gts.GetBlockWithName($"LWE Landing Gear {id}") as IMyLandingGear;
            }
        
            public bool checkValidity()
            {
                return !(!this.rotors.Any() || !this.pistons.Any() || this.landingGear == null);  // true means valid
            }
        }

        public Program()
        {
            IMyGridTerminalSystem gts = GridTerminalSystem;

            //init mechanical blocks
            this.legs = new Dictionary<string, Leg>()
            {
                ["right"] = new Leg(gts, "Right"),
                ["left"] = new Leg(gts, "Left")
            };
            this.cockpit = gts.GetBlockWithName("LWE Cockpit") as IMyCockpit;
            this.cockpitLcdLarge = Me.GetSurface(0);

            this.activeMovementAction = null;

            if (!(this.legs["right"].checkValidity() && this.legs["left"].checkValidity() && this.cockpit != null))
            {
                this.cockpitLcdLarge.WriteText("one ore more of the expected mechanical components not found!");
                Echo("one ore more of the expected mechanical components not found!");
            }
            else
            {
                this.cockpitLcdLarge.WriteText("all set up!");
                Echo("all set up!");
            }
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
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
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    this.activeMovementAction = null;
                    this.cockpitLcdLarge.WriteText("movement script stopped", true);
                }
            }

            //is there an active action? then just do this
            if (this.activeMovementAction != null)
            {
                if (!this.activeMovementAction.Update())  //this continues the action and returns false on finish
                {
                    //active action finished
                    this.activeMovementAction = null;
                    ReadCustomData();
                    if (customData["walkState"] == "11")
                    {
                        customData["walkState"] = "1";

                        // switch Legs
                        var activeLeg = customData["walkActiveLeg"];
                        customData["walkActiveLeg"] = customData["walkInactiveLeg"];
                        customData["walkInactiveLeg"] = activeLeg;
                    }
                    else
                    {
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

                this.cockpitLcdLarge.WriteText("walk state : " + customData["walkActiveLeg"] + " " + customData["walkState"]);
                Echo("walk state : " + customData["walkActiveLeg"] + " " + customData["walkState"]);

                switch (Convert.ToInt32(customData["walkState"]))
                {
                    case 1:

                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["yaw leg"],
                            activeLeg.rotors["yaw body"].Angle + Convert.ToDouble(customData["addYaw"]));
                        break;
                    case 2:  // code here a bit dirty hardcoded... so just look away or sth
                        var pitch = GetPitch() - Convert.ToDouble(customData["setPitch"]);
                        if (customData["walkActiveLeg"] == "right")
                        {
                            pitch *= -1;
                        }
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["pitch leg"],
                            activeLeg.rotors["pitch body"].Angle + pitch);
                        break;
                    case 3:
                        this.activeMovementAction = new PistonAndLandingGearExtendAndLock(activeLeg.pistons["down"], activeLeg.landingGear);
                        break;
                    case 4:
                        this.activeMovementAction = new UnlockSafe(inactiveLeg.landingGear, activeLeg.landingGear);
                        break;
                    case 5:
                        this.activeMovementAction = new PistonMoveFull(inactiveLeg.pistons["down"], false);
                        break;
                    case 6:
                        this.activeMovementAction = new PistonMoveFull(inactiveLeg.pistons["forward"], false);
                        break;
                    case 7:
                        this.activeMovementAction = new RotorPositionSet(inactiveLeg.rotors["roll leg"],
                            inactiveLeg.rotors["roll body"].Angle + 0);
                        break;
                    case 8:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["yaw body"],
                            -1 * (activeLeg.rotors["yaw leg"].Angle + 0));
                        break;
                    case 9:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["pitch body"],
                            -1 * (activeLeg.rotors["pitch leg"].Angle + 0));
                        break;
                    case 10:
                        this.activeMovementAction = new RotorPositionSet(activeLeg.rotors["roll body"],
                            -1 * (activeLeg.rotors["roll leg"].Angle + GetRoll()));
                        break;
                    case 11:
                        this.activeMovementAction = new PistonMoveFull(activeLeg.pistons["forward"], true);
                        break;
                }
            }
        }

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
                "# Turns by this angle every walk step\n" +
                $"addYaw={customData["addYaw"]}\n" +
                "# keeps This Pitch relative to Gravity\n" +
                $"setPitch={customData["setPitch"]}\n" +
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
