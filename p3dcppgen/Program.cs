using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace p3dcppgen
{
	class Program
	{
		static readonly string classTemplate = @"
	class {0}
	{{
	public:

		{0}();

		{1}
	private:

		{2}
	}};";

		static readonly string ctorTemplate = @"
	{0}::{0}(const P3DChunk& chunk)
	{{
		assert(chunk.IsType(ChunkType::{0}));

		MemoryStream stream(chunk.GetData());

		{1}{2}	}}";

		static readonly string switchCaseTemplate = @"
		for (auto const& child : chunk.GetChildren())
		{{
			MemoryStream data(child->GetData());

			switch (child->GetType())
			{{
				{1}				default:
					std::cout << ""[{0}] Unexpected Chunk: "" << child->GetType() << ""\n"";
			}}
		}}";

        static readonly Dictionary<string, string> typeDict = new Dictionary<string, string>
        {
            { "s8", "int8_t" },
            { "s16", "int16_t" },
            { "s32", "int32_t" },
            { "s64", "int64_t" },

            { "u8", "uint8_t" },
            { "u16", "uint16_t" },
            { "u32", "uint32_t" },
            { "u64", "uint64_t" },

            { "bool", "bool" },
            { "float", "float" },
            { "string", "std::string" },

            { "vec2", "glm::vec2" },
            { "vec3", "glm::vec3" },
            { "vec4", "glm::vec4" },
            { "quat", "glm::quat" },
            { "mat4", "glm::mat4" },
        };

        static string GetNativeType(string s) => typeDict.TryGetValue(s, out var v) ? v : s;

		static void Main(string[] args)
		{
			var defObject = JObject.Parse(File.ReadAllText("def.json"));
			var headersb = new StringBuilder();
			var cpprb = new StringBuilder();
			var forwardDecl = new List<string>();

			foreach (var classToken in defObject)
			{
				forwardDecl.Add(classToken.Key);

				var readers = new IndentedTextWriter(new StringWriter(), "\t") { Indent = 2 };
				var publicBlock = new IndentedTextWriter(new StringWriter(), "\t") { Indent = 2 };
				var privateBlock = new IndentedTextWriter(new StringWriter(), "\t") { Indent = 2 };
				var caseBlock = new IndentedTextWriter(new StringWriter(), "\t") { Indent = 4 };

				foreach (var propToken in classToken.Value)
				{
					var propObject = propToken.ToObject<JProperty>();

					switch (propObject.Name)
					{
							case "child-buffer":
							{
								foreach (var propToken2 in propToken.Values())
								{
									var propObject2 = propToken2.ToObject<JProperty>();
									var funcName = Char.ToUpperInvariant(propObject2.Name[0]) + propObject2.Name.Substring(1);

									var chunk = propObject2.Value.ToObject<JObject>()["chunk"];
									var type = GetNativeType(propObject2.Value.ToObject<JObject>()["type"].ToString());
									var channels = propObject2.Value.ToObject<JObject>()["channels"];

									bool hasChannels = false;
									if (channels != null && (bool)channels)
									{
										hasChannels = true;
									}

									if (hasChannels)
									{
										publicBlock.WriteLine($"const std::vector<{type}>& Get{funcName}(size_t channel) const {{ return _{propObject2.Name}.at(channel); }}");
										privateBlock.WriteLine($"std::vector<std::vector<{type}>> _{propObject2.Name};");

										caseBlock.WriteLine($@"case ChunkType::{chunk}:
				{{
					uint32_t len = data.Read<uint32_t>();
					uint32_t channel = data.Read<uint32_t>();
					_{propObject2.Name}.resize(channel + 1);
					data.ReadBytes(reinterpret_cast<uint8_t*>(_{propObject2.Name}[channel].data()), len * sizeof({type}));
					break;
				}}");
									}
									else
									{
										publicBlock.WriteLine($"const std::vector<{type}>& Get{funcName}() const {{ return _{propObject2.Name}; }}");
										privateBlock.WriteLine($"std::vector<{type}> _{propObject2.Name};");

										caseBlock.WriteLine($@"case ChunkType::{chunk}:
				{{
					uint32_t len = data.Read<uint32_t>();
					_{propObject2.Name}.resize(len);
					data.ReadBytes(reinterpret_cast<uint8_t*>(_{propObject2.Name}.data()), len * sizeof({type}));
					break;
				}}");
									}
								}
								break;
							}
						case "child":
							{
								foreach (var propToken2 in propToken.Values())
								{
									var propObject2 = propToken2.ToObject<JProperty>();
									var funcName = Char.ToUpperInvariant(propObject2.Name[0]) + propObject2.Name.Substring(1);

									publicBlock.WriteLine($"const unique_ptr<{propObject2.Value}>& Get{funcName}() const {{ return _{propObject2.Name}; }}");
									privateBlock.WriteLine($"unique_ptr<{propObject2.Value}> _{propObject2.Name};");

									caseBlock.WriteLine($@"case ChunkType::{propObject2.Value}:
				{{
					_{propObject2.Name} = std::make_unique<{propObject2.Value}>(*child);
					break;
				}}");
								}
								break;
							}
						case "children":
							{
								foreach (var propToken2 in propToken.Values())
								{
									var propObject2 = propToken2.ToObject<JProperty>();
									var funcName = Char.ToUpperInvariant(propObject2.Name[0]) + propObject2.Name.Substring(1);

									publicBlock.WriteLine($"const std::vector<unique_ptr<{propObject2.Value}>>& Get{funcName}() const {{ return _{propObject2.Name}; }}");
									privateBlock.WriteLine($"std::vector<unique_ptr<{propObject2.Value}>> _{propObject2.Name};");

									caseBlock.WriteLine($@"case ChunkType::{propObject2.Value}:
				{{
					_{propObject2.Name}.push_back(std::make_unique<{propObject2.Value}>(*child));
					break;
				}}");
								}
								break;
							}
						default:
							{
								if (propObject.Value.Type != JTokenType.String) break;

								var funcName = Char.ToUpperInvariant(propObject.Name[0]) + propObject.Name.Substring(1);
                                var type = GetNativeType(propObject.Value.ToString());
                                var readerName = propObject.Value.ToString() == "string" ? "LPString" : $"<{type}>";

                                readers.WriteLine($"_{propObject.Name} = stream.Read{readerName}();");
								publicBlock.WriteLine($"const {type}& Get{funcName}() const {{ return _{propObject.Name}; }}");
								privateBlock.WriteLine($"{type} _{propObject.Name};");
								break;
							}
					}               
				}

				var switchBlock = new StringWriter();
				if (!string.IsNullOrEmpty(caseBlock.InnerWriter.ToString()))
				{
					switchBlock.WriteLine(switchCaseTemplate, classToken.Key, caseBlock.InnerWriter);
				}

				headersb.AppendLine(string.Format(classTemplate,
					classToken.Key,
					publicBlock.InnerWriter, 
					privateBlock.InnerWriter));

				cpprb.AppendLine(string.Format(ctorTemplate,
					classToken.Key,
					readers.InnerWriter,
					switchBlock));
			}

			var headerIncludes = new[]
			{
				"string",
				"memory",
				"vector",
				"glm/vec2.hpp",
				"glm/vec3.hpp",
				"glm/vec4.hpp",
				"glm/gtc/quaternion.hpp",
				"glm/gtc/mat4x4.hpp",
			};

			var cppIncludes = new[]
			{
				"Core/MemoryStream.h",
				"iostream",
			};

			using (var writer = File.CreateText("p3d.generated.h"))
			{
				foreach (var inc in headerIncludes)
				{
					writer.WriteLine($"#include <{inc}>");
				}

				writer.WriteLine();

				writer.WriteLine("namespace Donut::P3D");
				writer.WriteLine("{");
				foreach (var decl in forwardDecl)
				{
					writer.WriteLine($"\tclass {decl};");
				}
				writer.Write(headersb.ToString());
				writer.WriteLine("}");
			}

			using (var writer = File.CreateText("p3d.generated.cpp"))
			{
				writer.WriteLine("#include \"p3d.generated.h\"");
				foreach (var inc in cppIncludes)
				{
					writer.WriteLine($"#include <{inc}>");
				}

				writer.WriteLine();

				writer.WriteLine("namespace Donut::P3D");
				writer.Write("{");
				writer.Write(cpprb.ToString());
				writer.WriteLine("}");
			}
		}
	}
}
