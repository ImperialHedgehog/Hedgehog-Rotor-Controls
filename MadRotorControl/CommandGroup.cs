using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        /*****************************************************************************/
        //                            COMMAND GROUP CLASS
        class CommandGroup
        {

            CommandInput _command_input_memory_buffer;
            public bool IsValid { get; }
            public string Name { get; }

            private string _errors;
            public string Errors { get { return _errors; } }
            Program parent;

            IMyShipController _controller;
            List<ArticulationJoint> _joints;

            public CommandGroup(IMyBlockGroup group, Program parent)
            {
                _errors = "";
                this.parent = parent;
                Name = $"Command Group \"{group.Name}\"";
                if (this.parent == null)
                {
                    IsValid = false;
                    throw new Exception($"Critical Error: Command group constructor for {this.ToString()} did not receive valid parent reference");
                }

                List<IMyTerminalBlock> getter_list = parent.GeneralGetterList;
                getter_list.Clear();
                group.GetBlocksOfType<IMyShipController>(getter_list, block => block.IsSameConstructAs(parent.Me));

                if (getter_list.Count == 0)
                {
                    _errors += ($"Block group does not contain a valid controller!\n");
                    IsValid = false;
                    return;
                }

                _controller = getter_list[0] as IMyShipController;

                if (_controller == null)
                {
                    parent.LogError("Failed to retrieve correct type for ship controller", this.ToString());
                    throw new Exception($"A Critical Error has occured in Command Group Constructor: Check that controller type requested from grid system is castable to interface used in script");
                }

                if (_controller is IMyCryoChamber)
                {
                    _errors += "No you cannot use a cryochamber as a control seat for this script. That's dumb.";
                    IsValid = false;
                    return;
                }

                if (parent.IsBlockAlreadyFound(_controller.GetId()))
                {
                    _errors += $"Controller \"{_controller.CustomName}\" is already in use in another group";
                    IsValid = false;
                    return;
                }

                if (getter_list.Count > 1)
                {
                    _errors += "Command group has multiple controllers. Control may be ambiguous";
                    IsValid = false;
                    return;
                }

                getter_list.Clear();
                group.GetBlocks(getter_list, block => block.IsSameConstructAs(parent.Me) && (block as IMyMotorStator != null || block as IMyPistonBase != null));

                if (getter_list.Count == 0)
                {
                    //TODO: Update this if I add other types of blocks the script can control
                    _errors += $"Block group does not contain any pistons or rotors";
                    IsValid = false;
                    return;
                }

                _joints = new List<ArticulationJoint>();
                foreach (IMyTerminalBlock block in getter_list)
                {
                    if (parent.IsBlockAlreadyFound(block.GetId()))
                    {
                        _errors += $"Block {block.ToString()} is already in use in another group";
                        IsValid = false;
                        return;
                    }

                    _joints.Add(new ArticulationJoint(block));
                }

                _command_input_memory_buffer = new CommandInput();
                IsValid = true;
                return;
            }

            public bool IsBeingControlled()
            {
                return IsValid && _controller.IsUnderControl;
            }


            public bool Run()
            {
                if (!IsValid)
                {
                    return false;
                }

                CommandInput commands = GetCommandInput();

                foreach (ArticulationJoint joint in _joints)
                {
                    joint.ApplyCommand(commands);
                }

                return true;
            }

            //Assigns value to the command_input_memory_buffer and returns reference
            //Using this more than once will overwrite the last run, but it only needs to run once per tick
            //and the results will always be the same in a given tick anyway
            private CommandInput GetCommandInput()
            {
                CommandInput inputs = _command_input_memory_buffer; //avoid heap memory allocation in general run
                inputs.forward_back = _controller.MoveIndicator.Z; 
                inputs.left_right = _controller.MoveIndicator.X;
                inputs.up_down = _controller.MoveIndicator.Y;

                inputs.pitch = _controller.RotationIndicator.X;
                inputs.yaw = _controller.RotationIndicator.Y;
                inputs.roll = _controller.RollIndicator;
                return inputs;
            }
        }
    }
}
