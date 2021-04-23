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
        //                            ARTICULATION JOINT CLASS
        class ArticulationJoint
        {
            public IMyTerminalBlock joint;
            public ArticulationType joint_type;
            private float _pitch_sensitivity; //TODO: make accessor functions
            private float _yaw_sensitivity;
            private float _roll_sensitivity;
            private float _up_down_sensitivity;
            private float _left_right_sensitivity;
            private float _forward_back_sensitivity;


            public ArticulationJoint(IMyTerminalBlock block)
            {
                joint = block;
                if (block is IMyMotorStator)
                {
                    joint_type = ArticulationType.rotor;
                }
                else
                {
                    joint_type = ArticulationType.piston;
                }

                _pitch_sensitivity = ConfigureSingleAxisInputResponse(block.CustomName, ControlType.pitch);
                _yaw_sensitivity = ConfigureSingleAxisInputResponse(block.CustomName, ControlType.yaw);
                _roll_sensitivity = ConfigureSingleAxisInputResponse(block.CustomName, ControlType.roll);
                _up_down_sensitivity = ConfigureSingleAxisInputResponse(block.CustomName, ControlType.up_down);
                _left_right_sensitivity = ConfigureSingleAxisInputResponse(block.CustomName, ControlType.left_right);
                _forward_back_sensitivity = ConfigureSingleAxisInputResponse(block.CustomName, ControlType.forward_back);
            }

            override public string ToString()
            {
                string s = $"Type: {joint_type.ToString()}\n" +
                    $"Pitch: {_pitch_sensitivity}\n" +
                    $"Yaw: {_yaw_sensitivity}\n" +
                    $"Roll: {_roll_sensitivity}\n" +
                    $"Up Down: {_up_down_sensitivity}\n" +
                    $"Left Right: {_left_right_sensitivity}\n" +
                    $"Forward Back: {_forward_back_sensitivity}\n";
                return s;
            }

            public void ApplyCommand(CommandInput commands)
            {
                float response = GetResponse(commands);

                if (joint_type == ArticulationType.piston)
                {
                    IMyPistonBase piston = (IMyPistonBase)joint; //should throw exception if this does not work
                    piston.Velocity = response;
                    return;
                }
                if (joint_type == ArticulationType.rotor)
                {
                    IMyMotorStator rotor = (IMyMotorStator)joint; //should throw exception if this does not work
                    rotor.TargetVelocityRPM = response;
                    return;
                }

                throw new Exception($"Error in CommandGroup.ApplyCommandResponse: Unrecognized joint type: {joint_type.ToString()}");
            }

            private float GetResponse(CommandInput inputs)
            {
                float total_response = inputs.pitch * _pitch_sensitivity;
                total_response += inputs.yaw * _yaw_sensitivity;
                total_response += inputs.roll * _roll_sensitivity;
                total_response += inputs.forward_back * _forward_back_sensitivity;
                total_response += inputs.left_right * _left_right_sensitivity;
                total_response += inputs.up_down * _up_down_sensitivity;

                if (joint_type == ArticulationType.piston)
                {
                    return MathHelper.Clamp(total_response, -1 * MAX_LINEAR_SPEED, MAX_LINEAR_SPEED);
                }
                if (joint_type == ArticulationType.rotor)
                {
                    return MathHelper.Clamp(total_response, -1 * MAX_ROTATIONAL_SPEED, MAX_ROTATIONAL_SPEED);
                }

                throw new Exception($"Critical Error in CommandGroup.GetResponse: Unrecognized joint type: {joint_type.ToString()}");
            }

            private float ConfigureSingleAxisInputResponse(string block_name, ControlType control_type)
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
                        throw new Exception($"Error: control type {control_type.ToString()} not accounted for.");
                }

                System.Text.RegularExpressions.Match m = regex.Match(block_name);

                if (!m.Success)
                {
                    return 0 ;
                }

                string[] parts_of_name = m.Value.Substring(0, m.Length - 1).Split(':');
                float response;

                //response has assigned value assigned during evaluation of if statement
                //response defaults to 1.0 if number portion of name is not found or if float parse fails
                if (parts_of_name.Length != 2 || !float.TryParse(parts_of_name[1], out response))
                {
                    response = 1.0f;
                }

                return response * global_scaler;
            }
        }

    }
}

