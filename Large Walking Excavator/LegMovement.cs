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
            abstract public bool Update();  //true means continue these Updates
        }
        
        public class UnlockSafe : LegMovement
        {
            public UnlockSafe(IMyLandingGear landingGearToUnlock, IMyLandingGear landingGearControl)  // to make sure at least one landing gear is always locked to the ground
            {
                if (landingGearControl.IsLocked)
                {
                    landingGearToUnlock.Unlock();
                }
            }

            public override bool Update()
            {
                return false;
            }
        }

        public class PistonAndLandingGearExtendAndLock : LegMovement
        {
            private readonly float pistonVelocity = 1; //unit: m/s
            private readonly IMyPistonBase piston;
            private readonly IMyLandingGear landingGear;
           
            public PistonAndLandingGearExtendAndLock(IMyPistonBase piston, IMyLandingGear landingGear)
            {
                this.piston = piston;
                this.landingGear = landingGear;
                this.piston.Velocity = pistonVelocity;

                this.StartMovement();
            }

            private void StartMovement()
            {
                this.piston.ApplyAction("OnOff_On");
                this.piston.ApplyAction("Extend");
            }

            public override bool Update()
            {
                if (landingGear.LockMode == LandingGearMode.ReadyToLock)
                {
                    this.piston.ApplyAction("OnOff_Off");
                    this.landingGear.Lock();
                    return false;
                }
                else return true;
            }
        }

        public class PistonMoveFull : LegMovement
        {
            private readonly float velocity = 1;  //unit: m/s
            private readonly bool extend;
            private readonly IMyPistonBase piston;
           
            public PistonMoveFull(IMyPistonBase piston, bool extend)
            {
                this.piston = piston;
                this.extend = extend;
                this.piston.Velocity = velocity;

                this.StartMovement();
            }

            private void StartMovement()
            {
                if (this.extend)
                {
                    this.piston.ApplyAction("Extend");
                }
                else
                {
                    this.piston.ApplyAction("Retract");
                }
                this.piston.ApplyAction("OnOff_On");
            }

            public override bool Update()
            {
                //check if end reached
                if (this.extend && this.piston.Status == PistonStatus.Extended ||
                    !this.extend && this.piston.Status == PistonStatus.Retracted)
                {
                    this.piston.ApplyAction("OnOff_Off");
                    return false;
                }
                else return true;
            }
        }

        public class RotorPositionSet : LegMovement
        {
            private readonly float rotationSpeed = 1;  //unit: RPM
            private readonly double targetAngle;
            private readonly IMyMotorStator rotor;
            
            public RotorPositionSet(IMyMotorStator rotor, double targetAngle)
            {
                if (targetAngle > 0)
                {
                    this.targetAngle = targetAngle;
                }
                else
                {
                    this.targetAngle = -1 * targetAngle;
                }
                this.rotor = rotor;

                this.StartMovement();
            }

            private void StartMovement()
            {
                //set speed (and direction)
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
                //stop movement if necessary
                if (this.rotor.TargetVelocityRPM > 0 && this.targetAngle - this.rotor.Angle > 0 && this.targetAngle - this.rotor.Angle > 1.5 * Math.PI ||
                    this.rotor.TargetVelocityRPM > 0 && this.targetAngle - this.rotor.Angle < 0 && this.rotor.Angle - this.targetAngle < 0.5 * Math.PI ||
                    this.rotor.TargetVelocityRPM < 0 && this.targetAngle - this.rotor.Angle > 0 && this.targetAngle - this.rotor.Angle < 0.5 * Math.PI ||
                    this.rotor.TargetVelocityRPM < 0 && this.targetAngle - this.rotor.Angle < 0 && this.rotor.Angle - this.targetAngle > 1.5 * Math.PI)
                {
                    //stop and lock rotor
                    this.rotor.ApplyAction("OnOff_Off");
                    this.rotor.RotorLock = true;
                    return false;
                }
                else
                    return true;
            }
        }
    }
}
