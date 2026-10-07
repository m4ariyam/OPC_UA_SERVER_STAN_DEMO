using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
public class MyNodeManager : CustomNodeManager2
{
    private ushort _nsIndex;
    private readonly List<BaseDataVariableState> _randomizedVariables = new();
    private readonly Random _rand = new();
    private BaseDataVariableState _startLed;
    private BaseDataVariableState _sprayerPump;
    private List<BaseDataVariableState> _opcBtnHandler = new();
    private BaseDataVariableState _read1;

    public MyNodeManager(IServerInternal server, ApplicationConfiguration config)
        : base(server, config, "http://yourcompany.com/UA/MyOpcUaServer")
    {
        SystemContext.NodeIdFactory = this;
        var timer = new System.Timers.Timer(10000);
        timer.Elapsed += (s, e) => RandomizeValues();
        timer.AutoReset = true;
        timer.Start();

        // Таймер для триггерной логики
        var logicTimer = new System.Timers.Timer(200);
        logicTimer.Elapsed += (s, e) => TriggerLogic();
        logicTimer.AutoReset = true;
        logicTimer.Start();
    }

    private void TriggerLogic()
    {
        lock (Lock)
        {
            if (_opcBtnHandler.Count < 3 * 13 || _startLed == null || _sprayerPump == null || _read1 == null)
                return;

            // [0,11], [1,11], [2,11] — логика для StartLed и Read[1]
            bool startPressed = GetBool(0, 11) || GetBool(1, 11) || GetBool(2, 11);

            _startLed.Value = startPressed;
            _startLed.Timestamp = DateTime.UtcNow;
            _startLed.ClearChangeMasks(SystemContext, false);

            _read1.Value = (short)(startPressed ? 1 : 0);
            _read1.Timestamp = DateTime.UtcNow;
            _read1.ClearChangeMasks(SystemContext, false);

            // [0,12], [1,12], [2,12] — логика для SprayerPump
            bool pumpPressed = GetBool(0, 12) || GetBool(1, 12) || GetBool(2, 12);
            _sprayerPump.Value = pumpPressed;
            _sprayerPump.Timestamp = DateTime.UtcNow;
            _sprayerPump.ClearChangeMasks(SystemContext, false);
        }
    }


    private bool GetBool(int row, int col)
    {
        int index = row * 13 + col;
        if (_opcBtnHandler.Count > index && _opcBtnHandler[index].Value is bool val)
            return val;
        return false;
    }

    private void RandomizeValues()
    {
        lock (Lock)
        {
            if (_randomizedVariables.Count == 0)
                return;

            var allowedVariables = _randomizedVariables
                .Where(v => v.BrowseName.Name != "AutomatModes" && !v.BrowseName.Name.StartsWith("Opc") && v.BrowseName.Name != "Read[1]"
                && v.BrowseName.Name != "SprayerPump" && v.BrowseName.Name != "StartedLed")

                .ToList();

            if (allowedVariables.Count == 0)
                return;

            int count = _rand.Next(2, Math.Min(11, allowedVariables.Count + 1));
            var selected = allowedVariables.OrderBy(x => _rand.Next()).Take(count);

            Console.Clear();

            foreach (var variable in selected)
            {
                var dataType = variable.DataType;
                object newValue = variable.Value;

                if (dataType == DataTypeIds.Boolean)
                {
                    bool current = variable.Value is bool b && b;
                    newValue = !current;
                }
                else if (dataType == DataTypeIds.Int16 ||
                         dataType == DataTypeIds.UInt16 ||
                         dataType == DataTypeIds.Int32 ||
                         dataType == DataTypeIds.UInt32)
                {
                    newValue = (short)_rand.Next(1, 201);
                }
                else if (dataType == DataTypeIds.Float)
                {
                    newValue = (float)_rand.Next(1, 201);
                }
                else
                {
                    continue;
                }

                variable.Value = newValue;
                variable.Timestamp = DateTime.UtcNow;
                variable.ClearChangeMasks(SystemContext, false);

                Console.WriteLine($"{variable.BrowseName.Name} = {newValue}");
            }
        }
    }

    // Изменённый метод для отслеживания важных переменных
    private void AddBoolVariable(NodeState parent, string baseNodeIdStr, string baseBrowseName)
    {
        var node = new BaseDataVariableState(parent)
        {
            NodeId = new NodeId(baseNodeIdStr, _nsIndex),
            BrowseName = new QualifiedName(baseBrowseName, _nsIndex),
            DisplayName = baseBrowseName,
            DataType = DataTypeIds.Boolean,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Value = false,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };
        parent.AddChild(node);
        AddPredefinedNode(SystemContext, node);

        if (baseBrowseName == "StartLed")
            _startLed = node;
        else if (baseBrowseName == "SprayerPump")
            _sprayerPump = node;

        if (!baseBrowseName.Contains("Opc"))
            _randomizedVariables.Add(node);
    }

    // Изменённый метод для матрицы булевых кнопок
    private void AddBoolMatrixElements(NodeState parent, string baseNodeIdStr, string baseBrowseName, int dim0, int dim1)
    {
        for (int i = 0; i < dim0; i++)
        {
            for (int j = 0; j < dim1; j++)
            {
                string nodeIdStr = $"|var|{baseNodeIdStr}[{i},{j}]";
                string browseName = $"{baseBrowseName}[{i},{j}]";

                var node = new BaseDataVariableState(parent)
                {
                    NodeId = new NodeId(nodeIdStr, _nsIndex),
                    BrowseName = new QualifiedName(browseName, _nsIndex),
                    DisplayName = browseName,
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = false,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };
                parent.AddChild(node);
                AddPredefinedNode(SystemContext, node);

                if (baseBrowseName.Contains("OpcBtnHandler"))
                    _opcBtnHandler.Add(node);

                if (!baseBrowseName.Contains("Opc"))
                    _randomizedVariables.Add(node);
            }
        }
    }
    private void AddUShortArrayElements(NodeState parent, string baseNodeIdStr, string baseBrowseName, int dim0)
    {
        for (int i = 0; i < dim0; i++)
        {
            string nodeIdStr = $"|var|{baseNodeIdStr}[{i}]";
            string browseName = $"{baseBrowseName}[{i}]";

            var node = new BaseDataVariableState(parent)
            {
                NodeId = new NodeId(nodeIdStr, _nsIndex),
                BrowseName = new QualifiedName(browseName, _nsIndex),
                DisplayName = browseName,
                DataType = DataTypeIds.Int16,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                Value = (short)0,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };
            parent.AddChild(node);
            AddPredefinedNode(SystemContext, node);
            if (browseName == "Read[1]")
                _read1 = node;


            if (!baseBrowseName.Contains("OpcArr"))
                _randomizedVariables.Add(node);
        }
    }

    private void AddShortArrayElements(NodeState parent, string baseNodeIdStr, string baseBrowseName, int dim0)
    {
        for (int i = 0; i < dim0; i++)
        {
            string nodeIdStr = $"|var|{baseNodeIdStr}[{i}]";
            string browseName = $"{baseBrowseName}[{i}]";

            var node = new BaseDataVariableState(parent)
            {
                NodeId = new NodeId(nodeIdStr, _nsIndex),
                BrowseName = new QualifiedName(browseName, _nsIndex),
                DisplayName = browseName,
                DataType = DataTypeIds.Int16,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                Value = (short)0,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };
            parent.AddChild(node);
            AddPredefinedNode(SystemContext, node);

            if (!baseBrowseName.Contains("OpcArr"))
                _randomizedVariables.Add(node);
        }
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        _nsIndex = Server.NamespaceUris.GetIndexOrAppend("http://yourcompany.com/UA/MyOpcUaServer");

        var root = new FolderState(null)
        {
            NodeId = new NodeId("MyRootFolder", _nsIndex),
            BrowseName = new QualifiedName("MyRootFolder", _nsIndex),
            DisplayName = "MyRootFolder",
            TypeDefinitionId = ObjectTypeIds.FolderType
        };

        if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var refs))
        {
            refs = new List<IReference>();
            externalReferences[ObjectIds.ObjectsFolder] = refs;
        }
        refs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, root.NodeId));

        

        AddPredefinedNode(SystemContext, root);

        AddAxisWithNState(root, "Axis1");
        AddAxisWithNState(root, "Axis2");
        AddAxisWithNState(root, "Axis3");
        AddAxisWithNState(root, "Axis4");
        AddAxisWithNState(root, "Axis5");

        AddShortVariable(root, "|var|LS.Application.PLC_PRG.AutomatModes", "AutomatModes");
        AddShortVariable(root, "|var|LS.Application.PLC_PRG.Modes", "Modes");
        var modesVar = (BaseDataVariableState)FindPredefinedNode(
        new NodeId("|var|LS.Application.PLC_PRG.Modes", _nsIndex),
        typeof(BaseDataVariableState)
        );
            if (modesVar != null)
            {
                modesVar.Value = (short)1;
                modesVar.Timestamp = DateTime.UtcNow;
                modesVar.ClearChangeMasks(SystemContext, false);
            }

        AddShortVariable(root, "|var|LS.Application.PLC_PRG.CurrentTooth", "CurrentTooth");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.StartLed", "StartLed");
        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.ProtokInd", "ProtokInd");
        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.ProtokPCh", "ProtokPCh");
        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.ProtokTSU", "ProtokTSU");
        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.OutBlock", "OutBlock");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LimitIndBack", "LimitIndBack");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LimitIndDown", "LimitIndDown");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LimitIndForw", "LimitIndForw");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LimitIndLeft", "LimitIndLeft");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LimitIndRight", "LimitIndRight");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LimitIndUp", "LimitIndUp");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LimitClampDown", "LimitClampDown");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LimitClampUp", "LimitClampUp");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.LowerLimit", "LowerLimit");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.MiddleLimit", "MiddleLimit");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.HighestLimit", "HighestLimit");

        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.ClampUp", "ClampUp");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.ClampDown", "ClampDown");

        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.IndUp", "IndUp");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.IndDown", "IndDown");

        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.IndLeft", "IndLeft");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.IndRight", "IndRight");

        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.IndForw", "IndForw");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.IndBack", "IndBack");

        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.Rotleft", "RotLeft");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.RotRight", "RotRight");

        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.SprayerPump", "SprayerPump");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.Sprayer", "Sprayer");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.Automat", "Automat");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.Start", "Start");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.StopHeater", "Stop");
        AddBoolVariable(root, "|var|LS.Application.IoConfig_Globals_Mapping.ProgrammBlock", "ProgrammBlock");

        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.calibration2", "calibration2");
        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.calibration3", "calibration3");
        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.calibration4", "calibration4");

        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.SwitchDisabled", "SwitchDisabled");

        AddShortMatrixElements(root, "LS.Application.PLC_PRG.OpcArr", "OpcArr", 20, 8);
        AddShortArrayElements(root, "LS.Application.PLC_PRG.OpcArr2", "OpcArr2", 15);
        AddBoolMatrixElements(root, "LS.Application.PLC_PRG.OpcArrChBxs", "OpcArrChBxs", 20, 1);
        AddBoolArrayElements(root, "LS.Application.PLC_PRG.OpcArrChBxs2", "OpcArrChBxs2", 7);
        AddBoolMatrixElements(root, "LS.Application.PLC_PRG.OpcArrCmBxs", "OpcArrCmBxs", 20, 2);
        AddBoolArrayElements(root, "LS.Application.PLC_PRG.OpcArrCmBxs2", "OpcArrCmBxs2", 3);
        AddBoolMatrixElements(root, "LS.Application.PLC_PRG.OpcBtnHandler", "OpcBtnHandler", 3, 13);
        AddFloatMatrixElements(root, "LS.Application.PLC_PRG.OpcArrOptions", "OpcArrOptions", 18, 2);

        AddFloatVariable(root, "|var|LS.Application.PLC_PRG.CurrentMoveSpeed", "CurrentMoveSpeed");
        AddFloatVariable(root, "|var|LS.Application.PLC_PRG.CurrentOutput", "CurrentOutput");
        AddFloatVariable(root, "|var|LS.Application.PLC_PRG.CurrentRotSpeed", "CurrentRotSpeed");
        AddBoolVariable(root, "|var|LS.Application.PLC_PRG.bLinkStable", "bLinkStable");

        AddFloatVariable(root, "|var|LS.Application.PLC_PRG.fActualPosition_Axis2", "fActualPosition_Axis2");
        AddFloatVariable(root, "|var|LS.Application.PLC_PRG.fActualPosition_Axis3", "fActualPosition_Axis3");
        AddFloatVariable(root, "|var|LS.Application.PLC_PRG.fActualPosition_Axis4", "fActualPosition_Axis4");

        AddFloatVariable(root, "|var|LS.Application.PLC_PRG.Rotation", "Rotation");

        AddUShortArrayElements(root, "LS.Application.PLC_PRG.Read", "Read", 20);
        AddUShortArrayElements(root, "LS.Application.PLC_PRG.WriteControl", "WriteControl", 1);
        AddUShortArrayElements(root, "LS.Application.PLC_PRG.WriteOptions", "WriteOptions", 3);
    }
    private void AddAxisWithNState(NodeState parent, string axisName)
    {
        var axisFolder = new FolderState(parent)
        {
            NodeId = new NodeId($"|var|LS.Application.IoConfig_Globals.{axisName}", _nsIndex),
            BrowseName = new QualifiedName(axisName, _nsIndex),
            DisplayName = axisName,
            TypeDefinitionId = ObjectTypeIds.BaseObjectType
        };
        parent.AddChild(axisFolder);
        AddPredefinedNode(SystemContext, axisFolder);

        var stateNode = new BaseDataVariableState(axisFolder)
        {
            NodeId = new NodeId($"|var|LS.Application.IoConfig_Globals.{axisName}.nAxisState", _nsIndex),
            BrowseName = new QualifiedName("nAxisState", _nsIndex),
            DisplayName = "nAxisState",
            DataType = DataTypeIds.Int16,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Value = (short)0,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };
        var stateNode2 = new BaseDataVariableState(axisFolder)
        {
            NodeId = new NodeId($"|var|LS.Application.IoConfig_Globals.{axisName}.bCommunication", _nsIndex),
            BrowseName = new QualifiedName("bCommunication", _nsIndex),
            DisplayName = "bCommunication",
            DataType = DataTypeIds.Boolean,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Value = false,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };
        var stateNode3 = new BaseDataVariableState(axisFolder)
        {
            NodeId = new NodeId($"|var|LS.Application.IoConfig_Globals.{axisName}.fActVelocity", _nsIndex),
            BrowseName = new QualifiedName("fActVelocity", _nsIndex),
            DisplayName = "fActVelocity",
            DataType = DataTypeIds.Float,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Value = 0.0f,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };
        axisFolder.AddChild(stateNode);
        AddPredefinedNode(SystemContext, stateNode);
        axisFolder.AddChild(stateNode2);
        AddPredefinedNode(SystemContext, stateNode2);
        axisFolder.AddChild(stateNode3);
        AddPredefinedNode(SystemContext, stateNode3);
    }

    private void AddShortVariable(NodeState parent, string nodeIdStr, string browseName)
    {
        var node = new BaseDataVariableState(parent)
        {
            NodeId = new NodeId(nodeIdStr, _nsIndex),
            BrowseName = new QualifiedName(browseName, _nsIndex),
            DisplayName = browseName,
            DataType = DataTypeIds.Int16,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Value = (short)0,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };
        parent.AddChild(node);
        AddPredefinedNode(SystemContext, node);
    }



   


    private void AddShortMatrixElements(NodeState parent, string baseNodeIdStr, string baseBrowseName, int dim0, int dim1)
    {
        for (int i = 0; i < dim0; i++)
        {
            for (int j = 0; j < dim1; j++)
            {
                string nodeIdStr = $"|var|{baseNodeIdStr}[{i},{j}]";
                string browseName = $"{baseBrowseName}[{i},{j}]";

                var node = new BaseDataVariableState(parent)
                {
                    NodeId = new NodeId(nodeIdStr, _nsIndex),
                    BrowseName = new QualifiedName(browseName, _nsIndex),
                    DisplayName = browseName,
                    DataType = DataTypeIds.Int16,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = (short)0,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };
                parent.AddChild(node);
                AddPredefinedNode(SystemContext, node);
            }
        }
    }
    private void AddFloatVariable(NodeState parent, string nodeIdStr, string browseName)
    {
        var node = new BaseDataVariableState(parent)
        {
            NodeId = new NodeId(nodeIdStr, _nsIndex),
            BrowseName = new QualifiedName(browseName, _nsIndex),
            DisplayName = browseName,
            DataType = DataTypeIds.Float,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Value = 0.0f,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };
        parent.AddChild(node);
        AddPredefinedNode(SystemContext, node);
    }
    
   

    private void AddBoolArrayElements(NodeState parent, string baseNodeIdStr, string baseBrowseName, int dim0)
    {
        for (int i = 0; i < dim0; i++)
        {
            string nodeIdStr = $"|var|{baseNodeIdStr}[{i}]";
            string browseName = $"{baseBrowseName}[{i}]";

            var node = new BaseDataVariableState(parent)
            {
                NodeId = new NodeId(nodeIdStr, _nsIndex),
                BrowseName = new QualifiedName(browseName, _nsIndex),
                DisplayName = browseName,
                DataType = DataTypeIds.Boolean,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                Value = false,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };
            parent.AddChild(node);
            AddPredefinedNode(SystemContext, node);
        }
    }

    private void AddFloatMatrixElements(NodeState parent, string baseNodeIdStr, string baseBrowseName, int dim0, int dim1)
    {
        for (int i = 0; i < dim0; i++)
        {
            for (int j = 0; j < dim1; j++)
            {
                string nodeIdStr = $"|var|{baseNodeIdStr}[{i},{j}]";
                string browseName = $"{baseBrowseName}[{i},{j}]";

                var node = new BaseDataVariableState(parent)
                {
                    NodeId = new NodeId(nodeIdStr, _nsIndex),
                    BrowseName = new QualifiedName(browseName, _nsIndex),
                    DisplayName = browseName,
                    DataType = DataTypeIds.Float,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0.0f,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };
                parent.AddChild(node);
                AddPredefinedNode(SystemContext, node);
            }
        }
    }
}
