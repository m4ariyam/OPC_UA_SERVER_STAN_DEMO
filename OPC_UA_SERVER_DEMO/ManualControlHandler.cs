using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPC_UA_SERVER_DEMO
{
    public class ManualControlHandler
    {
        private readonly List<BaseDataVariableState> _allVariables;
        private readonly object _lock;

        public ManualControlHandler(List<BaseDataVariableState> allVariables, object nodeLock)
        {
            _allVariables = allVariables;
            _lock = nodeLock;
        }

        public void Run()
        {
            new Thread(() =>
            {
                while (true)
                {
                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                    if (key.Modifiers == ConsoleModifiers.Shift && key.Key == ConsoleKey.Enter)
                    {
                        HandleManualMode();
                    }
                }
            })
            { IsBackground = true }.Start();
        }

        private void HandleManualMode()
        {
            while (true)
            {
                Console.Clear();
                Dictionary<string, BaseDataVariableState> tagMap = new();

                lock (_lock)
                {
                    int tagNumber = 1;
                    foreach (var group in _allVariables.GroupBy(v => v.BrowseName.Name.Split('[')[0]))
                    {
                        Console.WriteLine($"{tagNumber}. {group.Key}");
                        int subIndex = 1;
                        foreach (var variable in group)
                        {
                            string subKey = $"{tagNumber}.{subIndex}";
                            Console.WriteLine($"	{subKey} - {variable.BrowseName.Name} = {variable.Value}");
                            tagMap[subKey] = variable;
                            subIndex++;
                        }
                        tagNumber++;
                    }
                }

                Console.Write("Введите номер тега (или пусто для выхода): ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) break;

                if (tagMap.TryGetValue(input.Trim(), out var selectedTag))
                {
                    Console.Write("Введите значение: ");
                    string? valueInput = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(valueInput))
                    {
                        object? parsed = ParseValue(selectedTag, valueInput);
                        if (parsed != null)
                        {
                            lock (_lock)
                            {
                                selectedTag.Value = parsed;
                                selectedTag.Timestamp = DateTime.UtcNow;
                                selectedTag.ClearChangeMasks(null, false);
                            }
                        }
                    }
                }
            }
        }

        private object? ParseValue(BaseDataVariableState variable, string input)
        {
            try
            {
                var type = variable.DataType;
                return type.Identifier switch
                {
                    var id when id.Equals(DataTypeIds.Boolean.Identifier) => bool.Parse(input),
                    var id when id.Equals(DataTypeIds.Int16.Identifier) => short.Parse(input),
                    var id when id.Equals(DataTypeIds.UInt16.Identifier) => ushort.Parse(input),
                    var id when id.Equals(DataTypeIds.Float.Identifier) => float.Parse(input),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
