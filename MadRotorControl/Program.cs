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
    partial class Program : MyGridProgram
    {
        //GLOBAL CONSTANTS
        const string COMMAND_GROUP_NAME_PATTERN = @"Articulated\s*Assembly\s*\d*";

        const string NUMBER_PATTERN = @"([\+\-]?\d+\.?\d*)";

        const string PITCH_COMMAND_NAME_PATTERN = @"\[(mouse_up_down:?" + NUMBER_PATTERN + @"?)\]";
        const string YAW_COMMAND_NAME_PATTERN = @"\[(mouse_left_right:?" + NUMBER_PATTERN + @"?)\]";
        const string ROLL_COMMAND_NAME_PATTERN = @"\[(qe:?" + NUMBER_PATTERN + @"?)\]";
        const string UP_DOWN_COMMAND_NAME_PATTERN = @"\[(space[\s_]?c:?" + NUMBER_PATTERN + @"?)\]";
        const string LEFT_RIGHT_COMMAND_NAME_PATTERN = @"\[(ad:?" + NUMBER_PATTERN + @"?)\]";
        const string FORWARD_BACK_COMMAND_NAME_PATTERN = @"\[(ws:?" + NUMBER_PATTERN + @"?)\]";

        const float MAX_ROTATIONAL_SPEED = 60.0f; //rpm
        const float MAX_LINEAR_SPEED = 10.0f; //meters per second

        const float GLOBAL_MAGNITUDE_SCALER = 1.0f;
        const float GLOBAL_YAW_SCALER = 1.0f / 50.0f;
        const float GLOBAL_PITCH_SCALER = -1.0f / 50.0f;
        const float GLOBAL_ROLL_SCALER = 1.0f;
        const float GLOBAL_UP_DOWN_SCALER = 1.0f;
        const float GLOBAL_LEFT_RIGHT_SCALER = 1.0f;
        const float GLOBAL_FORWARD_BACK_SCALER = -1.0f;

        const bool SUPPRESS_WARNINGS = false;

        private const double REFRESH_TIME = 30.0; //seconds
        private double _time_since_last_refresh = REFRESH_TIME;

        //GLOBAL VARIABLES
        bool PREVENT_ASSIGNMENT_OF_BLOCKS_TO_MULTIPLE_COMMAND_GROUPS = true;
        List<IMyTerminalBlock> GeneralGetterList; //Not guaranteed to keep anything

        //PRIVATE MEMBERS
        private List<CommandGroup> _command_groups;
        //used to prevent a single block from being assigned to multiple command groups
        private HashSet<long> _found_block_ids; 

        /*****************************************************************************/
        //                              MINOR DATA STRUCTURES
        class CommandInput
        {
            public float pitch;
            public float yaw;
            public float roll;
            public float up_down;
            public float left_right;
            public float forward_back;
        }

        enum ArticulationType
        {
            rotor,
            piston
        }

        enum ControlType
        {
            pitch,
            yaw,
            roll,
            up_down,
            left_right,
            forward_back
        }





        /*****************************************************************************/
        //                     PROGRAM CLASS CONSTRUCTOR AND METHODS
        //METHOD: Program/
        public Program()
        {
            GeneralGetterList = new List<IMyTerminalBlock>();
            _command_groups = new List<CommandGroup>();
            _found_block_ids = null;

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        //METHOD: InitializeGroups
        private void InitializeGroups()
        {
            GeneralGetterList.Clear();
            _command_groups.Clear();
            ClearFoundBlocks();
            _time_since_last_refresh = 0;

            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();

            GridTerminalSystem.GetBlockGroups(groups, 
                group => System.Text.RegularExpressions.Regex.IsMatch(
                    group.Name, 
                    COMMAND_GROUP_NAME_PATTERN, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            );

            foreach (IMyBlockGroup group in groups)
            {
                _command_groups.Add(new CommandGroup(group, this));
            }
        }

        //METHOD: MAIN
        public void Main(string argument, UpdateType updateSource)
        {
            _time_since_last_refresh += Runtime.TimeSinceLastRun.TotalSeconds;
            if (_time_since_last_refresh >= REFRESH_TIME)
            {
                InitializeGroups();
            }

            foreach (CommandGroup group in _command_groups)
            {
                if (group.IsBeingControlled())
                {
                    Runtime.UpdateFrequency |= UpdateFrequency.Once;
                    group.Run();
                }
                //TODO: Set velocities to 0 if a group is stops being controlled
                //TODO: Add support for wheels

                if (!group.IsValid)
                {
                    LogError(group.Errors, group.Name);
                }
            }

            PrintDiagnostics();
        }


        //FUNCTIONS THAT PREVENT BLOCKS BEING IN MULTIPLE GROUPS
        void ClearFoundBlocks()
        {
            if (_found_block_ids != null)
            {
                _found_block_ids.Clear();
            }
        }

        bool IsBlockAlreadyFound(long id)
        {
            if (!PREVENT_ASSIGNMENT_OF_BLOCKS_TO_MULTIPLE_COMMAND_GROUPS)
            {
                return false;
            }

            if (_found_block_ids == null)
            {
                _found_block_ids = new HashSet<long>();
            } 

            return !_found_block_ids.Add(id);
        }

        //OUTPUT FUNCTIONS

        private void PrintDiagnostics()
        {
            Echo($"Time Since Last Refresh: {_time_since_last_refresh:0.0}s");
            Echo($"{_command_groups.Count} command groups detected");
            Echo($"{_command_groups.Count(group => group.IsBeingControlled())} groups in use");
            Echo($"{_command_groups.Count(group => !group.IsValid)} invalid groups detected");
            Echo($"Script last ran in {Runtime.LastRunTimeMs:0.0000} ms");
        }
        //TODO: Make a more persistent log of messages and errors
        void LogWarning(string error, string caller)
        {
            Echo($"Warning from {caller}: {error}");
        }

        void LogError(string error, string caller)
        {
            Echo($"Error from {caller}: {error}");
        }

        void LogMessage(string msg)
        {
            Echo(msg);
        }
    }
}
