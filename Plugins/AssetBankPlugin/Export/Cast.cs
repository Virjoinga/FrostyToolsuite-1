using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetBankPlugin.Export
{
    public class Cast
    {
        private static ulong _hashBase = 0x534E495752545250;

        public static ulong NextHash()
        {
            return _hashBase++;
        }

        public static string CastTypeForMaximum(IEnumerable<uint> values)
        {
            if (!values.Any()) return "b";
            uint maximum = values.Max();

            if (maximum <= 0xFF)
                return "b";
            if (maximum <= 0xFFFF)
                return "h";
            return "i";
        }

        public List<CastNode> RootNodes { get; set; } = new List<CastNode>();

        public RootNode CreateRoot()
        {
            var root = new RootNode();
            RootNodes.Add(root);
            return root;
        }

        public static Cast Load(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return Load(stream);
            }
        }

        public static Cast Load(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                uint magic = reader.ReadUInt32();
                if (magic != 0x74736163)
                    throw new InvalidDataException("Invalid cast file magic");

                uint version = reader.ReadUInt32();
                uint rootCount = reader.ReadUInt32();
                uint reserved = reader.ReadUInt32();

                var cast = new Cast();
                for (int i = 0; i < rootCount; i++)
                {
                    cast.RootNodes.Add(CastNode.LoadNode(reader));
                }

                return cast;
            }
        }

        public void Save(string path)
        {
            using (var stream = File.Create(path))
            {
                Save(stream);
            }
        }

        public void Save(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write((uint)0x74736163); // Magic
                writer.Write((uint)1);          // Version
                writer.Write((uint)RootNodes.Count);
                writer.Write((uint)0);          // Reserved

                foreach (var rootNode in RootNodes)
                {
                    rootNode.Save(writer);
                }
            }
        }
    }

    public class CastProperty_t
    {
        public int Size { get; set; }
        public string Format { get; set; }
        public string Identifier { get; set; }
        public int ArrayCount { get; set; }

        public CastProperty_t(string identifier)
        {
            Identifier = identifier;
            switch (identifier)
            {
                case "b": Size = 1; Format = "B"; ArrayCount = 1; break;
                case "h": Size = 2; Format = "H"; ArrayCount = 1; break;
                case "i": Size = 4; Format = "I"; ArrayCount = 1; break;
                case "l": Size = 8; Format = "Q"; ArrayCount = 1; break;
                case "f": Size = 4; Format = "f"; ArrayCount = 1; break;
                case "d": Size = 8; Format = "d"; ArrayCount = 1; break;
                case "s": Size = 0; Format = "s"; ArrayCount = 1; break;
                case "2v": Size = 8; Format = "2f"; ArrayCount = 2; break;
                case "3v": Size = 12; Format = "3f"; ArrayCount = 3; break;
                case "4v": Size = 16; Format = "4f"; ArrayCount = 4; break;
                default:
                    Size = 0;
                    Format = "";
                    ArrayCount = 1;
                    break;
            }
        }
    }

    public class CastProperty
    {
        public string Name { get; set; }
        public CastProperty_t PropertyType { get; set; }
        public List<object> Values { get; set; } = new List<object>();

        public CastProperty(string name, string typeIdentifier)
        {
            Name = name;
            PropertyType = new CastProperty_t(typeIdentifier);
        }

        public CastProperty(BinaryReader reader)
        {
            Load(reader);
        }

        public void Load(BinaryReader reader)
        {
            byte[] typeBytes = reader.ReadBytes(2);
            ushort nameLength = reader.ReadUInt16();
            uint rawCount = reader.ReadUInt32();

            byte[] nameBytes = reader.ReadBytes(nameLength);
            Name = Encoding.UTF8.GetString(nameBytes);

            string typeId = Encoding.UTF8.GetString(typeBytes).TrimEnd('\0');
            PropertyType = new CastProperty_t(typeId);

            if (PropertyType.Size == 0 && PropertyType.Format == "s")
            {
                List<byte> strBytes = new List<byte>();
                byte b;
                while ((b = reader.ReadByte()) != 0)
                {
                    strBytes.Add(b);
                }
                Values = new List<object> { Encoding.UTF8.GetString(strBytes.ToArray()) };
            }
            else
            {
                int totalElements = (int)rawCount * PropertyType.ArrayCount;
                Values = new List<object>(totalElements);

                for (int i = 0; i < totalElements; i++)
                {
                    switch (PropertyType.Identifier)
                    {
                        case "b": Values.Add(reader.ReadByte()); break;
                        case "h": Values.Add(reader.ReadUInt16()); break;
                        case "i": Values.Add(reader.ReadUInt32()); break;
                        case "l": Values.Add(reader.ReadUInt64()); break;
                        case "f":
                        case "2v":
                        case "3v":
                        case "4v":
                            Values.Add(reader.ReadSingle());
                            break;
                        case "d": Values.Add(reader.ReadDouble()); break;
                    }
                }
            }
        }

        public void Save(BinaryWriter writer)
        {
            byte[] typeBytes = new byte[2];
            byte[] rawTypeBytes = Encoding.UTF8.GetBytes(PropertyType.Identifier);
            Array.Copy(rawTypeBytes, typeBytes, Math.Min(rawTypeBytes.Length, 2));

            byte[] nameBytes = Encoding.UTF8.GetBytes(Name);

            writer.Write(typeBytes);
            writer.Write((ushort)nameBytes.Length);
            writer.Write((uint)(Values.Count / PropertyType.ArrayCount));
            writer.Write(nameBytes);

            if (PropertyType.Size == 0 && PropertyType.Format == "s")
            {
                string str = (Values.Count > 0) ? (Values[0]?.ToString() ?? "") : "";
                byte[] strBytes = Encoding.UTF8.GetBytes(str);
                writer.Write(strBytes);
                writer.Write((byte)0);
            }
            else
            {
                foreach (var val in Values)
                {
                    switch (PropertyType.Identifier)
                    {
                        case "b": writer.Write(Convert.ToByte(val)); break;
                        case "h": writer.Write(Convert.ToUInt16(val)); break;
                        case "i": writer.Write(Convert.ToUInt32(val)); break;
                        case "l": writer.Write(Convert.ToUInt64(val)); break;
                        case "f":
                        case "2v":
                        case "3v":
                        case "4v":
                            writer.Write(Convert.ToSingle(val));
                            break;
                        case "d": writer.Write(Convert.ToDouble(val)); break;
                    }
                }
            }
        }

        public uint Length()
        {
            uint result = 8; // Identifier(2) + NameLength(2) + Count(4)
            result += (uint)Encoding.UTF8.GetByteCount(Name);

            if (PropertyType.Size == 0 && PropertyType.Format == "s")
            {
                string str = (Values.Count > 0) ? (Values[0]?.ToString() ?? "") : "";
                result += (uint)Encoding.UTF8.GetByteCount(str) + 1;
            }
            else
            {
                result += (uint)(PropertyType.Size * (Values.Count / PropertyType.ArrayCount));
            }

            return result;
        }
    }

    public class CastNode
    {
        public uint Identifier { get; set; }
        public ulong Hash { get; set; }
        public CastNode ParentNode { get; set; }
        public List<CastNode> ChildNodes { get; set; } = new List<CastNode>();
        public Dictionary<string, CastProperty> Properties { get; set; } = new Dictionary<string, CastProperty>();

        public CastNode(uint identifier)
        {
            Identifier = identifier;
            Hash = Cast.NextHash();
        }

        public T ChildOfType<T>() where T : CastNode
        {
            return ChildNodes.OfType<T>().FirstOrDefault();
        }

        public List<T> ChildrenOfType<T>() where T : CastNode
        {
            return ChildNodes.OfType<T>().ToList();
        }

        public CastNode ChildByHash(ulong hash)
        {
            return ChildNodes.FirstOrDefault(x => x.Hash == hash);
        }

        public CastProperty CreateProperty(string name, string type)
        {
            var property = new CastProperty(name, type);
            Properties[name] = property;
            return property;
        }

        public void SetPropertyVal<T>(string name, string type, T val)
        {
            var prop = CreateProperty(name, type);
            prop.Values.Clear();
            prop.Values.Add(val);
        }

        public void SetPropertyArray<T>(string name, string type, IEnumerable<T> values)
        {
            var prop = CreateProperty(name, type);
            prop.Values.Clear();
            foreach (var v in values)
            {
                prop.Values.Add(v);
            }
        }

        public T GetPropertyVal<T>(string name, T defaultVal = default)
        {
            if (Properties.TryGetValue(name, out var prop) && prop.Values.Count > 0)
            {
                try
                {
                    return (T)Convert.ChangeType(prop.Values[0], typeof(T));
                }
                catch
                {
                    if (prop.Values[0] is T val) return val;
                }
            }
            return defaultVal;
        }

        public List<T> GetPropertyArray<T>(string name)
        {
            if (Properties.TryGetValue(name, out var prop))
            {
                return prop.Values.Select(v => (T)Convert.ChangeType(v, typeof(T))).ToList();
            }
            return new List<T>();
        }

        public CastNode CreateChild(CastNode child)
        {
            child.ParentNode = this;
            ChildNodes.Add(child);
            return child;
        }

        public static CastNode LoadNode(BinaryReader reader)
        {
            uint identifier = reader.ReadUInt32();
            uint size = reader.ReadUInt32();
            ulong hash = reader.ReadUInt64();
            uint propertyCount = reader.ReadUInt32();
            uint childCount = reader.ReadUInt32();

            CastNode node = CreateNode(identifier);
            node.Hash = hash;

            for (uint i = 0; i < propertyCount; i++)
            {
                var prop = new CastProperty(reader);
                node.Properties[prop.Name] = prop;
            }

            for (uint i = 0; i < childCount; i++)
            {
                var child = LoadNode(reader);
                child.ParentNode = node;
                node.ChildNodes.Add(child);
            }

            return node;
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Identifier);
            writer.Write(Length());
            writer.Write(Hash);
            writer.Write((uint)Properties.Count);
            writer.Write((uint)ChildNodes.Count);

            foreach (var property in Properties.Values)
            {
                property.Save(writer);
            }

            foreach (var childNode in ChildNodes)
            {
                childNode.Save(writer);
            }
        }

        public uint Length()
        {
            uint result = 24; // Header size (Identifier(4) + Size(4) + Hash(8) + PropCount(4) + ChildCount(4))

            foreach (var property in Properties.Values)
            {
                result += property.Length();
            }

            foreach (var childNode in ChildNodes)
            {
                result += childNode.Length();
            }

            return result;
        }

        private static CastNode CreateNode(uint identifier)
        {
            switch (identifier)
            {
                case 0x746F6F72: return new RootNode();
                case 0x6C646F6D: return new ModelNode();
                case 0x6873656D: return new MeshNode();
                case 0x72696168: return new HairNode();
                case 0x68736C62: return new BlendShapeNode();
                case 0x6C656B73: return new SkeletonNode();
                case 0x6D696E61: return new AnimationNode();
                case 0x76727563: return new CurveNode();
                case 0x564F4D43: return new CurveModeOverrideNode();
                case 0x6669746E: return new NotificationTrackNode();
                case 0x656E6F62: return new BoneNode();
                case 0x64686B69: return new IKHandleNode();
                case 0x74736E63: return new ConstraintNode();
                case 0x6C74616D: return new MaterialNode();
                case 0x656C6966: return new FileNode();
                case 0x726C6F63: return new ColorNode();
                case 0x74736E69: return new InstanceNode();
                case 0x6174656D: return new MetadataNode();
                default: return new CastNode(identifier);
            }
        }
    }

    public class RootNode : CastNode
    {
        public RootNode() : base(0x746F6F72) { }

        public ModelNode CreateModel() => (ModelNode)CreateChild(new ModelNode());
        public AnimationNode CreateAnimation() => (AnimationNode)CreateChild(new AnimationNode());
        public InstanceNode CreateInstance() => (InstanceNode)CreateChild(new InstanceNode());
        public MetadataNode CreateMetadata() => (MetadataNode)CreateChild(new MetadataNode());
    }

    public class ModelNode : CastNode
    {
        public ModelNode() : base(0x6C646F6D) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public List<float> Position => GetPropertyArray<float>("p");
        public void SetPosition(float[] position) => SetPropertyArray("p", "3v", position);

        public List<float> Rotation => GetPropertyArray<float>("r");
        public void SetRotation(float[] rotation) => SetPropertyArray("r", "4v", rotation);

        public List<float> Scale => GetPropertyArray<float>("s");
        public void SetScale(float[] scale) => SetPropertyArray("s", "3v", scale);

        public SkeletonNode Skeleton => ChildOfType<SkeletonNode>();
        public SkeletonNode CreateSkeleton() => (SkeletonNode)CreateChild(new SkeletonNode());

        public List<MeshNode> Meshes => ChildrenOfType<MeshNode>();
        public MeshNode CreateMesh() => (MeshNode)CreateChild(new MeshNode());

        public List<HairNode> Hairs => ChildrenOfType<HairNode>();
        public HairNode CreateHair() => (HairNode)CreateChild(new HairNode());

        public List<MaterialNode> Materials => ChildrenOfType<MaterialNode>();
        public MaterialNode CreateMaterial() => (MaterialNode)CreateChild(new MaterialNode());

        public List<BlendShapeNode> BlendShapes => ChildrenOfType<BlendShapeNode>();
        public BlendShapeNode CreateBlendShape() => (BlendShapeNode)CreateChild(new BlendShapeNode());
    }

    public class AnimationNode : CastNode
    {
        public AnimationNode() : base(0x6D696E61) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public SkeletonNode Skeleton => ChildOfType<SkeletonNode>();
        public SkeletonNode CreateSkeleton() => (SkeletonNode)CreateChild(new SkeletonNode());

        public List<CurveNode> Curves => ChildrenOfType<CurveNode>();
        public CurveNode CreateCurve() => (CurveNode)CreateChild(new CurveNode());

        public List<CurveModeOverrideNode> CurveModeOverrides => ChildrenOfType<CurveModeOverrideNode>();
        public CurveModeOverrideNode CreateCurveModeOverride() => (CurveModeOverrideNode)CreateChild(new CurveModeOverrideNode());

        public List<NotificationTrackNode> Notifications => ChildrenOfType<NotificationTrackNode>();
        public NotificationTrackNode CreateNotification() => (NotificationTrackNode)CreateChild(new NotificationTrackNode());

        public float Framerate => GetPropertyVal<float>("fr");
        public void SetFramerate(float framerate) => SetPropertyVal("fr", "f", framerate);

        public bool Looping => GetPropertyVal<byte>("lo") >= 1;
        public void SetLooping(bool enabled) => SetPropertyVal("lo", "b", (byte)(enabled ? 1 : 0));
    }

    public class CurveNode : CastNode
    {
        public CurveNode() : base(0x76727563) { }

        public string NodeName => GetPropertyVal<string>("nn");
        public void SetNodeName(string name) => SetPropertyVal("nn", "s", name);

        public string KeyPropertyName => GetPropertyVal<string>("kp");
        public void SetKeyPropertyName(string name) => SetPropertyVal("kp", "s", name);

        public List<uint> KeyFrameBuffer => GetPropertyArray<uint>("kb");
        public void SetKeyFrameBuffer(IEnumerable<uint> values) => SetPropertyArray("kb", Cast.CastTypeForMaximum(values), values);

        public List<float> KeyValueBuffer => GetPropertyArray<float>("kv");
        public void SetFloatKeyValueBuffer(IEnumerable<float> values) => SetPropertyArray("kv", "f", values);
        public void SetVec4KeyValueBuffer(IEnumerable<float[]> values)
        {
            var flattened = values.SelectMany(v => v);
            SetPropertyArray("kv", "4v", flattened);
        }
        public void SetVec4KeyValueBuffer(IEnumerable<float> values) { SetPropertyArray("kv", "4v", values); }
        public void SetByteKeyValueBuffer(IEnumerable<byte> values) => SetPropertyArray("kv", "b", values);

        public string Mode => GetPropertyVal<string>("m");
        public void SetMode(string mode) => SetPropertyVal("m", "s", mode);

        public float AdditiveBlendWeight => GetPropertyVal<float>("ab", 1.0f);
        public void SetAdditiveBlendWeight(float value) => SetPropertyVal("ab", "f", value);
    }

    public class CurveModeOverrideNode : CastNode
    {
        public CurveModeOverrideNode() : base(0x564F4D43) { }

        public string NodeName => GetPropertyVal<string>("nn");
        public void SetNodeName(string name) => SetPropertyVal("nn", "s", name);

        public string Mode => GetPropertyVal<string>("m");
        public void SetMode(string mode) => SetPropertyVal("m", "s", mode);

        public bool OverrideTranslationCurves => GetPropertyVal<byte>("ot") >= 1;
        public void SetOverrideTranslationCurves(bool enabled) => SetPropertyVal("ot", "b", (byte)(enabled ? 1 : 0));

        public bool OverrideRotationCurves => GetPropertyVal<byte>("or") >= 1;
        public void SetOverrideRotationCurves(bool enabled) => SetPropertyVal("or", "b", (byte)(enabled ? 1 : 0));

        public bool OverrideScaleCurves => GetPropertyVal<byte>("os") >= 1;
        public void SetOverrideScaleCurves(bool enabled) => SetPropertyVal("os", "b", (byte)(enabled ? 1 : 0));
    }

    public class NotificationTrackNode : CastNode
    {
        public NotificationTrackNode() : base(0x6669746E) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public List<uint> KeyFrameBuffer => GetPropertyArray<uint>("kb");
        public void SetKeyFrameBuffer(IEnumerable<uint> values) => SetPropertyArray("kb", Cast.CastTypeForMaximum(values), values);
    }

    public class MeshNode : CastNode
    {
        public MeshNode() : base(0x6873656D) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public int UVLayerCount => GetPropertyVal<byte>("ul");
        public void SetUVLayerCount(byte count) => SetPropertyVal("ul", "b", count);

        public int ColorLayerCount()
        {
            if (Properties.TryGetValue("cl", out var cl) && cl.Values.Count > 0)
                return Convert.ToInt32(cl.Values[0]);
            if (Properties.ContainsKey("vc"))
                return 1;
            return 0;
        }

        public void SetColorLayerCount(byte count) => SetPropertyVal("cl", "b", count);

        public List<object> VertexColorLayerBuffer(int index)
        {
            if (Properties.TryGetValue($"c{index}", out var cl))
                return cl.Values;
            if (index == 0 && Properties.TryGetValue("vc", out var vc))
                return vc.Values;
            return null;
        }

        public void SetVertexColorBuffer(int index, IEnumerable<uint> values)
        {
            SetPropertyArray($"c{index}", "i", values);
        }

        public void SetVertexColorBuffer(int index, IEnumerable<float[]> values)
        {
            var flattened = values.SelectMany(v => v);
            SetPropertyArray($"c{index}", "4v", flattened);
        }

        public bool VertexColorLayerBufferPacked(int index)
        {
            if (Properties.TryGetValue($"c{index}", out var cl))
                return cl.PropertyType.Identifier == "i";
            return true;
        }

        public int MaximumWeightInfluence => GetPropertyVal<byte>("mi");
        public void SetMaximumWeightInfluence(byte maximum) => SetPropertyVal("mi", "b", maximum);

        public string SkinningMethod => GetPropertyVal<string>("sm", "linear");
        public void SetSkinningMethod(string method) => SetPropertyVal("sm", "s", method);

        public List<uint> FaceBuffer => GetPropertyArray<uint>("f");
        public void SetFaceBuffer(IEnumerable<uint> values) => SetPropertyArray("f", Cast.CastTypeForMaximum(values), values);

        public List<float> VertexPositionBuffer => GetPropertyArray<float>("vp");
        public void SetVertexPositionBuffer(IEnumerable<float> values) => SetPropertyArray("vp", "3v", values);

        public List<float> VertexNormalBuffer => GetPropertyArray<float>("vn");
        public void SetVertexNormalBuffer(IEnumerable<float> values) => SetPropertyArray("vn", "3v", values);

        public List<float> VertexTangentBuffer => GetPropertyArray<float>("vt");
        public void SetVertexTangentBuffer(IEnumerable<float> values) => SetPropertyArray("vt", "3v", values);

        public List<float> VertexUVLayerBuffer(int index) => GetPropertyArray<float>($"u{index}");
        public void SetVertexUVLayerBuffer(int index, IEnumerable<float> values) => SetPropertyArray($"u{index}", "2v", values);

        public List<uint> VertexWeightBoneBuffer => GetPropertyArray<uint>("wb");
        public void SetVertexWeightBoneBuffer(IEnumerable<uint> values) => SetPropertyArray("wb", Cast.CastTypeForMaximum(values), values);

        public List<float> VertexWeightValueBuffer => GetPropertyArray<float>("wv");
        public void SetVertexWeightValueBuffer(IEnumerable<float> values) => SetPropertyArray("wv", "f", values);

        public void SetMaterial(ulong hash) => SetPropertyVal("m", "l", hash);
    }

    public class HairNode : CastNode
    {
        public HairNode() : base(0x72696168) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public List<uint> SegmentsBuffer => GetPropertyArray<uint>("se");
        public void SetSegmentBuffer(IEnumerable<uint> values) => SetPropertyArray("se", Cast.CastTypeForMaximum(values), values);

        public List<float> ParticleBuffer => GetPropertyArray<float>("pt");
        public void SetParticleBuffer(IEnumerable<float> values) => SetPropertyArray("pt", "3v", values);

        public void SetMaterial(ulong hash) => SetPropertyVal("m", "l", hash);
    }

    public class BlendShapeNode : CastNode
    {
        public BlendShapeNode() : base(0x68736C62) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public void SetBaseShape(ulong hash) => SetPropertyVal("b", "l", hash);

        public List<uint> TargetShapeVertexIndices => GetPropertyArray<uint>("vi");
        public void SetTargetShapeVertexIndices(IEnumerable<uint> indices) => SetPropertyArray("vi", Cast.CastTypeForMaximum(indices), indices);

        public List<float> TargetShapeVertexPositions => GetPropertyArray<float>("vp");
        public void SetTargetShapeVertexPositions(IEnumerable<float> positions) => SetPropertyArray("vp", "3v", positions);

        public float TargetWeightScale => GetPropertyVal<float>("ts");
        public void SetTargetWeightScale(float scale) => SetPropertyVal("ts", "f", scale);
    }

    public class SkeletonNode : CastNode
    {
        public SkeletonNode() : base(0x6C656B73) { }

        public List<BoneNode> Bones => ChildrenOfType<BoneNode>();
        public BoneNode CreateBone() => (BoneNode)CreateChild(new BoneNode());

        public List<IKHandleNode> IKHandles => ChildrenOfType<IKHandleNode>();
        public IKHandleNode CreateIKHandle() => (IKHandleNode)CreateChild(new IKHandleNode());

        public List<ConstraintNode> Constraints => ChildrenOfType<ConstraintNode>();
        public ConstraintNode CreateConstraint() => (ConstraintNode)CreateChild(new ConstraintNode());
    }

    public class BoneNode : CastNode
    {
        public BoneNode() : base(0x656E6F62) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public int ParentIndex()
        {
            uint parentUnsigned = GetPropertyVal<uint>("p");
            return (int)((parentUnsigned ^ 0x80000000) - 0x80000000);
        }

        public void SetParentIndex(int index)
        {
            if (index < 0)
            {
                SetPropertyVal("p", "i", (uint)(index + 4294967296L));
            }
            else
            {
                SetPropertyVal("p", "i", (uint)index);
            }
        }

        public bool SegmentScaleCompensate => GetPropertyVal<byte>("ssc", 1) >= 1;
        public void SetSegmentScaleCompensate(bool enabled) => SetPropertyVal("ssc", "b", (byte)(enabled ? 1 : 0));

        public List<float> LocalPosition => GetPropertyArray<float>("lp");
        public void SetLocalPosition(float[] position) => SetPropertyArray("lp", "3v", position);

        public List<float> LocalRotation => GetPropertyArray<float>("lr");
        public void SetLocalRotation(float[] rotation) => SetPropertyArray("lr", "4v", rotation);

        public List<float> WorldPosition => GetPropertyArray<float>("wp");
        public void SetWorldPosition(float[] position) => SetPropertyArray("wp", "3v", position);

        public List<float> WorldRotation => GetPropertyArray<float>("wr");
        public void SetWorldRotation(float[] rotation) => SetPropertyArray("wr", "4v", rotation);

        public List<float> Scale => GetPropertyArray<float>("s");
        public void SetScale(float[] scale) => SetPropertyArray("s", "3v", scale);
    }

    public class IKHandleNode : CastNode
    {
        public IKHandleNode() : base(0x64686B69) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public void SetStartBone(ulong hash) => SetPropertyVal("sb", "l", hash);
        public void SetEndBone(ulong hash) => SetPropertyVal("eb", "l", hash);
        public void SetTargetBone(ulong hash) => SetPropertyVal("tb", "l", hash);

        public List<float> TargetOffset => GetPropertyArray<float>("to");
        public void SetTargetOffset(float[] offset) => SetPropertyArray("to", "3v", offset);

        public void SetPoleVectorBone(ulong hash) => SetPropertyVal("pv", "l", hash);
        public void SetPoleBone(ulong hash) => SetPropertyVal("pb", "l", hash);

        public bool UseTargetRotation => GetPropertyVal<byte>("tr") >= 1;
        public void SetUseTargetRotation(bool enabled) => SetPropertyVal("tr", "b", (byte)(enabled ? 1 : 0));
    }

    public class ConstraintNode : CastNode
    {
        public ConstraintNode() : base(0x74736E63) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public string ConstraintType => GetPropertyVal<string>("ct");
        public void SetConstraintType(string type) => SetPropertyVal("ct", "s", type);

        public void SetConstraintBone(ulong hash) => SetPropertyVal("cb", "l", hash);
        public void SetTargetBone(ulong hash) => SetPropertyVal("tb", "l", hash);

        public bool MaintainOffset => GetPropertyVal<byte>("mo") >= 1;
        public void SetMaintainOffset(bool enabled) => SetPropertyVal("mo", "b", (byte)(enabled ? 1 : 0));

        public List<float> CustomOffset => GetPropertyArray<float>("co");
        public void SetCustomOffset(float[] offset)
        {
            if (offset.Length == 3)
                SetPropertyArray("co", "3v", offset);
            else if (offset.Length == 4)
                SetPropertyArray("co", "4v", offset);
        }

        public float Weight => GetPropertyVal<float>("wt", 1.0f);
        public void SetWeight(float weight) => SetPropertyVal("wt", "f", weight);

        public bool SkipX => GetPropertyVal<byte>("sx") >= 1;
        public void SetSkipX(bool enabled) => SetPropertyVal("sx", "b", (byte)(enabled ? 1 : 0));

        public bool SkipY => GetPropertyVal<byte>("sy") >= 1;
        public void SetSkipY(bool enabled) => SetPropertyVal("sy", "b", (byte)(enabled ? 1 : 0));

        public bool SkipZ => GetPropertyVal<byte>("sz") >= 1;
        public void SetSkipZ(bool enabled) => SetPropertyVal("sz", "b", (byte)(enabled ? 1 : 0));
    }

    public class MaterialNode : CastNode
    {
        public MaterialNode() : base(0x6C74616D) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public string MaterialType => GetPropertyVal<string>("t");
        public void SetMaterialType(string type) => SetPropertyVal("t", "s", type);

        public void SetSlot(string slot, ulong hash) => SetPropertyVal(slot, "l", hash);

        public FileNode CreateFile() => (FileNode)CreateChild(new FileNode());
    }

    public class FileNode : CastNode
    {
        public FileNode() : base(0x656C6966) { }

        public string Path => GetPropertyVal<string>("p");
        public void SetPath(string path) => SetPropertyVal("p", "s", path);
    }

    public class ColorNode : CastNode
    {
        public ColorNode() : base(0x726C6F63) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public string ColorSpace => GetPropertyVal<string>("cs", "srgb");
        public void SetColorSpace(string value) => SetPropertyVal("cs", "s", value);

        public List<float> Rgba => GetPropertyArray<float>("rgba");
        public void SetRgba(float[] rgba) => SetPropertyArray("rgba", "4v", rgba);
    }

    public class InstanceNode : CastNode
    {
        public InstanceNode() : base(0x74736E69) { }

        public string Name => GetPropertyVal<string>("n");
        public void SetName(string name) => SetPropertyVal("n", "s", name);

        public void SetReferenceFile(ulong hash) => SetPropertyVal("rf", "l", hash);

        public List<float> Position => GetPropertyArray<float>("p");
        public void SetPosition(float[] position) => SetPropertyArray("p", "3v", position);

        public List<float> Rotation => GetPropertyArray<float>("r");
        public void SetRotation(float[] rotation) => SetPropertyArray("r", "4v", rotation);

        public List<float> Scale => GetPropertyArray<float>("s");
        public void SetScale(float[] scale) => SetPropertyArray("s", "3v", scale);
    }

    public class MetadataNode : CastNode
    {
        public MetadataNode() : base(0x6174656D) { }

        public string Author => GetPropertyVal<string>("a");
        public void SetAuthor(string author) => SetPropertyVal("a", "s", author);

        public string Software => GetPropertyVal<string>("s");
        public void SetSoftware(string software) => SetPropertyVal("s", "s", software);

        public string UpAxis => GetPropertyVal<string>("up");
        public void SetUpAxis(string up) => SetPropertyVal("up", "s", up);

        public string SceneRoot => GetPropertyVal<string>("sr");
        public void SetSceneRoot(string root) => SetPropertyVal("sr", "s", root);
    }
}