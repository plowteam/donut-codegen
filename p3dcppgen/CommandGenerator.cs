using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DonutCodeGen
{
    class CommandGenerator
    {
        [JsonConverter(typeof(StringEnumConverter))]
        enum ValueType
        {
            String,
            Int,
            Int64,
            Float,
        }

        private static readonly string[] defaultValues =
        {
            "\"\"",
            "0",
            "0",
            "0.0f",
        };

        private static readonly string[] valueTypes =
{
            "const std::string&",
            "int32_t",
            "int64_t",
            "float",
        };

        private static readonly string[] stringConverters =
        {
            "",
            "Commands::StringToInt",
            "Commands::StringToInt64",
            "Commands::StringToFloat",
        };

        class Params
        {
            public int min = 0;
            public List<List<ValueType>> types = new List<List<ValueType>>();
            public string help = "";

            public override string ToString()
            {
                return $"{min} {string.Join(" ", types.First())} \"{help}\"";
            }
        }

        public static int Process(string inputFile, string outputPath)
        {
            int exitCode = 0;

            var functions = new StringWriter();
            var commands = new StringWriter();
            var commandMap = new StringWriter();

            var commandDict = JsonConvert.DeserializeObject<Dictionary<string, Params>>(File.ReadAllText(inputFile));

            foreach (var command in commandDict)
            {
                var funcName = command.Key;
                var helpText = command.Value.help;

                var types = new List<List<ValueType>>();

                if (command.Value.types.Count == 0 || command.Value.types.All(x => x.Count == 0))
                {
                    commands.WriteLine($"    static bool Command_{funcName}(const std::string& line)\n    {{");
                    commands.WriteLine($"        if (!line.empty()) return false;");
                    commands.WriteLine("");
                    commands.WriteLine($"        Impl_{funcName}();");
                    commands.WriteLine($"        return true;");
                    commands.WriteLine($"    }}");
                    commands.WriteLine("");

                    functions.WriteLine($"    static void Impl_{funcName}() {{ GameCommands::{funcName}(); }}");
                }
                else if (command.Value.types.Count == 1)
                {
                    var paramTypes = command.Value.types.First();
                    var minParams = command.Value.min;
                    var maxParams = paramTypes.Count;

                    types.Add(paramTypes);

                    var paramStrings =
                        Enumerable.Range(0, minParams).Select(x => $"{valueTypes[(int)paramTypes[x]]} param{x}").
                        Concat(Enumerable.Range(minParams, maxParams - minParams).Select(x => $"{valueTypes[(int)paramTypes[x]]} param{x} = {defaultValues[(int)paramTypes[x]]}"));

                    functions.WriteLine($"    static void Impl_{funcName}({string.Join(", ", paramStrings)}) {{ GameCommands::{funcName}({string.Join(", ", Enumerable.Range(0, maxParams).Select(x => $"param{x}"))}); }}");

                    commands.WriteLine($"    static bool Command_{funcName}(const std::string& line)\n    {{");

                    commands.WriteLine("        std::vector<std::string> params;");
                    commands.WriteLine($"        if (!Commands::SplitParams(line, params, {maxParams})) return false;");
                    commands.WriteLine();
                    commands.WriteLine("        size_t numParams = params.size();");
                    if (minParams > 0) commands.WriteLine($"        if (numParams < {minParams}) return false;");
                    commands.WriteLine();

                    var paramNum = 0;
                    var functionParams = new List<string>();
                    var newLine = false;

                    foreach (var paramType in paramTypes)
                    {
                        if (paramType != ValueType.String)
                        {
                            newLine = true;
                            functionParams.Add($"param{paramNum}");

                            if (paramNum >= minParams)
                            {
                                commands.WriteLine($"        {valueTypes[(int)paramType]} param{paramNum} = {defaultValues[(int)paramType]};");
                                commands.WriteLine($"        if (numParams > {paramNum})");
                                commands.WriteLine($"            if (!{stringConverters[(int)paramType]}(params[{paramNum}], param{paramNum})) return false;");
                            }
                            else
                            {
                                commands.WriteLine($"        {valueTypes[(int)paramType]} param{paramNum};");
                                commands.WriteLine($"        if (!{stringConverters[(int)paramType]}(params[{paramNum}], param{paramNum})) return false;");
                            }
                        }
                        else
                        {
                            if (paramNum >= minParams)
                            {
                                functionParams.Add($"(numParams > {paramNum}) ? params[{paramNum}] : \"\"");
                            }
                            else
                            {
                                functionParams.Add($"params[{paramNum}]");
                            }
                        }
                        paramNum++;
                    }

                    if (newLine) commands.WriteLine();
                    commands.WriteLine($"        Impl_{funcName}({string.Join(", ", functionParams)});");
                    commands.WriteLine($"        return true;");
                    commands.WriteLine("    }\n");
                }
                else
                {
                    var maxParams = command.Value.types.Max(x => x.Count);

                    commands.WriteLine($"    static bool Command_{funcName}(const std::string& line)\n    {{");

                    commands.WriteLine("        std::vector<std::string> params;");
                    commands.WriteLine($"        if (!Commands::SplitParams(line, params, {maxParams})) return false;");
                    commands.WriteLine();
                    commands.WriteLine("        size_t numParams = params.size();");
                    commands.WriteLine("        switch (numParams)");
                    commands.WriteLine("        {");

                    foreach (var paramTypes in command.Value.types)
                    {
                        types.Add(paramTypes);

                        var numParams = paramTypes.Count;
                        var paramStrings =
                            Enumerable.Range(0, numParams).Select(x => $"{valueTypes[(int)paramTypes[x]]} param{x}");

                        functions.WriteLine($"    static void Impl_{funcName}({string.Join(", ", paramStrings)}) {{ GameCommands::{funcName}({string.Join(", ", Enumerable.Range(0, numParams).Select(x => $"param{x}"))}); }}");

                        commands.WriteLine($"            case {numParams}:");
                        commands.WriteLine($"            {{");

                        var paramNum = 0;
                        var functionParams = new List<string>();
                        var newLine = false;

                        foreach (var paramType in paramTypes)
                        {
                            if (paramType != ValueType.String)
                            {
                                newLine = true;
                                commands.WriteLine($"                {valueTypes[(int)paramType]} param{paramNum};");
                                commands.WriteLine($"                if (!{stringConverters[(int)paramType]}(params[{paramNum}], param{paramNum})) return false;");
                                functionParams.Add($"param{paramNum}");
                            }
                            else
                            {
                                functionParams.Add($"params[{paramNum}]");
                            }
                            paramNum++;
                        }

                        if (newLine) commands.WriteLine();
                        commands.WriteLine($"                Impl_{funcName}({string.Join(", ", functionParams)});");
                        commands.WriteLine($"                return true;");
                        commands.WriteLine($"            }}");
                    }

                    commands.WriteLine("        }");

                    commands.WriteLine();

                    commands.WriteLine($"        return false;");
                    commands.WriteLine("    }\n");
                }

                if (types.Count == 0)
                {
                    commandMap.WriteLine($"        {{ \"{funcName}\", Command {{ &Command_{funcName}, \"{helpText}\" }} }},");
                }
                else
                {
                    commandMap.WriteLine($"        {{ \"{funcName}\", Command {{ &Command_{funcName}, \"{helpText}\", {{ {string.Join(", ", types.Select(x => $"{{ {string.Join(", ", x.Select(t => $"ParamType::{t}"))} }}"))} }} }} }},");
                }
            }

            using (var writer = File.CreateText(Path.Combine(outputPath, "commands.generated.cpp")))
            {
                writer.WriteLine("#include \"Commands.h\"");

                writer.WriteLine();

                writer.WriteLine("namespace Donut");
                writer.WriteLine("{");
                writer.WriteLine(functions.ToString());

                writer.Write(commands.ToString());

                writer.WriteLine("    std::unordered_map<std::string, Command> Commands::_namedCommands =");
                writer.WriteLine("    {");
                writer.Write(commandMap.ToString());
                writer.WriteLine("    };");

                writer.WriteLine("}");
            }

            return exitCode;
        }
    }
}
