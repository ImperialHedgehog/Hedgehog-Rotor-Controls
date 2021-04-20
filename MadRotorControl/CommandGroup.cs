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
                    _errors += ($"Block group \"{group.Name}\" does not contain a valid controller!\n");
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

                    
                    ArticulationJoint joint = new ArticulationJoint();
                    //TODO: refactor ArticulationJoint to do all this nonsense
                    /*  ConfigureInputResponse
                     *  GetResponse (make private method of ArticulationJoint)
                     *  ApplyCommand
                     */
                    joint.joint = block;
                    if (block as IMyMotorStator != null)
                    {
                        joint.joint_type = ArticulationType.rotor;
                    }
                    else
                    {
                        joint.joint_type = ArticulationType.piston;
                    }

                    joint.pitch_sensitivity = ConfigureInputResponse(block.CustomName, ControlType.pitch);
                    joint.yaw_sensitivity = ConfigureInputResponse(block.CustomName, ControlType.yaw);
                    joint.roll_sensitivity = ConfigureInputResponse(block.CustomName, ControlType.roll);
                    joint.up_down_sensitivity = ConfigureInputResponse(block.CustomName, ControlType.up_down);
                    joint.left_right_sensitivity = ConfigureInputResponse(block.CustomName, ControlType.left_right);
                    joint.forward_back_sensitivity = ConfigureInputResponse(block.CustomName, ControlType.forward_back);

                    //block.CustomData = joint.ToString();

                    _joints.Add(joint);
                }

                _command_input_memory_buffer = new CommandInput();
                IsValid = true;
                return;
            }

            public bool IsBeingControlled()
            {
                return _controller.IsUnderControl;
            }

            private float ConfigureInputResponse(string block_name, ControlType control_type)
            {
                System.Text.RegularExpressions.Regex regex;
                float global_scaler;

                switch (control_type)
                {
                    case ControlType.pitch:
                        regex = new System.Text.RegularExpressions.Regex(PITCH_COMMAND_NAME_PATTERN, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        global_scaler = GLOBAL_MAGNITUDE_SCALER * GLOBAL_PITCH_SCALER;
                        break;
                    case ControlType.yaw:
                        regex = new System.Text.RegularExpressions.Regex(YAW_COMMAND_NAME_PATTERN, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        global_scaler = GLOBAL_MAGNITUDE_SCALER * GLOBAL_YAW_SCALER;
                        break;
                    case ControlType.roll:
                        regex = new System.Text.RegularExpressions.Regex(ROLL_COMMAND_NAME_PATTERN, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        global_scaler = GLOBAL_MAGNITUDE_SCALER * GLOBAL_ROLL_SCALER;
                        break;
                    case ControlType.up_down:
                        regex = new System.Text.RegularExpressions.Regex(UP_DOWN_COMMAND_NAME_PATTERN, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        global_scaler = GLOBAL_MAGNITUDE_SCALER * GLOBAL_UP_DOWN_SCALER;
                        break;
                    case ControlType.left_right:
                        regex = new System.Text.RegularExpressions.Regex(LEFT_RIGHT_COMMAND_NAME_PATTERN, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        global_scaler = GLOBAL_MAGNITUDE_SCALER * GLOBAL_LEFT_RIGHT_SCALER;
                        break;
                    case ControlType.forward_back:
                        regex = new System.Text.RegularExpressions.Regex(FORWARD_BACK_COMMAND_NAME_PATTERN, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        global_scaler = GLOBAL_MAGNITUDE_SCALER * GLOBAL_FORWARD_BACK_SCALER;
                        break;
                    default:
                        parent.LogError($"Error: control type {control_type.ToString()} not accounted for.", "CommandGroup.ConfigureInputResponse");
                        throw new Exception();
                }

                System.Text.RegularExpressions.Match m = regex.Match(block_name);

                if (!m.Success)
                {
                    return 0;
                }

                string[] parts_of_name = m.Value.Substring(0, m.Length - 1).Split(':');
                float response;

                //response has assigned value assigned during evaluation of if statement
                //response defaults to 1.0 if number portion of name is not found or if float parse fails
                if (parts_of_name.Length != 2 || !float.TryParse(parts_of_name[1],out response))
                {
                    
                    response = 1.0f;
                }

                return response * global_scaler;      
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
                    float response = GetResponse(commands, joint);
                    ApplyCommandResponse(joint, response);
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

            private float GetResponse(CommandInput inputs, ArticulationJoint joint)
            {
                float total_response = inputs.pitch * joint.pitch_sensitivity;
                total_response += inputs.yaw * joint.yaw_sensitivity;
                total_response += inputs.roll * joint.roll_sensitivity;
                total_response += inputs.forward_back * joint.forward_back_sensitivity;
                total_response += inputs.left_right * joint.left_right_sensitivity;
                total_response += inputs.up_down * joint.up_down_sensitivity;

                if (joint.joint_type == ArticulationType.piston)
                {
                    return MathHelper.Clamp(total_response, -1 * MAX_LINEAR_SPEED, MAX_LINEAR_SPEED);
                }
                if (joint.joint_type == ArticulationType.rotor)
                {
                    return MathHelper.Clamp(total_response, -1 * MAX_ROTATIONAL_SPEED, MAX_ROTATIONAL_SPEED);
                }

                parent.LogError($"Critical Error: Unrecognized joint type: {joint.joint_type.ToString()}", "CommandGroup.GetResponse");
                throw new Exception();
            }

            private void ApplyCommandResponse(ArticulationJoint joint, float response)
            {
                if (joint.joint_type == ArticulationType.piston)
                {
                    IMyPistonBase piston = (IMyPistonBase)joint.joint; //should throw exception if this does not work
                    piston.Velocity = response;
                    return;
                }
                if (joint.joint_type == ArticulationType.rotor)
                {
                    IMyMotorStator rotor = (IMyMotorStator)joint.joint; //should throw exception if this does not work
                    rotor.TargetVelocityRPM = response;
                    return;
                }

                parent.LogError($"Unrecognized joint type: {joint.joint_type.ToString()}", "CommandGroup.ApplyCommandResponse");
                throw new Exception();
            }
        }

    }
}
