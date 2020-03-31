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
    partial class Program
    {
        abstract public class LegMovement
        {
            abstract public bool Update();  // true means continue these Updates

            abstract public void StopMovement();  // manual stop of moving parts for script shutdown only
        }
        
        public class UnlockSafe : LegMovement
        {
            private IMyLandingGear landingGearToUnlock;
            private IMyLandingGear landingGearControl;

            public UnlockSafe(IMyLandingGear landingGearToUnlock, IMyLandingGear landingGearControl)  // to make sure at least one landing gear is always locked to the ground
            {
                this.landingGearToUnlock = landingGearToUnlock;
                this.landingGearControl = landingGearControl;

                ExecuteLockSwitch();  // in this case just one action
            }
            private void ExecuteLockSwitch()
            { 
                if (this.landingGearControl.IsLocked)
                {
                    this.landingGearToUnlock.Unlock();
                }
            }

            public override bool Update()
            {
                if (this.landingGearToUnlock.IsLocked)
                {
                    // if gear not unlocked as planned getting stuck in this loop is better than just continuing
                    // however there might be a need for information about this ingame
                    ExecuteLockSwitch();
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public override void StopMovement() { }  // no action needed since there is no continuous movement in this class
        }

        public class PistonAndMergeBlockExtendAndMerge : LegMovement
        {
            private IMyPistonBase piston;
            private IMyShipMergeBlock mergeBlock;
            private IMyGridTerminalSystem gts; //needed only because keen messed up in IMyShipMergeBlock.IsConnected()

            public PistonAndMergeBlockExtendAndMerge(IMyPistonBase piston, IMyShipMergeBlock mergeBlock, IMyGridTerminalSystem gts)
            {
                this.piston = piston;
                this.mergeBlock = mergeBlock;
                this.gts = gts;

                this.StartMovement();
            }

            private void StartMovement()
            {
                this.mergeBlock.ApplyAction("OnOff_On");
                this.piston.ApplyAction("Extend");
                this.piston.ApplyAction("OnOff_On");
            }

            public override bool Update()
            {
                // check if end reached
                if (IsMergeBlockConnected(this.mergeBlock, this.gts))
                    {
                        this.piston.ApplyAction("OnOff_Off");
                        return false;
                    }
                else return true;
            }

            public override void StopMovement()
            {
                this.piston.ApplyAction("OnOff_Off");
            }

            bool IsMergeBlockConnected(IMyShipMergeBlock SourceMB, IMyGridTerminalSystem gts)
            // due to a bug making IMyShipMergeBlock.isConnected show info only about yellow state (not green)
            // copied from user Martin R Wolfe @ https://forum.keenswh.com/threads/1-176-002-dev-imyshipmergeblock-isconnected.7393234/ and changed a bit
            {
                bool Result = SourceMB.Enabled;
                IMyCubeGrid CubeGrid = SourceMB.CubeGrid;
                Quaternion aQuaternion;
                Vector3I TargetMbLoc;
                List<IMyShipMergeBlock> TargetMBs = new List<IMyShipMergeBlock>();
                //Find target location
                SourceMB.Orientation.GetQuaternion(out aQuaternion);
                TargetMbLoc = SourceMB.Position + Vector3I.Transform(new Vector3I(1, 0, 0), aQuaternion);
                //Find TargetMB will only be found if powered and on same grid as source
                gts.GetBlocksOfType<IMyShipMergeBlock>(TargetMBs,
                x => ((x.CubeGrid == CubeGrid) && (x.Enabled == true) && (x.Position == TargetMbLoc)));
                if (TargetMBs.Count != 0)
                {//We have a powered MB at source merge location
                 //Find target's merge location
                    TargetMBs[0].Orientation.GetQuaternion(out aQuaternion);
                    TargetMbLoc = TargetMBs[0].Position + Vector3I.Transform(new Vector3I(1, 0, 0), aQuaternion);
                    //Check that targets merge location is source's location
                    Result &= (TargetMbLoc == SourceMB.Position);
                }
                else Result = false;  //no target found
                return Result;
            }
        }

        public class PistonAndLandingGearExtendAndLock : LegMovement
        {
            private IMyPistonBase piston;
            private IMyPistonBase supportPiston;
            private IMyLandingGear landingGear;
           
            public PistonAndLandingGearExtendAndLock(IMyPistonBase piston, IMyLandingGear landingGear, IMyPistonBase supportPiston = null)
            {
                this.piston = piston;
                this.supportPiston = supportPiston;
                this.landingGear = landingGear;

                this.StartMovement();
            }

            private void StartMovement()
            {
                this.piston.ApplyAction("Extend");
                this.piston.ApplyAction("OnOff_On");
                if (this.supportPiston != null)
                {
                    this.supportPiston.ApplyAction("Extend");
                    this.supportPiston.ApplyAction("OnOff_On");
                }
            }

            public override bool Update()
            {
                if (this.landingGear.LockMode == LandingGearMode.ReadyToLock)
                {
                    this.piston.ApplyAction("OnOff_Off");
                    if (this.supportPiston != null)
                    {
                        this.supportPiston.ApplyAction("OnOff_Off");
                    }
                    this.landingGear.Lock();
                    return false;
                }
                else return true;
            }

            public override void StopMovement()
            {
                this.piston.ApplyAction("OnOff_Off");
                if (this.supportPiston != null)
                {
                    this.supportPiston.ApplyAction("OnOff_Off");
                }
            }
        }

        public class PistonMoveFull : LegMovement
        {
            private readonly bool extend;
            private IMyPistonBase piston;
            private IMyPistonBase supportPiston;
           
            public PistonMoveFull(IMyPistonBase piston, bool extend, IMyPistonBase supportPiston = null)
            {
                this.piston = piston;
                this.supportPiston = supportPiston;
                this.extend = extend;

                this.StartMovement();
            }

            private void StartMovement()
            {
                string action = "Retract";
                if (extend)
                {
                    action = "Extend";
                }

                this.piston.ApplyAction(action);  // "Extend" or "Retract"
                this.piston.ApplyAction("OnOff_On");

                if (this.supportPiston != null)
                {
                    this.supportPiston.ApplyAction(action);  // "Extend" or "Retract"
                    this.supportPiston.ApplyAction("OnOff_On");
                }
            }

            public override bool Update()
            {
                // check if end reached
                if (this.supportPiston != null)
                {
                    if (this.extend && this.piston.Status == PistonStatus.Extended ||  // && this.supportPiston.Status == PistonStatus.Extended ||
                        !this.extend && this.piston.Status == PistonStatus.Retracted)  // && this.supportPiston.Status == PistonStatus.Retracted)
                                // supportPiston status currently ignored because design never lets it retract completely
                    {
                        this.piston.ApplyAction("OnOff_Off");
                        this.supportPiston.ApplyAction("OnOff_Off");
                        return false;
                    }
                    else return true;
                }
                else
                {
                    if (this.extend && this.piston.Status == PistonStatus.Extended ||
                        !this.extend && this.piston.Status == PistonStatus.Retracted)
                    {
                        this.piston.ApplyAction("OnOff_Off");
                        return false;
                    }
                    else return true;
                }
            }

            public override void StopMovement()
            {
                this.piston.ApplyAction("OnOff_Off");
                if (this.supportPiston != null)
                {
                    this.supportPiston.ApplyAction("OnOff_Off");
                }
            }
        }

        public class PistonQuadrupleExtend : LegMovement
        {
            private IMyPistonBase piston1;
            private IMyPistonBase piston2;
            private IMyPistonBase piston3;
            private IMyPistonBase piston4;
            
            public PistonQuadrupleExtend(IMyPistonBase piston1, IMyPistonBase piston2, IMyPistonBase piston3, IMyPistonBase piston4)
            {
                this.piston1 = piston1;
                this.piston2 = piston2;
                this.piston3 = piston3;
                this.piston4 = piston4;

                this.StartMovement();
            }

            private void StartMovement()
            {
                this.piston1.ApplyAction("Extend");
                this.piston2.ApplyAction("Extend");
                this.piston3.ApplyAction("Extend");
                this.piston4.ApplyAction("Extend");
                this.piston1.ApplyAction("OnOff_On");
                this.piston2.ApplyAction("OnOff_On");
                this.piston3.ApplyAction("OnOff_On");
                this.piston4.ApplyAction("OnOff_On");
            }

            public override bool Update()
            {
                // check if end reached (one of those at the and is good enough)
                if (this.piston1.Status == PistonStatus.Extended ||
                    this.piston2.Status == PistonStatus.Extended ||
                    this.piston3.Status == PistonStatus.Extended ||
                    this.piston4.Status == PistonStatus.Extended)
                {
                    StopMovement();
                    return false;
                }
                else return true;
            }

            public override void StopMovement()
            {
                this.piston1.ApplyAction("OnOff_Off");
                this.piston2.ApplyAction("OnOff_Off");
                this.piston3.ApplyAction("OnOff_Off");
                this.piston4.ApplyAction("OnOff_Off");
            }
        }

        public class RotorPositionSet : LegMovement
        {
            private readonly float rotationSpeed = 1;  // unit: RPM
            private readonly double targetAngle;
            private IMyMotorStator rotor;
            
            public RotorPositionSet(IMyMotorStator rotor, double targetAngle)
            {
                while (targetAngle < 0)
                {
                    targetAngle += 2 * Math.PI;
                }
                this.targetAngle = targetAngle;

                this.rotor = rotor;

                this.StartMovement();
            }

            private void StartMovement()
            {
                // set speed (and direction)
                if (this.targetAngle - this.rotor.Angle > 0 && this.targetAngle - this.rotor.Angle < Math.PI ||
                    this.targetAngle - this.rotor.Angle < 0 && this.rotor.Angle - this.targetAngle > Math.PI)
                {
                    this.rotor.TargetVelocityRPM = rotationSpeed;
                }
                else
                {
                    this.rotor.TargetVelocityRPM = -1 * rotationSpeed;
                }
                this.rotor.ApplyAction("OnOff_On");
                this.rotor.RotorLock = false;
            }

            override public bool Update()
            {
                // stop movement if necessary
                if (this.rotor.TargetVelocityRPM > 0 && this.targetAngle - this.rotor.Angle > 0 && this.targetAngle - this.rotor.Angle > 1.5 * Math.PI ||
                    this.rotor.TargetVelocityRPM > 0 && this.targetAngle - this.rotor.Angle < 0 && this.rotor.Angle - this.targetAngle < 0.5 * Math.PI ||
                    this.rotor.TargetVelocityRPM < 0 && this.targetAngle - this.rotor.Angle > 0 && this.targetAngle - this.rotor.Angle < 0.5 * Math.PI ||
                    this.rotor.TargetVelocityRPM < 0 && this.targetAngle - this.rotor.Angle < 0 && this.rotor.Angle - this.targetAngle > 1.5 * Math.PI)
                {
                    // stop and lock rotor
                    this.rotor.ApplyAction("OnOff_Off");
                    this.rotor.RotorLock = true;
                    return false;
                }
                else
                    return true;
            }

            public override void StopMovement()
            {
                this.rotor.ApplyAction("OnOff_Off");
                this.rotor.RotorLock = true;
            }
        }

        public class SetBodyAxis : LegMovement
        {
            private readonly double targetAngle;
            private IMyPistonBase pistonMain;
            private IMyPistonBase pistonReverse;
            private IMyPistonBase pistonReverseSupport;
            private IMyMotorStator setRotor;
            private IMyMotorStator spinRotor1;
            private IMyMotorStator spinRotor2;
            private readonly bool invertDirection;
            private bool directionOfSetRotor;

            public SetBodyAxis(IMyPistonBase pistonMain, IMyPistonBase pistonReverse, IMyMotorStator setRotor, IMyMotorStator spinRotor1, IMyMotorStator spinRotor2, double targetAngle, bool invertDirection, IMyPistonBase pistonReverseSupport = null)
            {
                this.pistonMain = pistonMain;
                this.pistonReverse = pistonReverse;
                this.pistonReverseSupport = pistonReverseSupport;

                while (targetAngle < 0)
                {
                    targetAngle += 2 * Math.PI;
                }
                this.targetAngle = targetAngle;

                this.setRotor = setRotor;
                this.spinRotor1 = spinRotor1;
                this.spinRotor2 = spinRotor2;

                this.invertDirection = invertDirection; 

                this.StartMovement();
            }

            private void StartMovement()
            {
                this.directionOfSetRotor = (this.targetAngle - this.setRotor.Angle > 0 && this.targetAngle - this.setRotor.Angle < Math.PI ||
                    this.targetAngle - this.setRotor.Angle < 0 && this.setRotor.Angle - this.targetAngle > Math.PI);  // true means move positive (angular)

                // depending on the exact arm and rotor the piston needs to retract for positive angular movement
                if (directionOfSetRotor != this.invertDirection)
                {
                    this.pistonMain.ApplyAction("Extend");
                    this.pistonReverse.ApplyAction("Retract");
                    if (this.pistonReverseSupport != null)
                    {
                        this.pistonReverseSupport.ApplyAction("Retract");
                    }
                }
                else
                {
                    this.pistonMain.ApplyAction("Retract");
                    this.pistonReverse.ApplyAction("Extend");
                    if (this.pistonReverseSupport != null)
                    {
                        this.pistonReverseSupport.ApplyAction("Extend");
                    }
                }

                this.setRotor.RotorLock = false;
                this.spinRotor1.RotorLock = false;
                this.spinRotor2.RotorLock = false;
                this.pistonMain.ApplyAction("OnOff_On");
                this.pistonReverse.ApplyAction("OnOff_On");
                if (this.pistonReverseSupport != null)
                {
                    this.pistonReverseSupport.ApplyAction("OnOff_On");
                }
            }

            override public bool Update()
            {
                // stop movement if necessary
                if (this.directionOfSetRotor && this.targetAngle - this.setRotor.Angle > 0 && this.targetAngle - this.setRotor.Angle > 1.5 * Math.PI ||
                    this.directionOfSetRotor && this.targetAngle - this.setRotor.Angle < 0 && this.setRotor.Angle - this.targetAngle < 0.5 * Math.PI ||
                    !this.directionOfSetRotor && this.targetAngle - this.setRotor.Angle > 0 && this.targetAngle - this.setRotor.Angle < 0.5 * Math.PI ||
                    !this.directionOfSetRotor && this.targetAngle - this.setRotor.Angle < 0 && this.setRotor.Angle - this.targetAngle > 1.5 * Math.PI)
                {
                    // stop piston and lock rotors
                    StopMovement();
                    return false;
                }
                else
                    return true;
            }

            public override void StopMovement()
            {
                this.pistonMain.ApplyAction("OnOff_Off");
                this.pistonReverse.ApplyAction("OnOff_Off");
                if (this.pistonReverseSupport != null)
                {
                    this.pistonReverseSupport.ApplyAction("OnOff_Off");
                }
                this.setRotor.RotorLock = true;
                this.spinRotor1.RotorLock = true;
                this.spinRotor2.RotorLock = true;
            }
        }
    }
}
