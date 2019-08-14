using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DonutCodeGen
{
    class P3DGenerator
    {
        static readonly string classTemplate = @"
    class {0}
    {{
    public:

        {0}(const P3DChunk&);

        static std::unique_ptr<{0}> Load(const P3DChunk& chunk) {{ return std::make_unique<{0}>(chunk); }}

        {1}
    private:

        {2}
    }};";

        static readonly string ctorTemplate = @"
    {0}::{0}(const P3DChunk& chunk)
    {{
        assert(chunk.IsType(ChunkType::{0}));

        MemoryStream stream(chunk.GetData());
        {1}{3}{2}    }}";

        static readonly string switchCaseTemplate = @"
        for (auto const& child : chunk.GetChildren())
        {{
            {2}switch (child->GetType())
            {{
                {1}                default:
                    {3}break;
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

        enum ChunkTypes : uint
        {
            Camera = 0x2200,
            LightGroup = 0x2380,
            Skeleton = 0x4500,
            SkeletonJoint = 0x4501,
            SkeletonJointMirrorMap = 0x4503,
            SkeletonJointBonePreserve = 0x4504,
            CompositeDrawable = 0x4512,
            CompositeDrawableSkinList = 0x4513,
            CompositeDrawablePropList = 0x4514,
            CompositeDrawableSkin = 0x4515,
            CompositeDrawableProp = 0x4516,
            CompositeDrawableEffectList = 0x4517,
            CompositeDrawableEffect = 0x4518,
            CompositeDrawableSortOrder = 0x4519,
            MultiController = 0x48A0,
            MultiControllerTracks = 0x48A1,
            History = 0x7000,
            ExportInfo = 0x7030,
            ExportInfoNamedString = 0x7031,
            ExportInfoNamedInteger = 0x7032,
            Mesh = 0x10000,
            PolySkin = 0x10001,
            PrimitiveGroup = 0x10002,
            BoundingBox = 0x10003,
            BoundingSphere = 0x10004,
            PositionList = 0x10005,
            NormalList = 0x10006,
            UVList = 0x10007,
            ColorList = 0x10008,
            IndexList = 0x1000A,
            MatrixList = 0x1000B,
            WeightList = 0x1000C,
            MatrixPalette = 0x1000D,
            OffsetList = 0x1000E,
            PackedNormalList = 0x10010,
            VertexShader = 0x10011,
            DrawShadow = 0x10017,
            ExpressionOffsets = 0x10018,
            Shader = 0x11000,
            ShaderTextureParam = 0x11002,
            ShaderIntParam = 0x11003,
            ShaderFloatParam = 0x11004,
            ShaderColorParam = 0x11005,
            GameAttr = 0x12000,
            GameAttrIntParam = 0x12001,
            Light = 0x13000,
            LightDirection = 0x13001,
            LightPosition = 0x13002,
            LightShadow = 0x13004,
            Todo0 = 0x13008,
            Locator = 0x14000,
            ParticleSystemFactory = 0x15800,
            ParticleSystem = 0x15801,
            BaseEmitter = 0x15805,
            SpriteEmitter = 0x15806,
            ParticleAnimation = 0x15808,
            EmitterAnimation = 0x15809,
            GeneratorAnimation = 0x1580A,
            ParticleInstancingInfo = 0x1580B,
            BillboardQuad = 0x17001,
            BillboardQuadGroup = 0x17002,
            BillboardDisplayInfo = 0x17003,
            BillboardPerspectiveInfo = 0x17004,
            FrontendProject = 0x18000,
            FrontendScreen = 0x18001,
            FrontendPage = 0x18002,
            FrontendLayer = 0x18003,
            FrontendGroup = 0x18004,
            FrontendMultiSprite = 0x18006,
            FrontendMultiText = 0x18007,
            FrontendObject = 0x18008,
            FrontendPolygon = 0x18009,
            FrontendStringTextBible = 0x1800B,
            FrontendStringHardCoded = 0x1800C,
            FrontendTextBible = 0x1800D,
            FrontendLanguage = 0x1800E,
            FrontendImageResource = 0x18100,
            FrontendObjectResource = 0x18101,
            FrontendTextStyleResource = 0x18104,
            FrontendTextBibleResource = 0x18105,
            Texture = 0x19000,
            Image = 0x19001,
            ImageData = 0x19002,
            Sprite = 0x19005,
            AnimatedObjectFactory = 0x20000,
            AnimatedObject = 0x20001,
            AnimatedObjectAnimation = 0x20002,
            Expression = 0x21000,
            ExpressionGroup = 0x21001,
            ExpressionMixer = 0x21002,
            TextureFont = 0x22000,
            FontGlyphs = 0x22001,
            SceneGraph = 0x120100,
            SceneGraphRoot = 0x120101,
            SceneGraphBranch = 0x120102,
            SceneGraphTransform = 0x120103,
            SceneGraphVisibility = 0x120104,
            SceneGraphDrawable = 0x120107,
            SceneGraphLightGroup = 0x120109,
            SceneGraphSortOrder = 0x12010A,
            Animation = 0x121000,
            AnimationGroup = 0x121001,
            AnimationGroupList = 0x121002,
            AnimationSize = 0x121004,
            AnimationHeader = 0x121006,
            Float1Channel = 0x121100,
            Float2Channel = 0x121101,
            Vector1Channel = 0x121102,
            Vector2Channel = 0x121103,
            Vector3Channel = 0x121104,
            QuaternionChannel = 0x121105,
            EntityChannel = 0x121107,
            BoolChannel = 0x121108,
            Color = 0x121109,
            IntChannel = 0x12110E,
            ChannelInterpolationMode = 0x121110,
            CompressedQuaternionChannel = 0x121111,
            FrameController = 0x121200,
            FrameController2 = 0x121201,
            MultiController2 = 0x121202,
            Todo1 = 0x121203,
            VectorOffsetList = 0x121301,
            VertexAnimKeyFrame = 0x121304,
            Fence = 0x3000000,
            RoadSegment = 0x3000002,
            Road = 0x3000003,
            Intersection = 0x3000004,
            Locator2 = 0x3000005,
            TriggerVolume = 0x3000006,
            Spline = 0x3000007,
            InstanceList = 0x3000008,
            RoadDataSegment = 0x3000009,
            Todo2 = 0x300000A,
            Path = 0x300000B,
            LocatorMatrix = 0x300000C,
            SurfaceTypeList = 0x300000E,
            FollowCameraData = 0x3000100,
            Set = 0x3000110,
            CollisionEffect = 0x3000600,
            Atc = 0x3000602,
            BreakableObject = 0x3001000,
            InstParticleSystem = 0x3001001,
            StaticEntity = 0x3F00000,
            StaticPhysics = 0x3F00001,
            DynamicPhysics = 0x3F00002,
            Intersect = 0x3F00003,
            Tree = 0x3F00004,
            TreeNode = 0x3F00005,
            TreeNode2 = 0x3F00006,
            FenceWrapper = 0x3F00007,
            AnimCollision = 0x3F00008,
            InstancedStaticEntity = 0x3F00009,
            InstancedStaticPhysics = 0x3F0000A,
            WorldSphere = 0x3F0000B,
            Anim = 0x3F0000C,
            LensFlare = 0x3F0000D,
            AnimDynamicPhysics = 0x3F0000E,
            AnimDynamicPhysicsWrapper = 0x3F0000F,
            AnimObjectWrapper = 0x3F00010,
            CollisionObject = 0x7010000,
            CollisionVolume = 0x7010001,
            CollisionSphere = 0x7010002,
            CollisionCylinder = 0x7010003,
            CollisionOBBoxVolume = 0x7010004,
            CollisionWallVolume = 0x7010005,
            CollisionBBoxVolume = 0x7010006,
            CollisionVector = 0x7010007,
            CollisionVolumeOwner = 0x7010021,
            CollisionVolumeOwnerName = 0x7010022,
            CollisionObjectAttribute = 0x7010023,
            PhysicsObject = 0x7011000,
            PhysicsInertiaMatrix = 0x7011001,
            PhysicsVector = 0x7011002,
            PhysicsJoint = 0x7011020,
            StaticPropData = 0x8020000,
            StaticPropStateData = 0x8020001,
            StaticPropVisibilityData = 0x8020002,
            StaticPropFrameControllerData = 0x8020003,
            StaticPropEventData = 0x8020004,
            StaticPropCallbackData = 0x8020005,
            P3DZRoot = 0x5A443350,
            P3DRoot = 0xFF443350,
        }

        static string GetNativeType(string s) => typeDict.TryGetValue(s, out var v) ? v : s;

        public static int Process(string inputFile, string outputPath, string copyright)
        {
            if (!File.Exists(inputFile) ||
                !Directory.Exists(outputPath))
            {
                return 1;
            }

            int exitCode = 0;

            var defObject = JObject.Parse(File.ReadAllText(inputFile));
            var headersb = new StringBuilder();
            var cppsb = new StringBuilder();
            var docStringBuilders = new Dictionary<ChunkTypes, StringBuilder>();
            var chunkTypes = new List<string>();

            foreach (var classToken in defObject)
            {
                chunkTypes.Add(classToken.Key);

                var readers = new IndentedTextWriter(new StringWriter()) { Indent = 2 };
                var publicBlock = new IndentedTextWriter(new StringWriter()) { Indent = 2 };
                var privateBlock = new IndentedTextWriter(new StringWriter()) { Indent = 2 };
                var caseBlock = new IndentedTextWriter(new StringWriter()) { Indent = 4 };
                bool useDataStream = false;
                bool useLogs = false;

                var mdNameTypes = new StringBuilder();
                var mdNameChunks = new StringBuilder();

                foreach (var classProperty in classToken.Value.Values<JProperty>())
                {
                    if (classProperty.Name == "!log")
                    {
                        useLogs = (bool)classProperty.Value;
                        continue;
                    }

                    if (classProperty.Value.Type != JTokenType.String) continue;

                    var valueString = classProperty.Value.ToString();
                    if (string.IsNullOrWhiteSpace(valueString)) continue;

                    var propertyName = classProperty.Name.ToString();
                    if (string.IsNullOrWhiteSpace(propertyName)) continue;

                    var valueArgs = valueString.Split(new char[] { ' ', '<', '>' }, StringSplitOptions.RemoveEmptyEntries);
                    if (valueArgs.Length == 0) continue;

                    if (valueArgs.Length == 1)
                    {
                        var type = valueArgs[0];
                        var funcName = $"{Char.ToUpperInvariant(propertyName[0])}{propertyName.Substring(1)}";
                        string nativeType = null;

                        if (type.Contains("["))
                        {
                            var split = type.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                            if (split.Length != 2 || !uint.TryParse(split[1], out var n)) continue;

                            nativeType = GetNativeType(split[0]);

                            publicBlock.WriteLine($"const {nativeType}& Get{funcName}() const {{ return _{propertyName}; }}");
                            privateBlock.WriteLine($"{nativeType} _{propertyName};");
                            readers.WriteLine($"_{propertyName} = stream.ReadString({n});");

                            mdNameTypes.AppendLine($"|`{propertyName}`|`{type}`|");

                            continue;
                        }

                        nativeType = GetNativeType(type);
                        var readerName = type == "string" ? "LPString" : $"<{nativeType}>";

                        publicBlock.WriteLine($"const {nativeType}& Get{funcName}() const {{ return _{propertyName}; }}");
                        privateBlock.WriteLine($"{nativeType} _{propertyName};");
                        readers.WriteLine($"_{propertyName} = stream.Read{readerName}();");

                        mdNameTypes.AppendLine($"|`{propertyName}`|`{type}`|");
                    }
                    else if (valueArgs.Length <= 4)
                    {
                        var funcName = $"{Char.ToUpperInvariant(propertyName[0])}{propertyName.Substring(1)}";

                        switch (valueArgs[0])
                        {
                            case "child":
                                {
                                    if (valueArgs.Length == 2)
                                    {
                                        var chunkType = valueArgs[1];

                                        publicBlock.WriteLine($"const std::unique_ptr<{chunkType}>& Get{funcName}() const {{ return _{propertyName}; }}");
                                        privateBlock.WriteLine($"std::unique_ptr<{chunkType}> _{propertyName};");

                                        caseBlock.WriteLine($@"case ChunkType::{chunkType}:
                    {{
                        _{propertyName} = std::make_unique<{chunkType}>(*child);
                        break;
                    }}");

                                        mdNameChunks.AppendLine($"|`{propertyName}`|`{chunkType}`|");
                                    }
                                    else if (valueArgs.Length == 3)
                                    {
                                        var type = valueArgs[1];
                                        var chunkType = valueArgs[2];
                                        var nativeType = GetNativeType(type);
                                        var readerName = type == "string" ? "LPString" : $"<{nativeType}>";

                                        publicBlock.WriteLine($"const {nativeType}& Get{funcName}() const {{ return _{propertyName}; }}");
                                        privateBlock.WriteLine($"{nativeType} _{propertyName};");

                                        caseBlock.WriteLine($@"case ChunkType::{chunkType}:
                    {{
                        _{propertyName} = data.Read{readerName}();
                        break;
                    }}");
                                        useDataStream = true;

                                        mdNameChunks.AppendLine($"|`{propertyName}`|`{chunkType}<{type}>`|");
                                    }
                                    break;
                                }
                            case "children":
                                {
                                    if (valueArgs.Length == 2)
                                    {
                                        var chunkType = valueArgs[1];

                                        publicBlock.WriteLine($"const std::vector<std::unique_ptr<{chunkType}>>& Get{funcName}() const {{ return _{propertyName}; }}");
                                        privateBlock.WriteLine($"std::vector<std::unique_ptr<{chunkType}>> _{propertyName};");

                                        caseBlock.WriteLine($@"case ChunkType::{chunkType}:
                    {{
                        _{propertyName}.push_back(std::make_unique<{chunkType}>(*child));
                        break;
                    }}");

                                        mdNameChunks.AppendLine($"|`{propertyName}`|`{chunkType}[]`|");
                                    }
                                    else if (valueArgs.Length == 3)
                                    {
                                        var type = valueArgs[1];
                                        var chunkType = valueArgs[2];
                                        var nativeType = GetNativeType(type);
                                        var readerName = type == "string" ? "LPString" : $"<{nativeType}>";

                                        publicBlock.WriteLine($"const std::vector<{nativeType}>& Get{funcName}() const {{ return _{propertyName}; }}");
                                        privateBlock.WriteLine($"std::vector<{nativeType}> _{propertyName};");

                                        caseBlock.WriteLine($@"case ChunkType::{chunkType}:
                    {{
                        _{propertyName}.push_back(data.Read{readerName}());
                        break;
                    }}");
                                        useDataStream = true;

                                        mdNameChunks.AppendLine($"|`{propertyName}`|`{chunkType}<{type}>[]`|");
                                    }
                                    break;
                                }
                            case "dictionary":
                                {
                                    if (valueArgs.Length == 4)
                                    {
                                        var keyType = GetNativeType(valueArgs[1]);
                                        var keyName = valueArgs[2];
                                        keyName = $"{Char.ToUpperInvariant(keyName[0])}{keyName.Substring(1)}";
                                        var chunkType = valueArgs[3];

                                        publicBlock.WriteLine($"{chunkType}* Get{funcName}Value(const {keyType}& key) const {{ auto it = _{propertyName}.find(key); return (it != _{propertyName}.end()) ? it->second.get() : nullptr; }}");
                                        privateBlock.WriteLine($"std::map<{keyType}, std::unique_ptr<{chunkType}>> _{propertyName};");

                                        caseBlock.WriteLine($@"case ChunkType::{chunkType}:
                    {{
                        auto value = std::make_unique<{chunkType}>(*child);
                        _{propertyName}.insert({{ value->Get{keyName}(), std::move(value) }});
                        break;
                    }}");

                                        mdNameChunks.AppendLine($"|`{propertyName}`|`{chunkType}[]`|");
                                    }
                                    break;
                                }
                            case "buffer":
                                {
                                    var type = valueArgs[1];
                                    string nativeType = "";
                                    string resizeString = "";
                                    bool fixedSize = false;
                                    string mdFixedSize = "";
                                    string fixedType = "";

                                    if (type.Contains("["))
                                    {
                                        var split = type.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                                        if (split.Length != 2) continue;

                                        if (uint.TryParse(split[1], out var n))
                                        {
                                            resizeString = $"{n}";
                                            mdFixedSize = resizeString;
                                        }
                                        else
                                        {
                                            resizeString = $"_{split[1]}";
                                            mdFixedSize = $"{split[1]}";
                                        }

                                        nativeType = GetNativeType(split[0]);
                                        fixedType = split[0];
                                        fixedSize = true;
                                    }
                                    else
                                    {
                                        nativeType = GetNativeType(type);
                                    }

                                    var isString = nativeType == GetNativeType("string");

                                    if (valueArgs.Length == 2)
                                    {
                                        if (!fixedSize) resizeString = "stream.Read<uint32_t>()";

                                        publicBlock.WriteLine($"const std::vector<{nativeType}>& Get{funcName}() const {{ return _{propertyName}; }}");
                                        privateBlock.WriteLine($"std::vector<{nativeType}> _{propertyName};");
                                        readers.WriteLine($"_{propertyName}.resize({resizeString});");

                                        if (isString)
                                        {
                                            readers.WriteLine($"for (size_t i = 0; i < _{propertyName}.size(); ++i)");
                                            readers.WriteLine("{");
                                            readers.WriteLine($"    _{propertyName}[i] = stream.ReadLPString();");
                                            readers.WriteLine("}");
                                        }
                                        else
                                        {
                                            readers.WriteLine($"stream.ReadBytes(reinterpret_cast<uint8_t*>(_{propertyName}.data()), _{propertyName}.size() * sizeof({nativeType}));");
                                        }

                                        if (fixedSize)
                                        {
                                            mdNameTypes.AppendLine($"|`{propertyName}`|`{fixedType}[{mdFixedSize}]`|");
                                        }
                                        else
                                        {
                                            mdNameTypes.AppendLine($"|`{propertyName}`|`{type}[u32]`|");
                                        }
                                    }
                                    else if (valueArgs.Length == 3)
                                    {
                                        var chunkType = valueArgs[2];

                                        if (!fixedSize) resizeString = "data.Read<uint32_t>()";

                                        publicBlock.WriteLine($"const std::vector<{nativeType}>& Get{funcName}() const {{ return _{propertyName}; }}");
                                        privateBlock.WriteLine($"std::vector<{nativeType}> _{propertyName};");

                                        caseBlock.WriteLine($@"case ChunkType::{chunkType}:
                    {{
                        _{propertyName}.resize({resizeString});
                        data.ReadBytes(reinterpret_cast<uint8_t*>(_{propertyName}.data()), _{propertyName}.size() * sizeof({nativeType}));
                        break;
                    }}");
                                        useDataStream = true;

                                        if (fixedSize)
                                        {
                                            mdNameChunks.AppendLine($"|`{propertyName}`|`{chunkType}<{type}>[{mdFixedSize}]`|");
                                        }
                                        else
                                        {
                                            mdNameChunks.AppendLine($"|`{propertyName}`|`{chunkType}<{type}>[u32]`|");
                                        }
                                    }
                                    break;
                                }
                            case "buffers":
                                {
                                    if (valueArgs.Length != 3) continue;
                                    var type = GetNativeType(valueArgs[1]);
                                    if (type == "string") break; // don't allow string buffers
                                    var chunkType = valueArgs[2];

                                    publicBlock.WriteLine($"const std::vector<{type}>& Get{funcName}(size_t index) const {{ return _{propertyName}.at(index); }}");
                                    privateBlock.WriteLine($"std::vector<std::vector<{type}>> _{propertyName};");

                                    caseBlock.WriteLine($@"case ChunkType::{chunkType}:
                    {{
                        uint32_t length = data.Read<uint32_t>();
                        uint32_t channel = data.Read<uint32_t>();
                        _{propertyName}.resize(channel + 1);
                        _{propertyName}.at(channel).resize(length);
                        data.ReadBytes(reinterpret_cast<uint8_t*>(_{propertyName}.at(channel).data()), length * sizeof({type}));
                        break;
                    }}");
                                    useDataStream = true;

                                    mdNameChunks.AppendLine($"|`{propertyName}`|`{chunkType}<{valueArgs[1]}>[u32][u32]`|");

                                    break;
                                }
                        }
                    }
                }

                if (Enum.TryParse<ChunkTypes>(classToken.Key, out var chunkEnum))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"## {classToken.Key} `0x{string.Format("{0:x}", chunkEnum).TrimStart('0')}`");
                    sb.AppendLine("|Name|Type|");
                    sb.AppendLine("|--|--|");
                    sb.AppendLine(mdNameTypes.ToString());

                    if (mdNameChunks.Length > 0)
                    {
                        sb.AppendLine($"### Children");
                        sb.AppendLine("|Name|Chunk|");
                        sb.AppendLine("|--|--|");
                        sb.AppendLine(mdNameChunks.ToString());
                    }

                    docStringBuilders.Add(chunkEnum, sb);
                }

                var switchBlock = new StringWriter();
                if (!string.IsNullOrEmpty(caseBlock.InnerWriter.ToString()))
                {
                    switchBlock.WriteLine(switchCaseTemplate,
                        classToken.Key,
                        caseBlock.InnerWriter,
                        useDataStream ? "MemoryStream data(child->GetData());\n\n            " : null,
                        useLogs ? $"std::cout << \"[{classToken.Key}] Unexpected Chunk: \" << child->GetType() << \"\\n\";\n                    " : "");
                }

                var endOfStream = new StringWriter();
                if (useLogs)
                {
                    endOfStream.WriteLine($@"		
        if (!stream.End())
		{{
            std::cout << fmt::format(""[{classToken.Key}] only read {{0}} out of {{1}} bytes!"", stream.Position(), chunk.GetDataSize()) << std::endl;
        }}");
                }

                headersb.AppendLine(string.Format(classTemplate,
                    classToken.Key,
                    publicBlock.InnerWriter,
                    privateBlock.InnerWriter));

                cppsb.AppendLine(string.Format(ctorTemplate,
                    classToken.Key,
                    readers.InnerWriter,
                    switchBlock,
                    endOfStream));
            }

            var numChunkTypes = Enum.GetValues(typeof(ChunkTypes)).Length;

            var mdsb = new StringBuilder();
            foreach (var sbPair in docStringBuilders.OrderBy(key => key.Key))
            {
                mdsb.Append(sbPair.Value.ToString());
            }

            mdsb.AppendLine($"# TODO Chunks ({numChunkTypes - chunkTypes.Count} / {numChunkTypes})");

            foreach (var chunkType in Enum.GetValues(typeof(ChunkTypes)))
            {
                if (chunkTypes.Any(x => x == chunkType.ToString())) continue;

                mdsb.AppendLine($"#### {chunkType} `0x{string.Format("{0:x}", chunkType).TrimStart('0')}`");
            }

            var headerIncludes = new[]
            {
                "P3D/P3DChunk.h",
                "glm/vec2.hpp",
                "glm/vec3.hpp",
                "glm/vec4.hpp",
                "glm/gtc/quaternion.hpp",
                "glm/mat4x4.hpp",
                "string",
                "memory",
                "vector",
                "map",
            };

            var cppIncludes = new[]
            {
                "Core/MemoryStream.h",
                "iostream",
            };

            var headerPath = Path.Combine(outputPath, "P3D.generated.h");
            var cppPath = Path.Combine(outputPath, "P3D.generated.cpp");
            var mdPath = "Chunks.md";

            using (var writer = File.CreateText(mdPath))
            {
                writer.WriteLine($"# Chunks ({chunkTypes.Count} / {numChunkTypes})");
                writer.WriteLine();
                writer.WriteLine(mdsb);
            }

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine($"// {copyright}\n");
                writer.WriteLine("#pragma once\n");
                writer.WriteLine(Program.GeneratedComment);
                writer.WriteLine();

                foreach (var inc in headerIncludes)
                {
                    writer.WriteLine($"#include <{inc}>");
                }

                writer.WriteLine();

                writer.WriteLine("namespace Donut::P3D");
                writer.WriteLine("{");
                foreach (var decl in chunkTypes)
                {
                    writer.WriteLine($"\tclass {decl};");
                }
                writer.Write(headersb.ToString());
                writer.WriteLine("}");

                writer.Flush();

                var bytes = stream.ToArray();
                bool fileEqual = File.Exists(headerPath) && new FileInfo(headerPath).Length == stream.Length &&
                    File.ReadAllBytes(headerPath).SequenceEqual(bytes);

                if (!fileEqual)
                {
                    File.WriteAllBytes(headerPath, bytes);
                }
            }

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine($"// {copyright}\n");
                writer.WriteLine(Program.GeneratedComment);
                writer.WriteLine();

                writer.WriteLine("#include \"P3D.generated.h\"");
                foreach (var inc in cppIncludes)
                {
                    writer.WriteLine($"#include <{inc}>");
                }

                writer.WriteLine();

                writer.WriteLine("namespace Donut::P3D");
                writer.Write("{");
                writer.Write(cppsb.ToString());
                writer.WriteLine("}");

                writer.Flush();

                var bytes = stream.ToArray();
                bool fileEqual = File.Exists(cppPath) && new FileInfo(cppPath).Length == stream.Length &&
                    File.ReadAllBytes(cppPath).SequenceEqual(bytes);

                if (!fileEqual)
                {
                    File.WriteAllBytes(cppPath, bytes);
                }
            }

            return exitCode;
        }
    }
}
