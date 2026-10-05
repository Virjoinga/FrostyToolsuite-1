using Frosty.Core.IO;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Attributes;
using FrostySdk.BaseProfile;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Frosty.Core.Sdk
{
    #region -- EXE Header --
    class IMAGE_DOS_HEADER
    {                                              // DOS .EXE header
        public ushort e_magic;                     // Magic number
        public ushort e_cblp;                      // Bytes on last page of file
        public ushort e_cp;                        // Pages in file
        public ushort e_crlc;                      // Relocations
        public ushort e_cparhdr;                   // Size of header in paragraphs
        public ushort e_minalloc;                  // Minimum extra paragraphs needed
        public ushort e_maxalloc;                  // Maximum extra paragraphs needed
        public ushort e_ss;                        // Initial (relative) SS value
        public ushort e_sp;                        // Initial SP value
        public ushort e_csum;                      // Checksum
        public ushort e_ip;                        // Initial IP value
        public ushort e_cs;                        // Initial (relative) CS value
        public ushort e_lfarlc;                    // File address of relocation table
        public ushort e_ovno;                      // Overlay number
        public ushort[] e_res = new ushort[4];     // Reserved words
        public ushort e_oemid;                     // OEM identifier (for e_oeminfo)
        public ushort e_oeminfo;                   // OEM information; e_oemid specific
        public ushort[] e_res2 = new ushort[10];   // Reserved words
        public int e_lfanew;                       // File address of new exe header

        public void Read(NativeReader reader)
        {
            e_magic = reader.ReadUShort();
            e_cblp = reader.ReadUShort();
            e_cp = reader.ReadUShort();
            e_crlc = reader.ReadUShort();
            e_cparhdr = reader.ReadUShort();
            e_minalloc = reader.ReadUShort();
            e_maxalloc = reader.ReadUShort();
            e_ss = reader.ReadUShort();
            e_sp = reader.ReadUShort();
            e_csum = reader.ReadUShort();
            e_ip = reader.ReadUShort();
            e_cs = reader.ReadUShort();
            e_lfarlc = reader.ReadUShort();
            e_ovno = reader.ReadUShort();
            for (int i = 0; i < 4; i++) { e_res[i] = reader.ReadUShort(); }
            e_oemid = reader.ReadUShort();
            e_oeminfo = reader.ReadUShort();
            for (int i = 0; i < 10; i++) { e_res2[i] = reader.ReadUShort(); }
            e_lfanew = reader.ReadInt();
        }
    }

    class IMAGE_FILE_HEADER
    {
        public ushort Machine;
        public ushort NumberOfSections;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public ushort Characteristics;

        public void Read(NativeReader reader)
        {
            Machine = reader.ReadUShort();
            NumberOfSections = reader.ReadUShort();
            TimeDateStamp = reader.ReadUInt();
            PointerToSymbolTable = reader.ReadUInt();
            NumberOfSymbols = reader.ReadUInt();
            SizeOfOptionalHeader = reader.ReadUShort();
            Characteristics = reader.ReadUShort();
        }
    }

    struct IMAGE_SECTION_HEADER
    {
        public uint PhysicalAddress => Address;
        public uint VirtualSize => Address;

        public string Name;
        public uint Address;
        public uint VirtualAddress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLinenumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public uint Characteristics;

        public void Read(NativeReader reader)
        {
            Name = reader.ReadSizedString(8);
            Address = reader.ReadUInt();
            VirtualAddress = reader.ReadUInt();
            SizeOfRawData = reader.ReadUInt();
            PointerToRawData = reader.ReadUInt();
            PointerToRelocations = reader.ReadUInt();
            PointerToLinenumbers = reader.ReadUInt();
            NumberOfRelocations = reader.ReadUShort();
            NumberOfLinenumbers = reader.ReadUShort();
            Characteristics = reader.ReadUInt();
        }
    }

    struct IMAGE_DATA_DIRECTORY
    {
        public uint VirtualAddress;
        public uint Size;
    }

    class IMAGE_OPTIONAL_HEADER
    {
        public ushort Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
        public ulong ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public ulong SizeOfStackReserve;
        public ulong SizeOfStackCommit;
        public ulong SizeOfHeapReserve;
        public ulong SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
        IMAGE_DATA_DIRECTORY[] DataDirectory = new IMAGE_DATA_DIRECTORY[16];

        public void Read(NativeReader reader)
        {
            Magic = reader.ReadUShort();
            MajorLinkerVersion = reader.ReadByte();
            MinorLinkerVersion = reader.ReadByte();
            SizeOfCode = reader.ReadUInt();
            SizeOfInitializedData = reader.ReadUInt();
            SizeOfUninitializedData = reader.ReadUInt();
            AddressOfEntryPoint = reader.ReadUInt();
            BaseOfCode = reader.ReadUInt();
            ImageBase = reader.ReadULong();
            SectionAlignment = reader.ReadUInt();
            FileAlignment = reader.ReadUInt();
            MajorOperatingSystemVersion = reader.ReadUShort();
            MinorOperatingSystemVersion = reader.ReadUShort();
            MajorImageVersion = reader.ReadUShort();
            MinorImageVersion = reader.ReadUShort();
            MajorSubsystemVersion = reader.ReadUShort();
            MinorSubsystemVersion = reader.ReadUShort();
            Win32VersionValue = reader.ReadUInt();
            SizeOfImage = reader.ReadUInt();
            SizeOfHeaders = reader.ReadUInt();
            CheckSum = reader.ReadUInt();
            Subsystem = reader.ReadUShort();
            DllCharacteristics = reader.ReadUShort();
            SizeOfStackReserve = reader.ReadULong();
            SizeOfStackCommit = reader.ReadULong();
            SizeOfHeapReserve = reader.ReadULong();
            SizeOfHeapCommit = reader.ReadULong();
            LoaderFlags = reader.ReadUInt();
            NumberOfRvaAndSizes = reader.ReadUInt();

            for (int i = 0; i < 16; i++)
            {
                DataDirectory[i].VirtualAddress = reader.ReadUInt();
                DataDirectory[i].Size = reader.ReadUInt();
            }
        }
    }
    #endregion

    public class ModuleWriter : IDisposable
    {
        private static List<string> CreateOldFb3PFs() => new List<string>
        {
            "RenderFormat_BC1_UNORM",
            "RenderFormat_BC1A_UNORM",
            "RenderFormat_BC2_UNORM",
            "RenderFormat_BC3_UNORM",
            "RenderFormat_BC3A_UNORM",
            "RenderFormat_DXN",
            "RenderFormat_BC7_UNORM",
            "RenderFormat_RGB565",
            "RenderFormat_RGB888",
            "RenderFormat_ARGB1555",
            "RenderFormat_ARGB4444",
            "RenderFormat_ARGB8888",
            "RenderFormat_L8",
            "RenderFormat_L16",
            "RenderFormat_ABGR16",
            "RenderFormat_ABGR16F",
            "RenderFormat_ABGR32F",
            "RenderFormat_R16F",
            "RenderFormat_R32F",
            "RenderFormat_NormalDXN",
            "RenderFormat_NormalDXT1",
            "RenderFormat_NormalDXT5",
            "RenderFormat_NormalDXT5RGA",
            "RenderFormat_RG8",
            "RenderFormat_GR16",
            "RenderFormat_GR16F",
            "RenderFormat_D16",
            "RenderFormat_D24",
            "RenderFormat_D24S8",
            "RenderFormat_D24FS8",
            "RenderFormat_D32F",
            "RenderFormat_D32FS8",
            "RenderFormat_S8",
            "RenderFormat_ABGR32",
            "RenderFormat_GR32F",
            "RenderFormat_A2R10G10B10",
            "RenderFormat_R11G11B10F",
            "RenderFormat_ABGR16_SNORM",
            "RenderFormat_ABGR16_UINT",
            "RenderFormat_L16_UINT",
            "RenderFormat_L32",
            "RenderFormat_GR16_UINT",
            "RenderFormat_GR32_UINT",
            "RenderFormat_ETC1",
            "RenderFormat_ETC2_RGB",
            "RenderFormat_ETC2_RGBA",
            "RenderFormat_ETC2_RGB_A1",
            "RenderFormat_PVRTC1_4BPP_RGBA",
            "RenderFormat_PVRTC1_4BPP_RGB",
            "RenderFormat_PVRTC1_2BPP_RGBA",
            "RenderFormat_PVRTC1_2BPP_RGB",
            "RenderFormat_PVRTC2_4BPP",
            "RenderFormat_PVRTC2_2BPP",
            "RenderFormat_R8",
            "RenderFormat_R9G9B9E5F",
            "RenderFormat_Unknown"
        };

        private readonly DbObject m_classList;
        private readonly string m_filename;
        private Dictionary<string, DbObject> m_classByNameMap;

        public ModuleWriter(string inFilename, DbObject inList)
        {
            m_filename = inFilename;
            m_classList = inList;
        }

        private void BuildClassMap()
        {
            m_classByNameMap = new Dictionary<string, DbObject>(StringComparer.Ordinal);
            foreach (DbObject obj in m_classList)
            {
                string name = obj.GetValue<string>("name");
                if (!string.IsNullOrEmpty(name))
                {
                    m_classByNameMap[name] = obj;
                }
            }
        }

        public void Write(uint version)
        {
            BuildClassMap();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using FrostySdk.Attributes;");
            sb.AppendLine("using FrostySdk.Managers;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using FrostySdk;");
            sb.AppendLine();
            sb.AppendLine($"[assembly: SdkVersion({(int)version})]");
            sb.AppendLine();
            sb.AppendLine("namespace FrostySdk.Ebx");
            sb.AppendLine("{");
            {
                foreach (DbObject classObj in m_classList)
                {
                    EbxFieldType type = (EbxFieldType)classObj.GetValue<int>("type");
                    if (type == EbxFieldType.Enum)
                    {
                        sb.Append(WriteEnum(classObj));
                    }
                    else if (type == EbxFieldType.Struct || type == EbxFieldType.Pointer)
                    {
                        sb.Append(WriteClass(classObj));
                    }
                    else if (type != EbxFieldType.Array && type != EbxFieldType.Delegate && type != EbxFieldType.Function && classObj.HasValue("basic"))
                    {
                        sb.AppendLine("namespace Reflection\r\n{");
                        sb.Append(WriteClass(classObj));
                        sb.AppendLine("}");
                    }
                    else if (type == EbxFieldType.Delegate || type == EbxFieldType.Function)
                    {
                        sb.AppendLine("namespace Reflection\r\n{");
                        sb.Append(WriteDelegate(classObj));
                        sb.AppendLine("}");
                    }
                }

                if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                        ProfileVersion.Battlefield4,
                        ProfileVersion.NeedForSpeedRivals,
                        ProfileVersion.PlantsVsZombiesGardenWarfare))
                {
                    DbObject newEnum = DbObject.CreateObject();
                    newEnum.SetValue("name", "RenderFormat");

                    List<string> formats = CreateOldFb3PFs();
                    DbObject fields = DbObject.CreateList();

                    int value = 0;
                    foreach (string format in formats)
                    {
                        DbObject field = DbObject.CreateObject();
                        field.SetValue("name", format);
                        field.SetValue("value", value++);
                        fields.Add(field);
                    }

                    newEnum.SetValue("fields", fields);
                    sb.Append(WriteEnum(newEnum));
                }
            }
            sb.AppendLine("}");

            /* We'll need to gather the required assemblies first: FrostySdk, mscorlib, System, System.Core, System.Runtime. */
            const int ASSEMBLIES_COUNT = 5;
            List<MetadataReference> references = new(ASSEMBLIES_COUNT);
            references.Add(MetadataReference.CreateFromFile(typeof(BaseBinarySbReader).Assembly.Location));

            /* For the System assemblies, they have no publicly accessible types, so we'll have to fetch them in a different
             * manner. 
             */
            string systemAssembliesPath = Path.GetDirectoryName(typeof(AccessViolationException).Assembly.Location);

            references.Add(MetadataReference.CreateFromFile(Path.Combine(systemAssembliesPath, "mscorlib.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(systemAssembliesPath, "System.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(systemAssembliesPath, "System.Core.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(systemAssembliesPath, "System.Private.CoreLib.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(systemAssembliesPath, "System.Runtime.dll")));

            SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(sb.ToString(), new CSharpParseOptions().WithPreprocessorSymbols($"DV_{ProfilesLibrary.DataVersion}"));

            CSharpCompilation compilation = CSharpCompilation.Create(Path.GetFileNameWithoutExtension(m_filename))
                .AddSyntaxTrees(syntaxTree)
                .AddReferences(references)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            /* Emit the compiled assembly to a stream first, since Roslyn writes out a file regardless of compilation success. */
            using MemoryStream memoryStream = new();

            EmitResult results = compilation.Emit(memoryStream);
            File.Delete("temp.cs");

            if (results.Success)
            {
                using FileStream fileStream = new(m_filename, FileMode.Create, FileAccess.Write, FileShare.None);
                memoryStream.Position = 0;
                memoryStream.CopyTo(fileStream);
            }
#if FROSTY_ALPHA || FROSTY_DEVELOPER
            else
            {
                using (NativeWriter writer = new NativeWriter(new FileStream("Errors.txt", FileMode.Create)))
                {
                    foreach (Diagnostic error in results.Diagnostics)
                    {
                        writer.WriteLine($"[Line: {error.Location.GetLineSpan().StartLinePosition}]: {error.GetMessage()}");
                    }
                }
            }
#endif
        }

        private string WriteDelegate(DbObject delegateObj)
        {
            StringBuilder sb = new StringBuilder();
            {
                sb.Append(WriteClassAttributes(delegateObj));
                string returnType = "void";
                string delegateName = delegateObj.GetValue<string>("name");

                List<string> parameters = new List<string>();
                {
                    foreach (DbObject parameterObj in delegateObj.GetValue<DbObject>("parameters"))
                    {
                        switch (parameterObj.GetValue<byte>("parameterType"))
                        {
                            case 0:
                            case 2: // in ptr
                                parameters.Add($"{parameterObj.GetValue<string>("baseType")} {parameterObj.GetValue<string>("name")}");
                                break;
                            case 1:
                            case 3: // out ptr
                                returnType = parameterObj.GetValue<string>("baseType");
                                break;
                        }
                    }
                    int parenIndex = delegateName.IndexOf('(');
                    if (parenIndex >= 0 && delegateName.EndsWith(")"))
                    {
                        string fnName = delegateName.Substring(0, parenIndex);
                        if (fnName.StartsWith("delegate "))
                            fnName = fnName.Substring("delegate ".Length);

                        string args = delegateName.Substring(parenIndex + 1, delegateName.Length - parenIndex - 2);
                        string[] argTypes = args.Split(',');
                        for (int i = 0; i < argTypes.Length; i++)
                        {
                            string typeName = argTypes[i].Trim();
                            if (typeName.Length == 0)
                                continue;
                            parameters.Add(MapBfnTypeName(typeName) + " arg" + parameters.Count);
                        }

                        delegateName = "Delegate_" + HashDelegateName(delegateName).ToString("x8");
                    }
                }

                string inputParams = string.Join(", ", parameters);
                sb.AppendLine($"public delegate {returnType} {delegateName} ({inputParams});");
            }
            return sb.ToString();
        }

        private static string MapBfnTypeName(string name)
        {
            switch (name)
            {
                case "Boolean": return "bool";
                case "Int32": return "int";
                case "Uint32": return "uint";
                case "Float32": return "float";
                case "Float64": return "double";
                case "EntryInputAction_t": return "int";
                default: return name;
            }
        }

        /// <summary>
        /// Generates a unique, valid C# identifier suffix for delegate names.
        /// </summary>
        private static uint HashDelegateName(string name)
        {
            uint hash = 2166136261;
            foreach (char c in name)
            {
                hash ^= (byte)c;
                hash *= 16777619;
            }
            return hash;
        }

        private string WriteEnum(DbObject enumObj)
        {
            StringBuilder sb = new StringBuilder();
            {
                sb.Append(WriteClassAttributes(enumObj));
                sb.AppendLine("public enum " + enumObj.GetValue<string>("name"));
                sb.AppendLine("{");
                foreach (DbObject fieldObj in enumObj.GetValue<DbObject>("fields"))
                {
                    sb.AppendLine(fieldObj.GetValue<string>("name") + " = " + fieldObj.GetValue<int>("value") + ",");
                }
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        private string WriteClass(DbObject classObj)
        {
            if (classObj.GetValue<string>("name") == "char")
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            {
                string parent = classObj.GetValue<string>("parent", "").Replace(':', '_').Replace('<', '_').Replace('>', '_');
                EbxFieldType type = (EbxFieldType)classObj.GetValue<int>("type");
                DbObject meta = classObj.GetValue<DbObject>("meta");

                string className = classObj.GetValue<string>("name").Replace(':', '_').Replace('<', '_').Replace('>', '_');

                sb.Append(WriteClassAttributes(classObj));
                sb.AppendLine("public class " + className + (parent != "" ? " : " + parent : ""));
                sb.AppendLine("{");

                if (type == EbxFieldType.Pointer)
                {
                    if (parent == "DataContainer")
                    {
                        // add Id field to non asset types
                        sb.AppendLine("[" + typeof(IsTransientAttribute).Name + "]");
                        if (!classObj.HasValue("isData"))
                        {
                            sb.AppendLine("[" + typeof(IsHiddenAttribute).Name + "]");
                        }
                        sb.AppendLine("[" + typeof(DisplayNameAttribute).Name + "(\"Id\")]");
                        sb.AppendLine("[" + typeof(CategoryAttribute).Name + "(\"Annotations\")]");
                        sb.AppendLine("[" + typeof(EbxFieldMetaAttribute).Name + "(8310, 8u, null, false, 0)]");
                        sb.AppendLine("[" + typeof(FieldIndexAttribute).Name + "(-2)]");
                        sb.AppendLine("public CString __Id\r\n{\r\nget\r\n{\r\nreturn GetId();\r\n}\r\nset { __id = value; }\r\n}\r\n");
                        sb.AppendLine("protected CString __id = new CString();");
                    }

                    if (parent == "")
                    {
                        // add guid field
                        sb.AppendLine("[" + typeof(IsTransientAttribute).Name + "]");
                        sb.AppendLine("[" + typeof(IsReadOnlyAttribute).Name + "]");
                        sb.AppendLine("[" + typeof(DisplayNameAttribute).Name + "(\"Guid\")]");
                        sb.AppendLine("[" + typeof(CategoryAttribute).Name + "(\"Annotations\")]");
                        //sb.AppendLine("[" + typeof(EditorAttribute).Name + "(\"Struct\")]");
                        sb.AppendLine("[" + typeof(EbxFieldMetaAttribute).Name + "(24918, 1, null, false, 0)]");
                        sb.AppendLine("[" + typeof(FieldIndexAttribute).Name + "(-1)]");
                        sb.AppendLine("public AssetClassGuid __InstanceGuid { get { return __Guid; } }");

                        sb.AppendLine("protected AssetClassGuid __Guid;");
                        sb.AppendLine("public AssetClassGuid GetInstanceGuid() { return __Guid; }");
                        sb.AppendLine("public void SetInstanceGuid(AssetClassGuid newGuid) { __Guid = newGuid; }");
                    }
                }

                // write out fields/properties
                bool isAssetClass = classObj.GetValue<string>("name").Equals("Asset");
                bool addedGetId = false;

                foreach (DbObject fieldObj in classObj.GetValue<DbObject>("fields"))
                {
                    sb.Append(WriteField(fieldObj, classObj));
                    if (!isAssetClass && fieldObj.GetValue<string>("name").Equals("Name", StringComparison.OrdinalIgnoreCase) && type == EbxFieldType.Pointer)
                    {
                        EbxFieldType fieldType = (EbxFieldType)fieldObj.GetValue<int>("type");
                        if (fieldType == EbxFieldType.CString)
                        {
                            Type tmpType = typeof(EbxClassMetaAttribute);
                            string namespaceName = tmpType.GetProperties()[4].Name;
                            tmpType = typeof(GlobalAttributes);
                            string displayModuleName = tmpType.GetFields()[0].Name;
                            tmpType = typeof(CString);
                            string funcName1 = tmpType.GetMethods()[0].Name;

                            if (classObj.HasValue("isData"))
                            {
                                string fieldName = fieldObj.GetValue<string>("name");
                                sb.AppendLine("protected virtual CString GetId()\r\n{");
                                sb.AppendLine("if (__id != \"\") return __id;");
                                sb.AppendLine("if (_" + fieldName + " != \"\") return _" + fieldName + "." + funcName1 + "();");
                                sb.AppendLine("if (" + typeof(GlobalAttributes).Name + "." + displayModuleName + ")\r\n{\r\n" + typeof(EbxClassMetaAttribute).Name + " attr = GetType().GetCustomAttribute<" + typeof(EbxClassMetaAttribute).Name + ">();\r\nif (attr != null && attr." + namespaceName + " != \"\")\r\nreturn attr." + namespaceName + " + \".\" + GetType().Name;\r\n}\r\nreturn GetType().Name;");
                                sb.AppendLine("}");
                                addedGetId = true;
                            }
                            else
                            {
                                string fieldName = fieldObj.GetValue<string>("name");
                                sb.AppendLine("protected override CString GetId()\r\n{");
                                sb.AppendLine("if (__id != \"\") return __id;");
                                sb.AppendLine("if (_" + fieldName + " != \"\") return _" + fieldName + "." + funcName1 + "();\r\nreturn base.GetId();");
                                sb.AppendLine("}");
                            }
                        }
                    }
                }

                if (parent == "DataContainer" && !addedGetId)
                {
                    Type tmpType = typeof(EbxClassMetaAttribute);
                    string namespaceName = tmpType.GetProperties()[4].Name;
                    tmpType = typeof(GlobalAttributes);
                    string displayModuleName = tmpType.GetFields()[0].Name;

                    sb.AppendLine("protected virtual CString GetId()\r\n{");
                    sb.AppendLine("if (__id == \"\")\r\n{\r\nif (" + typeof(GlobalAttributes).Name + "." + displayModuleName + ")\r\n{\r\n" + typeof(EbxClassMetaAttribute).Name + " attr = GetType().GetCustomAttribute<" + typeof(EbxClassMetaAttribute).Name + ">();\r\nif (attr != null && attr." + namespaceName + " != \"\")\r\nreturn attr." + namespaceName + " + \".\" + GetType().Name;\r\n}\r\nreturn GetType().Name;\r\n}\r\nreturn __id;");
                    sb.AppendLine("}");
                }

                // write out custom functions
                if (meta != null)
                {
                    // constructor
                    if (meta.HasValue("constructor"))
                    {
                        sb.AppendLine("public " + classObj.GetValue<string>("name").Replace(':', '_') + "()");
                        sb.AppendLine("{");
                        sb.AppendLine(meta.GetValue<string>("constructor"));
                        sb.AppendLine("}");
                    }

                    sb.AppendLine(meta.GetValue<string>("functions", ""));
                }

                if (type == EbxFieldType.Struct && classObj.GetValue<DbObject>("fields").Count > 0)
                {
                    // Equals override
                    sb.AppendLine("public override bool Equals(object obj)\r\n{");
                    sb.AppendLine("if (obj == null || !(obj is " + className + "))\r\nreturn false;");
                    sb.AppendLine(className + " b = (" + className + ")obj;");
                    sb.Append("return ");

                    int z = 0;
                    foreach (DbObject fieldObj in classObj.GetValue<DbObject>("fields"))
                    {
                        DbObject fieldMeta = fieldObj.GetValue<DbObject>("meta");
                        if (fieldMeta != null && fieldMeta.HasValue("hidden"))
                        {
                            continue;
                        }
                        if (!CanEmitField(fieldObj))
                        {
                            continue;
                        }

                        string fieldName = fieldObj.GetValue<string>("name");
                        sb.AppendLine(((z++ != 0) ? "&& " : "") + fieldName + " == b." + fieldName);
                    }
                    sb.AppendLine(";\r\n}");

                    // GetHashCode override
                    sb.AppendLine("public override int GetHashCode()\r\n{\r\nunchecked {\r\nint hash = (int)2166136261;");
                    foreach (DbObject fieldObj in classObj.GetValue<DbObject>("fields"))
                    {
                        DbObject fieldMeta = fieldObj.GetValue<DbObject>("meta");
                        if (fieldMeta != null && fieldMeta.HasValue("hidden"))
                        {
                            continue;
                        }
                        if (!CanEmitField(fieldObj))
                        {
                            continue;
                        }

                        string fieldName = fieldObj.GetValue<string>("name");
                        sb.AppendLine("hash = (hash * 16777619) ^ " + fieldName + ".GetHashCode();");
                    }
                    sb.AppendLine("return hash;\r\n}\r\n}");
                }

                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        private static bool CanEmitField(DbObject fieldObj)
        {
            DbObject meta = fieldObj.GetValue<DbObject>("meta");
            if (meta != null && meta.HasValue("version"))
            {
                bool bFound = false;
                foreach (int ver in meta.GetValue<DbObject>("version"))
                {
                    if (ver == (int)ProfilesLibrary.DataVersion)
                    {
                        bFound = true;
                        break;
                    }
                }

                if (!bFound)
                {
                    return false;
                }
            }

            EbxFieldType type = (EbxFieldType)fieldObj.GetValue<int>("type");
            string baseType = fieldObj.GetValue<string>("baseType", "");
            DbObject typeObj = meta?.GetValue<DbObject>("type");
            if (typeObj != null)
            {
                type = (EbxFieldType)typeObj.GetValue<int>("flags");
                if (typeObj.HasValue("baseType"))
                {
                    baseType = typeObj.GetValue<string>("baseType");
                }
            }

            if (type == EbxFieldType.Array)
            {
                EbxFieldType arrayType = (EbxFieldType)((fieldObj.GetValue<int>("arrayFlags") >> 4) & 0x1F);
                if (typeObj != null)
                {
                    arrayType = (EbxFieldType)typeObj.GetValue<int>("arrayType");
                }
                return GetFieldType(arrayType, baseType) != "";
            }

            return GetFieldType(type, baseType) != "";
        }

        private string WriteField(DbObject fieldObj, DbObject classObj)
        {
            StringBuilder sb = new StringBuilder();
            {
                string fieldName = fieldObj.GetValue<string>("name");
                EbxFieldType type = (EbxFieldType)fieldObj.GetValue<int>("type");
                string baseType = fieldObj.GetValue<string>("baseType", "");

                DbObject meta = fieldObj.GetValue<DbObject>("meta");
                DbObject typeObj = meta?.GetValue<DbObject>("type");

                if (meta != null && meta.HasValue("version"))
                {
                    bool bFound = false;
                    foreach (int ver in meta.GetValue<DbObject>("version"))
                    {
                        if (ver == (int)ProfilesLibrary.DataVersion)
                        {
                            bFound = true;
                            break;
                        }
                    }

                    if (!bFound)
                    {
                        return "";
                    }
                }

                if (typeObj != null)
                {
                    type = (EbxFieldType)typeObj.GetValue<int>("flags");
                    if (typeObj.HasValue("baseType"))
                    {
                        baseType = typeObj.GetValue<string>("baseType");
                    }
                }

                string fieldType;
                bool requiresDeclaration;

                sb.Append(WriteFieldAttributes(fieldObj));
                if (type == EbxFieldType.Array)
                {
                    EbxFieldType arrayType = (EbxFieldType)((fieldObj.GetValue<int>("arrayFlags") >> 4) & 0x1F);
                    if (typeObj != null)
                    {
                        arrayType = (EbxFieldType)typeObj.GetValue<int>("arrayType");
                    }

                    fieldType = "List<" + GetFieldType(arrayType, baseType) + ">";
                    requiresDeclaration = true;
                }
                else
                {
                    fieldType = GetFieldType(type, baseType);
                    requiresDeclaration = (type == EbxFieldType.ResourceRef
                        || type == EbxFieldType.BoxedValueRef
                        || type == EbxFieldType.CString
                        || type == EbxFieldType.FileRef
                        || type == EbxFieldType.TypeRef
                        || type == EbxFieldType.Delegate
                        || type == EbxFieldType.Struct);
                }

                if (fieldType == "")
                {
                    return "";
                }

                // rename fields that have the same name as inherited ones
                // Replaced linear search via DbObject.Find with O(1) dictionary lookups

                string parentName = classObj.GetValue<string>("parent");
                while (!string.IsNullOrEmpty(parentName))
                {
                    if (!m_classByNameMap.TryGetValue(parentName, out DbObject parentObj))
                    {
                        break;
                    }

                    parentName = parentObj.GetValue<string>("parent");

                    DbObject parentFields = parentObj.GetValue<DbObject>("fields");
                    if (parentFields != null)
                    {
                        bool matchFound = false;
                        foreach (DbObject pField in parentFields)
                        {
                            if (pField.GetValue<string>("name") == fieldName)
                            {
                                fieldName += $"_{classObj.GetValue<string>("name")}";
                                matchFound = true;
                                break;
                            }
                        }

                        if (matchFound)
                        {
                            break;
                        }
                    }
                }

                if (meta != null && meta.HasValue("accessor"))
                {
                    // custom getter/setter
                    sb.AppendLine("public " + fieldType + " " + fieldName + " { " + meta.GetValue<string>("accessor") + " }");
                }
                else
                {
                    // default getter/setter
                    sb.AppendLine("public " + fieldType + " " + fieldName + " { get { return _" + fieldName + "; } set { _" + fieldName + " = value; } }");
                }
                sb.AppendLine("protected " + fieldType + " _" + fieldName + ((requiresDeclaration) ? " = new " + fieldType + "()" : "") + ";");
            }
            return sb.ToString();
        }

        private string WriteClassAttributes(DbObject classObj)
        {
            StringBuilder sb = new StringBuilder();
            {
                DbObject meta = classObj.GetValue<DbObject>("meta");
                int alignment = classObj.GetValue<int>("alignment");

                if (meta != null && meta.HasValue("alignment"))
                {
                    alignment = meta.GetValue<int>("alignment");
                }

                // mandatory ebx meta
                sb.AppendLine("[" + typeof(EbxClassMetaAttribute).Name + "(" + classObj.GetValue<int>("flags") + ", " + alignment + ", " + classObj.GetValue<int>("size") + ", \"" + classObj.GetValue<string>("namespace") + "\")]");

                if (classObj.HasValue("guid"))
                {
                    sb.AppendLine("[" + typeof(GuidAttribute).Name + "(\"" + classObj.GetValue<Guid>("guid") + "\")]");
                }
                if (classObj.HasValue("typeInfoGuid"))
                {
                    foreach (Guid typeInfoGuid in classObj.GetValue<DbObject>("typeInfoGuid"))
                        sb.AppendLine("[" + typeof(TypeInfoGuidAttribute).Name + "(\"" + typeInfoGuid + "\")]");
                }
                if (classObj.HasValue("arrayNameHash"))
                {
                    sb.AppendLine($"[{typeof(ArrayHashAttribute).Name}({classObj.GetValue<int>("arrayNameHash")})]");
                }
                if (classObj.HasValue("signature"))
                {
                    sb.AppendLine($"[{typeof(TypeInfoSignatureAttribute).Name}({classObj.GetValue<int>("signature")})]");
                }

                //if (ProfilesLibrary.DataVersion == (int)ProfileVersion.StarWarsBattlefrontII)
                //{
                //    sb.AppendLine("[" + typeof(RuntimeSizeAttribute).Name + "(" + classObj.GetValue<int>("runtimeSize") + ")]");
                //}

                if (classObj.HasValue("forceAlign"))
                {
                    sb.AppendLine("[" + typeof(ForceAlignAttribute).Name + "]");
                }

                if (classObj.HasValue("arrayGuid"))
                {
                    sb.AppendLine($"[{typeof(ArrayGuidAttribute).Name}(\"{classObj.GetValue<Guid>("arrayGuid")}\")]");
                }

                if (meta != null)
                {
                    // all other attributes
                    if (meta.HasValue("valueConverter"))
                    {
                        sb.AppendLine("[" + typeof(ClassConverterAttribute).Name + "(\"" + meta.GetValue<string>("valueConverter") + "\")]");
                    }

                    if (meta.HasValue("description"))
                    {
                        sb.AppendLine("[" + typeof(DescriptionAttribute).Name + "(\"" + meta.GetValue<string>("description") + "\")]");
                    }

                    if (meta.HasValue("inline"))
                    {
                        sb.AppendLine("[" + typeof(IsInlineAttribute).Name + "]");
                    }

                    if (meta.HasValue("abstract"))
                    {
                        sb.AppendLine("[" + typeof(IsAbstractAttribute).Name + "]");
                    }

                    if (meta.HasValue("icon"))
                    {
                        sb.AppendLine("[" + typeof(IconAttribute).Name + "(\"" + meta.GetValue<string>("icon") + "\")]");
                    }
                    //if (meta.HasValue("realm")) sb.AppendLine("[DefaultRealm(\"" + meta.GetValue<string>("realm") + "\")]");
                    //if (meta.HasValue("attributes")) sb.Append(meta.GetValue<string>("attributes"));
                }
            }
            return sb.ToString();
        }

        private string WriteFieldAttributes(DbObject fieldObj)
        {
            StringBuilder sb = new StringBuilder();
            {
                DbObject meta = fieldObj.GetValue<DbObject>("meta");
                EbxFieldType type = (EbxFieldType)fieldObj.GetValue<int>("type");
                int arrayFlags = fieldObj.GetValue<int>("arrayFlags", 0);
                string baseType = (type == EbxFieldType.Pointer || type == EbxFieldType.Array) ? fieldObj.GetValue<string>("baseType", "null") : "null";
                int flags = fieldObj.GetValue<int>("flags");

                if (type == EbxFieldType.Array && fieldObj.HasValue("guid"))
                {
                    sb.AppendLine("[" + typeof(GuidAttribute) + "(\"" + fieldObj.GetValue<Guid>("guid") + "\")]");
                }

                if (meta != null)
                {
                    DbObject typeObj = meta.GetValue<DbObject>("type");
                    if (typeObj != null)
                    {
                        flags = (typeObj.GetValue<int>("flags") << 4);
                        if (typeObj.HasValue("baseType"))
                        {
                            baseType = typeObj.GetValue<string>("baseType");
                        }
                        if (typeObj.HasValue("arrayType"))
                        {
                            arrayFlags = typeObj.GetValue<int>("arrayType") << 4;
                        }
                    }
                    else if (meta.HasValue("transient"))
                    {
                        // defaults to int
                        flags = (0x0F << 4);
                    }
                }

                if (baseType != "null")
                {
                    baseType = "typeof(" + baseType + ")";
                }

                // field index
                int index = fieldObj.GetValue<int>("index");

                if (fieldObj.HasValue("nameHash"))
                {
                    sb.AppendLine("[" + typeof(HashAttribute).Name + "(" + fieldObj.GetValue<int>("nameHash") + ")]");
                }

                // mandatory ebx meta
                sb.AppendLine("[" + typeof(EbxFieldMetaAttribute).Name + "(" + flags + ", " + fieldObj.GetValue<int>("offset") + ", " + baseType + ", " + (type == EbxFieldType.Array).ToString().ToLower() + ", " + arrayFlags + ")]");

                if (meta != null)
                {
                    // all other attributes
                    if (meta.HasValue("displayName"))
                    {
                        sb.AppendLine("[" + typeof(DisplayNameAttribute) + "(\"" + meta.GetValue<string>("displayName") + "\")]");
                    }

                    if (meta.HasValue("description"))
                    {
                        sb.AppendLine("[" + typeof(DescriptionAttribute) + "(\"" + meta.GetValue<string>("description") + "\")]");
                    }

                    if (meta.HasValue("editor"))
                    {
                        sb.AppendLine("[" + typeof(EditorAttribute).Name + "(\"" + meta.GetValue<string>("editor") + "\")]");
                    }

                    if (meta.HasValue("readOnly"))
                    {
                        sb.AppendLine("[" + typeof(IsReadOnlyAttribute).Name + "]");
                    }

                    if (meta.HasValue("hidden"))
                    {
                        sb.AppendLine("[" + typeof(IsHiddenAttribute).Name + "]");
                    }

                    if (meta.HasValue("reference"))
                    {
                        sb.AppendLine("[" + typeof(IsReferenceAttribute).Name + "]");
                    }

                    if (meta.HasValue("transient"))
                    {
                        sb.AppendLine("[" + typeof(IsTransientAttribute).Name + "]");
                    }

                    if (meta.HasValue("category"))
                    {
                        sb.AppendLine("[" + typeof(CategoryAttribute).Name + "(\"" + meta.GetValue<string>("category") + "\")]");
                    }

                    if (meta.HasValue("index"))
                    {
                        index = meta.GetValue<int>("index");
                    }

                    if (meta.HasValue("hideChildren"))
                    {
                        sb.AppendLine("[" + typeof(HideChildrentAttribute).Name + "]");
                    }
                    //if (meta.HasValue("property")) sb.AppendLine("[ExposedToSchematics(SchematicsInterfaceType.Property, (SchematicAccessType)" + meta.GetValue<int>("property") + ")]");
                    //if (meta.HasValue("event")) sb.AppendLine("[ExposedToSchematics(SchematicsInterfaceType.Event, (SchematicAccessType)" + meta.GetValue<int>("event") + ")]");
                    //if (meta.HasValue("link")) sb.AppendLine("[ExposedToSchematics(SchematicsInterfaceType.Link, (SchematicAccessType)" + meta.GetValue<int>("link") + ")]");
                }

                sb.AppendLine("[" + typeof(FieldIndexAttribute).Name + "(" + index + ")]");
            }
            return sb.ToString();
        }

        private static string GetFieldType(EbxFieldType type, string baseType)
        {
            switch (type)
            {
                case EbxFieldType.Boolean: return "bool";
                case EbxFieldType.BoxedValueRef: return "BoxedValueRef";
                case EbxFieldType.CString: return "CString";
                case EbxFieldType.FileRef: return "FileRef";
                case EbxFieldType.Float32: return "float";
                case EbxFieldType.Float64: return "double";
                case EbxFieldType.Guid: return "Guid";
                case EbxFieldType.Int16: return "short";
                case EbxFieldType.Int32: return "int";
                case EbxFieldType.Int64: return "long";
                case EbxFieldType.Int8: return "sbyte";
                case EbxFieldType.ResourceRef: return "ResourceRef";
                case EbxFieldType.Sha1: return "Sha1";
                case EbxFieldType.String: return "string";
                case EbxFieldType.TypeRef: return "TypeRef";
                case EbxFieldType.UInt16: return "ushort";
                case EbxFieldType.UInt32: return "uint";
                case EbxFieldType.UInt64: return "ulong";
                case EbxFieldType.UInt8: return "byte";
                case EbxFieldType.Pointer: return "PointerRef";
                case EbxFieldType.Enum: return baseType;
                case EbxFieldType.Struct: return baseType;
                case EbxFieldType.Delegate: return baseType;
                case EbxFieldType.Function: return baseType;
            }

            return "";
        }

        public void Dispose()
        {
        }
    }

    public class ClassesSdkCreator
    {
        #region -- FB Types --
        public class TypeInfo
        {
            public int Type => ((Flags >> 4) & 0x1F);

            public string Name;
            public ushort Flags;
            public uint Size;
            public Guid Guid;
            public ushort Padding1;
            public string NameSpace;
            public ushort Alignment;
            public uint FieldCount;
            public uint Padding3;

            public long ParentClass;
            public long ArrayTypeOffset;
            public List<FieldInfo> Fields = new List<FieldInfo>();
            public List<ParameterInfo> Parameters = new List<ParameterInfo>();

            public virtual void Read(MemoryReader reader)
            {
                bool byteAlignFieldCount = ProfilesLibrary.IsLoaded(ProfileVersion.NeedForSpeedRivals, ProfileVersion.DragonAgeInquisition, ProfileVersion.PlantsVsZombiesGardenWarfare);

                Name = reader.ReadNullTerminatedString();
                Flags = reader.ReadUShort();
                if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa18,
                    ProfileVersion.StarWarsBattlefrontII,
                    ProfileVersion.NeedForSpeedPayback,
                    ProfileVersion.Madden19,
                    ProfileVersion.Fifa19,
                    ProfileVersion.Madden20,
                    ProfileVersion.Battlefield5,
                    ProfileVersion.StarWarsSquadrons))
                {
                    Flags >>= 1;
                }
                Size = reader.ReadUInt();
                if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa19, ProfileVersion.Madden20))
                {
                    reader.Position -= 4;
                    Size = reader.ReadUShort();

                    Guid = reader.ReadGuid();
                    reader.ReadUShort();
                }
                Padding1 = reader.ReadUShort();
                long nameSpaceOffset = reader.ReadLong();

                if (ProfilesLibrary.IsLoaded(ProfileVersion.MassEffectAndromeda,
                    ProfileVersion.Fifa17,
                    ProfileVersion.Fifa18,
                    ProfileVersion.NeedForSpeedPayback,
                    ProfileVersion.Madden19,
                    ProfileVersion.Fifa19,
                    ProfileVersion.StarWarsBattlefrontII,
                    ProfileVersion.Madden20,
                    ProfileVersion.Battlefield5,
                    ProfileVersion.StarWarsSquadrons))
                {
                    ArrayTypeOffset = reader.ReadLong();
                }

                Alignment = (byteAlignFieldCount) ? reader.ReadByte() : reader.ReadUShort();
                FieldCount = (byteAlignFieldCount) ? reader.ReadByte() : reader.ReadUShort();
                if (byteAlignFieldCount)
                {
                    Padding3 = reader.ReadUShort();
                }

                Padding3 = reader.ReadUInt();

                long[] offsets = new long[7];
                for (int i = 0; i < 7; i++)
                    offsets[i] = reader.ReadLong();

                reader.Position = nameSpaceOffset;
                NameSpace = reader.ReadNullTerminatedString();

                bool bReadFields = false;
                if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa19, ProfileVersion.Madden20))
                {
                    // FIFA19
                    ParentClass = offsets[0];
                    if (Type == 2 /* Structure */) { reader.Position = offsets[6]; bReadFields = true; }
                    else if (Type == 3 /* Class */) { reader.Position = offsets[1]; bReadFields = true; }
                    else if (Type == 8 /* Enum */)
                    {
                        ParentClass = 0;
                        reader.Position = offsets[0];
                        if (reader.Position == offsets[0])
                        {
                            reader.Position = offsets[1];
                            long newOffset = reader.ReadLong();
                            reader.Position = newOffset;

                            uint z = FieldCount;
                            while (z > 0)
                            {
                                newOffset = reader.ReadLong();
                                long oldPos = reader.Position;
                                reader.Position = newOffset;

                                while (true)
                                {
                                    if (z == 0)
                                    {
                                        break;
                                    }

                                    long value = reader.ReadLong(); // 0xFFFFFFFF
                                    long nameOffset = reader.ReadLong(); // 0x0
                                    if (value == 0 && nameOffset == 0)
                                    {
                                        break;
                                    }

                                    z--;

                                    FieldInfo f = new FieldInfo { TypeOffset = value };

                                    reader.Position -= 8;
                                    f.Name = reader.ReadNullTerminatedString();

                                    Fields.Add(f);
                                }

                                reader.Position = oldPos;

                                if (z > 0)
                                {
                                    newOffset = reader.ReadLong();
                                    reader.Position = newOffset;
                                }
                            }
                            bReadFields = false;
                        }
                        else
                        {
                            bReadFields = true;
                        }
                    }
                }
                else if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa18,
                    ProfileVersion.StarWarsBattlefrontII,
                    ProfileVersion.NeedForSpeedPayback,
                    ProfileVersion.Madden19,
                    ProfileVersion.Battlefield5,
                    ProfileVersion.StarWarsSquadrons))
                {
                    // FIFA18
                    ParentClass = offsets[0];
                    if (Type == 2 /* Structure */) { reader.Position = offsets[5]; bReadFields = true; }
                    else if (Type == 3 /* Class */) { reader.Position = offsets[1]; bReadFields = true; }
                    else if (Type == 8 /* Enum */) { reader.Position = offsets[0]; bReadFields = true; ParentClass = 0; }
                }
                else if (ProfilesLibrary.IsLoaded(ProfileVersion.MassEffectAndromeda, ProfileVersion.Fifa17))
                {
                    // MEA
                    ParentClass = offsets[0];
                    if (Type == 2 /* Structure */) { reader.Position = offsets[3]; bReadFields = true; }
                    else if (Type == 3 /* Class */) { reader.Position = offsets[1]; bReadFields = true; }
                    else if (Type == 8 /* Enum */) { reader.Position = offsets[0]; bReadFields = true; ParentClass = 0; }
                }
                else if (ProfilesLibrary.IsLoaded(ProfileVersion.NeedForSpeedRivals,
                    ProfileVersion.DragonAgeInquisition,
                    ProfileVersion.NeedForSpeed,
                    ProfileVersion.PlantsVsZombiesGardenWarfare,
                    ProfileVersion.Battlefield4))
                {
                    // DAI
                    ParentClass = offsets[0];
                    if (Type == 2 /* Structure */) { reader.Position = offsets[1]; bReadFields = true; }
                    else if (Type == 3 /* Class */) { reader.Position = offsets[2]; bReadFields = true; }
                    else if (Type == 8 /* Enum */) { reader.Position = offsets[0]; bReadFields = true; ParentClass = 0; }
                }
                else
                {
                    // Everything else
                    if (Type == 2 /* Structure */) { reader.Position = offsets[1]; bReadFields = true; }
                    else if (Type == 3 /* Class */) { reader.Position = offsets[2]; bReadFields = true; }
                    else if (Type == 8 /* Enum */) { reader.Position = offsets[0]; bReadFields = true; ParentClass = 0; }
                    else if (Type == 4 /* Array */) { ParentClass = offsets[0]; }
                }

                if (bReadFields)
                {
                    for (int i = 0; i < FieldCount; i++)
                    {
                        FieldInfo fi = new FieldInfo();
                        fi.Read(reader);
                        fi.Index = i;

                        Fields.Add(fi);
                    }
                }
            }

            public virtual void Modify(DbObject classObj, Dictionary<long, ClassInfo> offsetClassInfoMapping)
            {
            }

            public T As<T>() where T : TypeInfo
            {
                return this as T;
            }
        }

        public class ClassInfo
        {
            public TypeInfo TypeInfo;
            public ushort Id;
            public ushort IsDataContainer;
            public byte[] Padding;
            public long ParentClass;

            public virtual void Read(MemoryReader reader)
            {
                long thisOffset = reader.Position;

                long typeInfoOffset = reader.ReadLong();
                NextOffset = reader.ReadLong();
                Guid guid = Guid.Empty;

                if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII,
                        ProfileVersion.NeedForSpeedPayback,
                        ProfileVersion.Madden19,
                        ProfileVersion.Fifa18,
                        ProfileVersion.Battlefield5,
                        ProfileVersion.StarWarsSquadrons))
                {
                    guid = reader.ReadGuid();
                }
                Id = reader.ReadUShort();
                IsDataContainer = reader.ReadUShort();
                Padding = new byte[] { reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte() };
                ParentClass = reader.ReadLong();

                reader.Position = typeInfoOffset;

                TypeInfo = new TypeInfo();
                TypeInfo.Read(reader);

                if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII,
                        ProfileVersion.NeedForSpeedPayback,
                        ProfileVersion.Madden19,
                        ProfileVersion.Fifa18,
                        ProfileVersion.Battlefield5,
                        ProfileVersion.StarWarsSquadrons))
                {
                    TypeInfo.Guid = guid;
                }

                if (TypeInfo.ParentClass != 0)
                {
                    ParentClass = TypeInfo.ParentClass;
                }

                reader.Position = ParentClass;
                if (reader.Position == thisOffset)
                {
                    ParentClass = 0;
                }
            }
        }

        public class FieldInfo
        {
            public string Name;
            public ushort Flags;
            public uint Offset;
            public ushort Padding1;
            public long TypeOffset;
            public int Index;

            public virtual void Read(MemoryReader reader)
            {
                Name = reader.ReadNullTerminatedString();
                Flags = reader.ReadUShort();
                Offset = reader.ReadUInt();
                Padding1 = reader.ReadUShort();
                TypeOffset = reader.ReadLong();
            }

            public virtual void Modify(DbObject fieldObj)
            {
            }
        }

        public class ParameterInfo
        {
            public string Name;
            public long TypeOffset;
            public long Type;
            public object DefaultValue;

            public virtual void Read(MemoryReader reader)
            {
            }

            public virtual void Modify(DbObject fieldObj)
            {
            }
        }
        #endregion

        public static long NextOffset;

        private readonly List<ClassInfo> m_classInfos = new List<ClassInfo>();
        private readonly HashSet<string> m_alreadyProcessedClasses = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<long, ClassInfo> m_offsetClassInfoMappings = new Dictionary<long, ClassInfo>();

        // Changed List<EbxClass> to HashSet<EbxClass> to remove CPU lookup bottleneck
        private readonly HashSet<EbxClass> m_processedClasses = new HashSet<EbxClass>();
        private Dictionary<string, List<EbxField>> m_fieldMappings;
        private Dictionary<string, Tuple<EbxClass, DbObject>> m_classMappings;

        // Fast lookup maps for CreateSDK loops
        private Dictionary<string, Tuple<EbxClass, DbObject>> m_nonBasicValueMap;
        private Dictionary<string, DbObject> m_classMetaMap;
        private Dictionary<string, Tuple<EbxClass, DbObject>> m_valuesByNameMap;
        private Dictionary<string, DbObject> m_outListByName = new Dictionary<string, DbObject>(StringComparer.Ordinal);

        private List<Tuple<EbxClass, DbObject>> m_values = null;
        private DbObject m_classList = null;
        private DbObject m_classMetaList = null;

        private readonly SdkUpdateState m_state;

        public ClassesSdkCreator(SdkUpdateState inState)
        {
            m_state = inState;
        }

        public bool GatherTypeInfos(SdkUpdateTask task)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Frosty.Core.Sdk.Classes.txt"))
            {
                if (stream != null)
                {
                    m_classMetaList = TypeLibrary.LoadClassesSdk(stream);
                }
            }

            if (ProfilesLibrary.IsLoaded(ProfileVersion.Anthem))
            {
                // read in strings from the AnthemDemo (since Anthem has stripped all strings)
                using (NativeReader reader = new NativeReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Frosty.Core.Sdk.Anthem-Strings.txt")))
                {
                    int count = int.Parse(reader.ReadLine());
                    for (int i = 0; i < count; i++)
                    {
                        string line = reader.ReadLine();
                        string[] arr = line.Split(',');

                        AnthemDemo.Strings.stringHash.Add(uint.Parse(arr[0]), arr[1]);
                    }

                    count = int.Parse(reader.ReadLine());
                    for (int i = 0; i < count; i++)
                    {
                        string line = reader.ReadLine();
                        string[] arr = line.Split(',');

                        AnthemDemo.Strings.classHash.Add(uint.Parse(arr[0]), arr[1]);
                    }

                    count = int.Parse(reader.ReadLine());
                    for (int i = 0; i < count; i++)
                    {
                        int hash = (int)uint.Parse(reader.ReadLine());
                        int fieldCount = int.Parse(reader.ReadLine());

                        AnthemDemo.Strings.fieldHash.Add((uint)hash, new Dictionary<uint, string>());
                        for (int j = 0; j < fieldCount; j++)
                        {
                            string line = reader.ReadLine();
                            string[] arr = line.Split(',');

                            AnthemDemo.Strings.fieldHash[(uint)hash].Add(uint.Parse(arr[0]), arr[1]);
                        }
                    }
                }
            }
            else if (ProfilesLibrary.IsLoaded(ProfileVersion.Battlefield2042))
            {
                // read in strings which were manually created (since Battlefield2042 has stripped all strings)
                using (NativeReader reader = new NativeReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Frosty.Core.Sdk.Bf2042-Strings.txt")))
                {
                    int count = int.Parse(reader.ReadLine());
                    for (int i = 0; i < count; i++)
                    {
                        string line = reader.ReadLine();
                        string[] arr = line.Split(',');

                        Bf2042.Strings.stringHash.Add(uint.Parse(arr[0]), arr[1]);
                    }

                    count = int.Parse(reader.ReadLine());
                    for (int i = 0; i < count; i++)
                    {
                        string line = reader.ReadLine();
                        string[] arr = line.Split(',');

                        Bf2042.Strings.classHash.Add(uint.Parse(arr[0]), arr[1]);
                    }

                    count = int.Parse(reader.ReadLine());
                    for (int i = 0; i < count; i++)
                    {
                        int hash = (int)uint.Parse(reader.ReadLine());
                        int fieldCount = int.Parse(reader.ReadLine());

                        Bf2042.Strings.fieldHash.Add((uint)hash, new Dictionary<uint, string>());
                        for (int j = 0; j < fieldCount; j++)
                        {
                            string line = reader.ReadLine();
                            string[] arr = line.Split(',');

                            Bf2042.Strings.fieldHash[(uint)hash].Add(uint.Parse(arr[0]), arr[1]);
                        }
                    }
                }
            }

            m_classList = DumpClasses(task);

            return m_classList != null && m_classList.Count > 0;
        }

        public bool CrossReferenceAssets(SdkUpdateTask task)
        {
            m_classMappings = new Dictionary<string, Tuple<EbxClass, DbObject>>(StringComparer.Ordinal);
            m_fieldMappings = new Dictionary<string, List<EbxField>>(StringComparer.Ordinal);

            if (App.FileSystemManager.HasFileInMemoryFs("SharedTypeDescriptors.ebx"))
            {
                List<Guid> guids = new List<Guid>();

                LoadSharedTypeDescriptors("SharedTypeDescriptors.ebx", m_classMappings, guids);
                if (App.FileSystemManager.HasFileInMemoryFs("SharedTypeDescriptors_patch.ebx"))
                {
                    LoadSharedTypeDescriptors("SharedTypeDescriptors_patch.ebx", m_classMappings, guids);
                }
            }
            else
            {
                uint count = App.AssetManager.GetEbxCount();
                uint index = 0;

                foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                {
                    Stream ebxStream = App.AssetManager.GetEbxStream(entry);
                    if (ebxStream == null)
                    {
                        continue;
                    }

                    task.StatusMessage = $"{((++index / (float)count) * 100):0}%";
                    using (EbxReader reader = EbxReader.CreateReader(ebxStream))
                    {
                        List<EbxClass> classes = reader.ClassTypes;
                        List<EbxField> fields = reader.FieldTypes;

                        foreach (EbxClass cl in classes)
                        {
                            if (cl.Name != "array")
                            {
                                if (!m_classMappings.ContainsKey(cl.Name))
                                {
                                    DbObject foundObj = null;
                                    int idx = 0;
                                    foreach (DbObject classObj in m_classList)
                                    {
                                        if (classObj.GetValue<string>("name") == cl.Name)
                                        {
                                            foundObj = classObj;
                                            m_classList.RemoveAt(idx);
                                            break;
                                        }
                                        idx++;
                                    }

                                    if (foundObj == null)
                                    {
                                        Console.WriteLine($"SDK: skipping class '{cl.Name}': no metadata in dumped type list");
                                        continue;
                                    }

                                    m_classMappings.Add(cl.Name, new Tuple<EbxClass, DbObject>(cl, foundObj));
                                    m_fieldMappings.Add(cl.Name, new List<EbxField>());

                                    for (int fieldId = 0; fieldId < cl.FieldCount; fieldId++)
                                    {
                                        EbxField field = fields[cl.FieldIndex + fieldId];
                                        m_fieldMappings[cl.Name].Add(field);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        private void RebuildFastLookups()
        {
            m_nonBasicValueMap = new Dictionary<string, Tuple<EbxClass, DbObject>>(StringComparer.Ordinal);
            m_valuesByNameMap = new Dictionary<string, Tuple<EbxClass, DbObject>>(StringComparer.Ordinal);
            foreach (var val in m_values)
            {
                m_valuesByNameMap[val.Item1.Name] = val;
                if (val.Item2 != null && !val.Item2.HasValue("basic") && !m_nonBasicValueMap.ContainsKey(val.Item1.Name))
                {
                    m_nonBasicValueMap[val.Item1.Name] = val;
                }
            }

            m_classMetaMap = new Dictionary<string, DbObject>(StringComparer.Ordinal);
            if (m_classMetaList != null)
            {
                foreach (DbObject obj in m_classMetaList)
                {
                    string name = obj.GetValue<string>("name");
                    if (!string.IsNullOrEmpty(name))
                    {
                        m_classMetaMap[name] = obj;
                    }
                }
            }
        }

        public bool CreateSDK()
        {
            DbObject finalList = new DbObject(false);

            m_values = m_classMappings.Values.ToList();
            m_values.Sort((Tuple<EbxClass, DbObject> a, Tuple<EbxClass, DbObject> b) => a.Item1.Name.CompareTo(b.Item1.Name));

            RebuildFastLookups();

            Console.WriteLine("Creating SDK");
            for (int z = 0; z < m_values.Count; z++)
            {
                Tuple<EbxClass, DbObject> ab = m_values[z];

                EbxClass cl = ab.Item1;
                DbObject obj = ab.Item2;

                if (obj == null)
                {
                    continue;
                }

                int offset = (cl.DebugType == EbxFieldType.Pointer) ? 8 : 0;
                int fieldIndex = 0;

                try
                {
                    ProcessClass(cl, obj, m_fieldMappings[cl.Name], finalList, ref offset, ref fieldIndex);
                }
                catch (Exception e)
                {
                    // Surface the exact class so a generator crash can be diagnosed.
                    Console.WriteLine($"SDK generation failed on class '{cl.Name}': {e}");
                    throw;
                }
            }

            List<DbObject> supportedClasses = new List<DbObject>();
            foreach (DbObject classObj in m_classList)
            {
                if (m_fieldMappings.ContainsKey(classObj.GetValue<string>("name")))
                {
                    continue;
                }

                EbxFieldType type = (EbxFieldType)((classObj.GetValue<int>("flags") >> 4) & 0x1F);
                if (type == EbxFieldType.Pointer)
                {
                    //if(classObj.GetValue<bool>("isRuntimeOnly"))
                    //{
                    //    Console.WriteLine("Class {0} is a runtime class", classObj.GetValue<string>("name"));
                    //    continue;
                    //}

                    if (classObj.GetValue<int>("alignment") == 0)
                    {
                        //Console.WriteLine("Class {0} has 0 byte alignment", classObj.GetValue<string>("name"));
                        classObj.SetValue("alignment", 4);
                    }
                }

                EbxClass tmpClass = new EbxClass()
                {
                    Name = classObj.GetValue<string>("name"),
                    Type = (ushort)classObj.GetValue<int>("flags"),
                    Alignment = (byte)classObj.GetValue<int>("alignment"),
                    FieldCount = (byte)classObj.GetValue<DbObject>("fields").Count
                };

                List<EbxField> tmpFields = new List<EbxField>();
                foreach (DbObject field in classObj.GetValue<DbObject>("fields"))
                {
                    EbxField tmpField = new EbxField()
                    {
                        Name = field.GetValue<string>("name"),
                        Type = (ushort)field.GetValue<int>("flags")
                    };
                    tmpFields.Add(tmpField);
                }

                var tuple = new Tuple<EbxClass, DbObject>(tmpClass, classObj);
                m_values.Add(tuple);
                m_fieldMappings.Add(tmpClass.Name, tmpFields);
                supportedClasses.Add(classObj);
            }

            RebuildFastLookups();

            foreach (DbObject classObj in supportedClasses)
            {
                if (classObj.HasValue("basic"))
                {
                    finalList.Add(classObj);
                    continue;
                }

                if (!m_valuesByNameMap.TryGetValue(classObj.GetValue<string>("name"), out Tuple<EbxClass, DbObject> p))
                {
                    p = m_values.Find(a => a.Item2 == classObj);
                }

                int offset = 0;
                int fieldIndex = 0;

                ProcessClass(p.Item1, p.Item2, m_fieldMappings[p.Item1.Name], finalList, ref offset, ref fieldIndex);

                //if(p.Item1.DebugType == EbxFieldType.Pointer)
                //    Console.WriteLine(p.Item1.Name);
            }

            //TypeLibrary.BuildModule(ProfilesLibrary.SDKFilename, finalList);
            using (ModuleWriter writer = new ModuleWriter("EbxClasses.dll", finalList))
            {
                writer.Write(App.FileSystemManager.Head);
            }

            if (File.Exists("EbxClasses.dll"))
            {
                FileInfo fi = new FileInfo(".\\TmpProfiles\\" + ProfilesLibrary.SDKFilename + ".dll");
                if (fi.Directory != null && !fi.Directory.Exists)
                {
                    Directory.CreateDirectory(fi.Directory.FullName);
                }
                if (fi.Exists)
                {
                    File.Delete(fi.FullName);
                }

                File.Move("EbxClasses.dll", fi.FullName);

                // Keep the source Profiles folder in sync so a freshly generated SDK
                // isn't replaced by a stale copy during the next build.
                string sourceProfiles = FindSourceProfilesDirectory();
                if (sourceProfiles != null)
                {
                    File.Copy(fi.FullName, Path.Combine(sourceProfiles, ProfilesLibrary.SDKFilename + ".dll"), true);
                }
            }
            else
            {
                Console.WriteLine("Failed to produce SDK");
                return false;
            }

            // delete type info cache if there is one
            if (File.Exists($"{App.FileSystemManager.CacheName}_typeinfo.cache"))
            {
                File.Delete($"{App.FileSystemManager.CacheName}_typeinfo.cache");
            }

            return true;
        }

        /// <summary>
        /// Resolves the source "FrostySdk\Profiles" folder by going up from the current
        /// output directory, so a freshly generated SDK can be written back next to the
        /// existing profile SDKs.
        /// </summary>
        private static string FindSourceProfilesDirectory()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "FrostySdk", "Profiles");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }

        private void LoadSharedTypeDescriptors(string name, Dictionary<string, Tuple<EbxClass, DbObject>> mapping, List<Guid> existingClasses)
        {
            Dictionary<uint, DbObject> classMapping = new Dictionary<uint, DbObject>();
            Dictionary<uint, string> hashToFieldMapping = new Dictionary<uint, string>();
            foreach (DbObject classObj in m_classList)
            {
                if (classObj.HasValue("basic"))
                {
                    continue;
                }

                classMapping.Add((uint)classObj.GetValue<int>("nameHash"), classObj);
                foreach (DbObject fieldObj in classObj.GetValue<DbObject>("fields"))
                {
                    uint fieldHash = (uint)fieldObj.GetValue<int>("nameHash");
                    if (!hashToFieldMapping.ContainsKey(fieldHash))
                    {
                        hashToFieldMapping.Add(fieldHash, fieldObj.GetValue("name", ""));
                    }
                }
            }

            EbxSharedTypeDescriptors std = new EbxSharedTypeDescriptors(App.FileSystemManager, name);

            for (int i = 0; i < std.ClassCount; i++)
            {
                if (!std.GetClass(i).HasValue)
                {
                    continue;
                }

                EbxClass theClass = std.GetClass(i).Value;
                Guid guid = std.GetGuid(i).Value;
                existingClasses.Add(guid);

                if (classMapping.TryGetValue(theClass.NameHash, out DbObject classObj))
                {
                    string className = classObj.GetValue("name", "");

                    if (mapping.ContainsKey(className))
                    {
                        mapping.Remove(className);
                        m_fieldMappings.Remove(className);
                    }

                    if (!classObj.HasValue("typeInfoGuid"))
                    {
                        classObj.SetValue("typeInfoGuid", DbObject.CreateList());
                    }
                    if (classObj.GetValue<DbObject>("typeInfoGuid").FindIndex((object a) => (Guid)a == guid) == -1)
                    {
                        classObj.GetValue<DbObject>("typeInfoGuid").Add(guid);
                    }

                    EbxClass ebxClass = new EbxClass
                    {
                        Name = className,
                        FieldCount = theClass.FieldCount,
                        Alignment = theClass.Alignment,
                        Size = theClass.Size,
                        Type = theClass.Type,
                        SecondSize = (ushort)classObj.GetValue<int>("size")
                    };

                    mapping.Add(ebxClass.Name, new Tuple<EbxClass, DbObject>(ebxClass, classObj));
                    m_fieldMappings.Add(ebxClass.Name, new List<EbxField>());

                    DbObject fieldObjs = classObj.GetValue<DbObject>("fields");
                    DbObject newFieldObjs = DbObject.CreateList();
                    classObj.RemoveValue("fields");

                    uint inheritedHash = (ProfilesLibrary.DataVersion == (int)ProfileVersion.PlantsVsZombiesBattleforNeighborville) ? 0xc4cfb854 : 0xb95a6ae7;

                    Dictionary<uint, DbObject> dumpFields = new Dictionary<uint, DbObject>();
                    foreach (DbObject fieldObj in fieldObjs)
                    {
                        if (!fieldObj.HasValue("index"))
                        {
                            continue;
                        }

                        uint nameHash = (uint)fieldObj.GetValue<int>("nameHash");
                        if (nameHash == inheritedHash)
                        {
                            continue;
                        }
                        if (!dumpFields.ContainsKey(nameHash))
                        {
                            dumpFields.Add(nameHash, fieldObj);
                        }
                    }

                    foreach (DbObject fieldObj in dumpFields.Values)
                    {
                        int dumpFlags = fieldObj.GetValue<int>("flags");
                        if (((dumpFlags >> 4) & 0x1F) == 0)
                        {
                            int dumpType = fieldObj.GetValue<int>("type");
                            fieldObj.SetValue("flags", (dumpType << 4) | (dumpFlags & 0xF));
                        }
                        newFieldObjs.Add(fieldObj);
                    }

                    for (int j = 0; j < theClass.FieldCount; j++)
                    {
                        EbxField field = std.GetField(theClass.FieldIndex + j).Value;
                        field.Name = hashToFieldMapping.TryGetValue(field.NameHash, out string mappedName) ? mappedName : "";

                        if (dumpFields.TryGetValue(field.NameHash, out DbObject fieldObj))
                        {
                            fieldObj.SetValue("type", field.Type);
                            fieldObj.SetValue("flags", field.Type);
                            fieldObj.SetValue("offset", field.DataOffset);
                            fieldObj.SetValue("value", (int)field.DataOffset);
                            if (field.DebugType == EbxFieldType.Array && field.ClassRef != ushort.MaxValue)
                            {
                                Guid arrayGuid = std.GetGuid(theClass.Index + (short)field.ClassRef).Value;
                                fieldObj.SetValue("guid", arrayGuid);
                            }
                        }
                        else if (field.NameHash != inheritedHash)
                        {
                            field.Name = (field.Name != "") ? field.Name : "Unknown_" + field.NameHash.ToString("x8");

                            DbObject newField = DbObject.CreateObject();
                            newField.SetValue("name", field.Name);
                            newField.SetValue("nameHash", (int)field.NameHash);
                            newField.SetValue("type", field.Type);
                            newField.SetValue("flags", (ushort)0);
                            newField.SetValue("offset", field.DataOffset);
                            newField.SetValue("value", (int)field.DataOffset);
                            newFieldObjs.Add(newField);
                        }

                        m_fieldMappings[ebxClass.Name].Add(field);
                    }

                    classObj.SetValue("fields", newFieldObjs);
                }
            }
        }

        private int ProcessClass(EbxClass pclass, DbObject pobj, List<EbxField> fields, DbObject outList, ref int offset, ref int fieldIndex)
        {
            string parent = pobj.GetValue<string>("parent");
            if (parent != "")
            {
                if (m_valuesByNameMap != null && m_valuesByNameMap.TryGetValue(parent, out Tuple<EbxClass, DbObject> p) && p.Item2 != null)
                {
                    offset = ProcessClass(p.Item1, p.Item2, m_fieldMappings[p.Item1.Name], outList, ref offset, ref fieldIndex);

                    if (p.Item1.Name == "DataContainer" && pclass.Name != "Asset")
                    {
                        pobj.SetValue("isData", true);
                    }
                }
            }

            // O(1) HashSet check replaces original List.Contains scan (now my life cant be wasted away at creating sdk)
            if (m_processedClasses.Contains(pclass))
            {
                if (m_outListByName.TryGetValue(pclass.Name, out DbObject t))
                {
                    fieldIndex += t.GetValue<DbObject>("fields").Count;
                    return t.GetValue<int>("size");
                }
                return 0;
            }
            m_processedClasses.Add(pclass);

            DbObject classMeta = null;
            if (m_classMetaMap != null)
            {
                m_classMetaMap.TryGetValue(pclass.Name, out classMeta);
            }

            DbObject origFieldList = pobj.GetValue<DbObject>("fields");
            DbObject fieldList = new DbObject(false);

            if (pclass.DebugType == EbxFieldType.Enum)
            {
                foreach (DbObject field in origFieldList)
                {
                    DbObject fieldObj = new DbObject();
                    fieldObj.AddValue("name", field.GetValue<string>("name"));
                    fieldObj.AddValue("value", field.GetValue<int>("value"));
                    fieldList.Add(fieldObj);
                }
            }
            else
            {
                List<EbxField> newFields = new List<EbxField>();
                foreach (DbObject field in origFieldList)
                {
                    EbxField nf = new EbxField
                    {
                        Name = field.GetValue<string>("name"),
                        Type = (ushort)field.GetValue<int>("flags"),
                        DataOffset = (uint)field.GetValue<int>("offset"),
                        NameHash = (uint)field.GetValue<int>("nameHash")
                    };
                    newFields.Add(nf);
                }
                newFields.Sort((EbxField a, EbxField b) => a.DataOffset.CompareTo(b.DataOffset));

                Dictionary<string, DbObject> origFieldByName = new Dictionary<string, DbObject>(StringComparer.Ordinal);
                foreach (DbObject a in origFieldList)
                {
                    string aName = a.GetValue<string>("name");
                    if (aName != null && !origFieldByName.ContainsKey(aName))
                    {
                        origFieldByName[aName] = a;
                    }
                }

                Dictionary<string, DbObject> metaFieldByName = null;
                if (classMeta != null)
                {
                    metaFieldByName = new Dictionary<string, DbObject>(StringComparer.Ordinal);
                    foreach (DbObject mf in classMeta.GetValue<DbObject>("fields"))
                    {
                        string mfName = mf.GetValue<string>("name");
                        if (mfName != null && !metaFieldByName.ContainsKey(mfName))
                        {
                            metaFieldByName[mfName] = mf;
                        }
                    }
                }

                foreach (EbxField field in newFields)
                {
                    if (field.DebugType == EbxFieldType.Inherited)
                    {
                        continue;
                    }

                    if (!origFieldByName.TryGetValue(field.Name, out DbObject origField))
                    {
                        Console.WriteLine(pclass.Name + "." + field.Name + " missing from executable definition");
                        continue;
                    }

                    DbObject fieldObj = new DbObject();
                    if (metaFieldByName != null)
                    {
                        if (metaFieldByName.TryGetValue(field.Name, out DbObject fieldMeta))
                        {
                            fieldObj.AddValue("meta", fieldMeta);
                        }
                    }

                    fieldObj.AddValue("name", field.Name);
                    fieldObj.AddValue("type", (int)field.DebugType);
                    fieldObj.AddValue("flags", (int)field.Type);
                    if (ProfilesLibrary.IsLoaded(ProfileVersion.Anthem, ProfileVersion.PlantsVsZombiesBattleforNeighborville,
                        ProfileVersion.Fifa20, ProfileVersion.Fifa21, ProfileVersion.Madden22,
                        ProfileVersion.Fifa22, ProfileVersion.Battlefield2042, ProfileVersion.Madden23, ProfileVersion.NeedForSpeedUnbound, ProfileVersion.DeadSpace, ProfileVersion.DragonAgeVeilguard))
                    {
                        fieldObj.AddValue("offset", (int)field.DataOffset);
                        fieldObj.AddValue("nameHash", field.NameHash);
                    }

                    if (field.DebugType == EbxFieldType.Pointer || field.DebugType == EbxFieldType.Struct || field.DebugType == EbxFieldType.Enum || field.DebugType == EbxFieldType.Delegate || field.DebugType == EbxFieldType.Function)
                    {
                        string baseTypeName = origField.GetValue<string>("baseType");

                        // Dictionary lookup instead of FindIndex
                        if (m_nonBasicValueMap != null && !string.IsNullOrEmpty(baseTypeName) && m_nonBasicValueMap.TryGetValue(baseTypeName, out var matchedValue))
                        {
                            if (field.DebugType == EbxFieldType.Delegate || field.DebugType == EbxFieldType.Function)
                            {
                                fieldObj.AddValue("baseType", $"Reflection.{matchedValue.Item1.Name}");
                            }
                            else
                            {
                                fieldObj.AddValue("baseType", matchedValue.Item1.Name);
                            }

                            if (field.DebugType == EbxFieldType.Struct)
                            {
                                foreach (EbxField ebxField in fields)
                                {
                                    if (ebxField.DebugType == EbxFieldType.Inherited && ebxField.DataOffset == 16)
                                    {
                                        pobj.SetValue("forceAlign", true);
                                        Console.WriteLine(pobj.GetValue<string>("name"));
                                    }
                                    if (ebxField.Name.Equals(field.Name))
                                    {
                                        if (field.Type != ebxField.Type)
                                        {
                                            fieldObj.SetValue("flags", (int)ebxField.Type);
                                        }
                                        break;
                                    }
                                }

                                if (matchedValue.Item1.Alignment != 0)
                                {
                                    while (offset % matchedValue.Item1.Alignment != 0)
                                    {
                                        offset++;
                                    }
                                }
                            }
                        }
                        else if (field.DebugType == EbxFieldType.Pointer)
                        {
                            // Pointer fields are always PointerRef
                            Console.WriteLine($"SDK: {pclass.Name}.{field.Name}: pointer baseType '{baseTypeName}' not resolved; emitting as PointerRef");
                        }
                        else if (string.IsNullOrEmpty(baseTypeName))
                        {
                            // Field has a complex type but no resolvable referenced class.
                            Console.WriteLine($"SDK: skipping {field.DebugType} field '{pclass.Name}.{field.Name}' (no baseType resolved)");
                            continue;
                        }
                        else if (field.DebugType == EbxFieldType.Enum)
                        {
                            throw new InvalidDataException($"Enum baseType not found: {baseTypeName} ({pclass.Name}.{field.Name})");
                        }
                        else
                        {
                            Console.WriteLine($"SDK: skipping {field.DebugType} field '{pclass.Name}.{field.Name}' (unresolved baseType '{baseTypeName}')");
                            continue;
                        }
                    }
                    else if (field.DebugType == EbxFieldType.Array)
                    {
                        string baseTypeName = origField.GetValue<string>("baseType");

                        if (m_nonBasicValueMap != null && !string.IsNullOrEmpty(baseTypeName) && m_nonBasicValueMap.TryGetValue(baseTypeName, out var matchedValue))
                        {
                            fieldObj.AddValue("baseType", matchedValue.Item1.Name);
                            fieldObj.AddValue("arrayFlags", (int)matchedValue.Item1.Type);
                        }
                        else
                        {
                            EbxFieldType arrayType = (EbxFieldType)((origField.GetValue<int>("arrayFlags") >> 4) & 0x1F);
                            if ((arrayType == EbxFieldType.Pointer || arrayType == EbxFieldType.Struct || arrayType == EbxFieldType.Enum)
                                && !string.IsNullOrEmpty(baseTypeName))
                            {
                                fieldObj.AddValue("baseType", baseTypeName);
                            }

                            fieldObj.AddValue("arrayFlags", origField.GetValue<int>("arrayFlags"));
                        }

                        if (origField.HasValue("guid"))
                        {
                            fieldObj.SetValue("guid", origField.GetValue<Guid>("guid"));
                        }
                    }

                    // Align refs to 8 byte boundaries
                    if (field.DebugType == EbxFieldType.ResourceRef
                        || field.DebugType == EbxFieldType.TypeRef
                        || field.DebugType == EbxFieldType.FileRef
                        || field.DebugType == EbxFieldType.BoxedValueRef)
                    {
                        while (offset % 8 != 0)
                        {
                            offset++;
                        }
                    }

                    // Align arrays and pointers to 4 byte boundaries
                    else if (field.DebugType == EbxFieldType.Array
                        || field.DebugType == EbxFieldType.Pointer)
                    {
                        while (offset % 4 != 0)
                        {
                            offset++;
                        }
                    }

                    if (ProfilesLibrary.DataVersion != (int)ProfileVersion.Anthem)
                    {
                        fieldObj.AddValue("offset", offset);
                    }

                    fieldObj.SetValue("index", origField.GetValue<int>("index") + fieldIndex);
                    fieldList.Add(fieldObj);

                    switch (field.DebugType)
                    {
                        case EbxFieldType.Struct:
                            {
                                string bName = fieldObj.GetValue<string>("baseType");
                                if (m_valuesByNameMap != null && m_valuesByNameMap.TryGetValue(bName, out var s))
                                {
                                    int structOffset = 0;
                                    int structFieldIndex = 0;

                                    offset += ProcessClass(s.Item1, s.Item2, m_fieldMappings[s.Item1.Name], outList, ref structOffset, ref structFieldIndex);
                                }
                            }
                            break;
                        case EbxFieldType.Pointer:
                        case EbxFieldType.Array:
                        case EbxFieldType.CString:
                        case EbxFieldType.Enum:
                        case EbxFieldType.Int32:
                        case EbxFieldType.UInt32:
                        case EbxFieldType.Float32:
                            offset += 4;
                            break;
                        case EbxFieldType.String:
                            offset += 32;
                            break;
                        case EbxFieldType.FileRef:
                        case EbxFieldType.Int64:
                        case EbxFieldType.UInt64:
                        case EbxFieldType.Float64:
                        case EbxFieldType.ResourceRef:
                        case EbxFieldType.TypeRef:
                            offset += 8;
                            break;
                        case EbxFieldType.Boolean:
                        case EbxFieldType.Int8:
                        case EbxFieldType.UInt8:
                            offset += 1;
                            break;
                        case EbxFieldType.Int16:
                        case EbxFieldType.UInt16:
                            offset += 2;
                            break;
                        case EbxFieldType.Guid:
                        case EbxFieldType.BoxedValueRef:
                            offset += 16;
                            break;
                        case EbxFieldType.Sha1:
                            offset += 20;
                            break;
                    }
                }
            }
            if (pclass.Alignment != 0)
            {
                while (offset % pclass.Alignment != 0)
                {
                    offset++;
                }
            }

            pobj.SetValue("flags", (int)pclass.Type);

            pobj.SetValue("size", pclass.Size != 0 ? pclass.Size : offset);

            if (pclass.DebugType == EbxFieldType.Enum)
            {
                pobj.SetValue("size", 4);
            }
            pobj.SetValue("alignment", (int)pclass.Alignment);
            pobj.SetValue("fields", fieldList);
            fieldIndex += fieldList.Count;

            if (classMeta != null)
            {
                pobj.AddValue("meta", classMeta);
                foreach (DbObject fieldMeta in classMeta.GetValue<DbObject>("fields"))
                {
                    if (fieldMeta.GetValue<bool>("added", false))
                    {
                        DbObject fieldObj = new DbObject();
                        fieldObj.AddValue("name", fieldMeta.GetValue<string>("name"));
                        fieldObj.AddValue("type", (int)EbxFieldType.Int32);
                        fieldObj.AddValue("meta", fieldMeta);

                        pobj.GetValue<DbObject>("fields").Add(fieldObj);
                    }
                }
            }

            if (!m_outListByName.ContainsKey(pclass.Name))
            {
                m_outListByName[pclass.Name] = pobj;
            }
            outList.Add(pobj);
            return offset;
        }

        private DbObject DumpClasses(SdkUpdateTask task)
        {
            MemoryReader reader;
            string Namespace = "Frosty.Core.Sdk.ClassesSdkCreator+";

            // Anthem
            if (ProfilesLibrary.IsLoaded(ProfileVersion.Anthem))
            {
                Namespace = "Frosty.Core.Sdk.Anthem.";
            }
            // Bf2042
            else if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa21, ProfileVersion.Madden22,
                ProfileVersion.Fifa22, ProfileVersion.Battlefield2042,
                ProfileVersion.Madden23, ProfileVersion.NeedForSpeedUnbound, ProfileVersion.DeadSpace, ProfileVersion.DragonAgeVeilguard))
            {
                Namespace = "Frosty.Core.Sdk.Bf2042.";
            }
            // All others
            else if (ProfilesLibrary.IsLoaded(ProfileVersion.Madden20, ProfileVersion.PlantsVsZombiesBattleforNeighborville,
                ProfileVersion.Fifa20, ProfileVersion.NeedForSpeedHeat))
            {
                Namespace = "Frosty.Core.Sdk.Madden20.";
            }

            long origOffset = m_state.TypeInfoOffset;
            reader = new MemoryReader(m_state.Process, origOffset);

            // cleanup
            m_offsetClassInfoMappings.Clear();
            m_classInfos.Clear();
            m_alreadyProcessedClasses.Clear();
            m_processedClasses.Clear();
            m_outListByName.Clear();
            m_fieldMappings?.Clear();

            NextOffset = origOffset;
            int count = 0;

            while (NextOffset != 0)
            {
                task.StatusMessage = $"Found {++count} type(s)";
                reader.Position = NextOffset;

                ClassInfo info = (ClassInfo)Activator.CreateInstance(Type.GetType(Namespace + "ClassInfo"));
                info.Read(reader);

                m_classInfos.Add(info);
                m_offsetClassInfoMappings.Add(origOffset, info);

                if (NextOffset != 0)
                {
                    origOffset = NextOffset;
                    //offset = AdjustOffset(offset);
                }
            }

            reader.Dispose();

            DbObject classList = new DbObject(false);
            m_classInfos.Sort((ClassInfo a, ClassInfo b) => a.TypeInfo.Name.CompareTo(b.TypeInfo.Name));

            foreach (ClassInfo classInfo in m_classInfos)
            {
                if (classInfo.TypeInfo.Type == 2 || classInfo.TypeInfo.Type == 3 || classInfo.TypeInfo.Type == 8 || classInfo.TypeInfo.Type == 0x1B)
                {
                    if (classInfo.TypeInfo.Type == 0x1B)
                    {
                        //Console.WriteLine(classInfo.typeInfo.name);
                        classInfo.TypeInfo.Flags = (3 << 4);
                    }
                    CreateClassObject(classInfo, ref classList);
                }
                else if (classInfo.TypeInfo.Type == 0x1c || classInfo.TypeInfo.Type == 0x18)
                {
                    CreateDelegateObject(classInfo, ref classList);
                }
                else if (classInfo.TypeInfo.Type != 4)
                {
                    CreateBasicClassObject(classInfo, ref classList);
                }
            }

            return classList;
        }

        private void CreateBasicClassObject(ClassInfo classInfo, ref DbObject classList)
        {
            int alignment = classInfo.TypeInfo.Alignment;
            int size = (int)classInfo.TypeInfo.Size;

            m_offsetClassInfoMappings.TryGetValue(classInfo.TypeInfo.ArrayTypeOffset, out ClassInfo arrayType);

            DbObject classObj = DbObject.CreateObject();
            classObj.SetValue("name", classInfo.TypeInfo.Name);
            classObj.SetValue("type", classInfo.TypeInfo.Type);
            classObj.SetValue("flags", (int)classInfo.TypeInfo.Flags);
            classObj.SetValue("alignment", alignment);
            classObj.SetValue("size", size);
            classObj.SetValue("runtimeSize", size);
            if (classInfo.TypeInfo.Guid != Guid.Empty)
            {
                classObj.SetValue("guid", classInfo.TypeInfo.Guid);
            }
            if (arrayType != null && arrayType.TypeInfo.Guid != Guid.Empty)
            {
                classObj.AddValue("arrayGuid", arrayType.TypeInfo.Guid);
            }
            classObj.SetValue("namespace", classInfo.TypeInfo.NameSpace);
            classObj.SetValue("fields", DbObject.CreateList());
            classObj.SetValue("parent", "");
            classObj.SetValue("basic", true);

            classInfo.TypeInfo.Modify(classObj, m_offsetClassInfoMappings);
            classList.Add(classObj);
        }

        private void CreateClassObject(ClassInfo classInfo, ref DbObject classList)
        {
            if (m_alreadyProcessedClasses.Contains(classInfo.TypeInfo.Name))
            {
                return;
            }

            m_offsetClassInfoMappings.TryGetValue(classInfo.ParentClass, out ClassInfo parent);
            m_offsetClassInfoMappings.TryGetValue(classInfo.TypeInfo.ArrayTypeOffset, out ClassInfo arrayType);
            if (parent != null)
            {
                CreateClassObject(parent, ref classList);
            }

            int alignment = classInfo.TypeInfo.Alignment;
            int size = (int)classInfo.TypeInfo.Size;

            DbObject classObj = new DbObject();
            classObj.AddValue("name", classInfo.TypeInfo.Name);
            classObj.AddValue("parent", (parent != null) ? parent.TypeInfo.Name : "");
            classObj.AddValue("type", classInfo.TypeInfo.Type);
            classObj.AddValue("flags", (int)classInfo.TypeInfo.Flags);
            classObj.AddValue("alignment", alignment);
            classObj.AddValue("size", size);
            classObj.AddValue("runtimeSize", size);
            classObj.AddValue("additional", (int)classInfo.IsDataContainer);
            classObj.AddValue("namespace", classInfo.TypeInfo.NameSpace);
            if (classInfo.TypeInfo.Guid != Guid.Empty)
            {
                classObj.AddValue("guid", classInfo.TypeInfo.Guid);
            }
            if (arrayType != null && arrayType.TypeInfo.Guid != Guid.Empty)
            {
                classObj.AddValue("arrayGuid", arrayType.TypeInfo.Guid);
            }

            classInfo.TypeInfo.Modify(classObj, m_offsetClassInfoMappings);

            DbObject fieldList = new DbObject(false);
            foreach (FieldInfo field in classInfo.TypeInfo.Fields)
            {
                DbObject fieldObj = new DbObject();
                if (classInfo.TypeInfo.Type == 8)
                {
                    fieldObj.AddValue("name", field.Name);
                    fieldObj.AddValue("value", (int)field.TypeOffset);
                }
                else
                {
                    ClassInfo fieldType = m_offsetClassInfoMappings[field.TypeOffset];
                    fieldObj.AddValue("name", field.Name);
                    fieldObj.AddValue("type", fieldType.TypeInfo.Type);
                    fieldObj.AddValue("flags", (int)fieldType.TypeInfo.Flags);
                    fieldObj.AddValue("offset", (int)field.Offset);
                    fieldObj.AddValue("index", (int)field.Index);
                    if (fieldType.TypeInfo.Type == 3 || fieldType.TypeInfo.Type == 2 || fieldType.TypeInfo.Type == 8
                        || fieldType.TypeInfo.Type == 0x1c || fieldType.TypeInfo.Type == 0x18 || fieldType.TypeInfo.Type == 0x1b)
                    {
                        fieldObj.AddValue("baseType", fieldType.TypeInfo.Name);
                    }
                    else if (fieldType.TypeInfo.Type == 4)
                    {
                        fieldType = m_offsetClassInfoMappings[fieldType.ParentClass];
                        fieldObj.AddValue("baseType", fieldType.TypeInfo.Name);
                        fieldObj.AddValue("arrayFlags", (int)fieldType.TypeInfo.Flags);
                    }
                }
                field.Modify(fieldObj);
                fieldList.Add(fieldObj);
            }

            classObj.AddValue("fields", fieldList);
            classList.Add(classObj);

            m_alreadyProcessedClasses.Add(classInfo.TypeInfo.Name);
        }

        private void CreateDelegateObject(ClassInfo classInfo, ref DbObject classList)
        {
            if (m_alreadyProcessedClasses.Contains(classInfo.TypeInfo.Name))
            {
                return;
            }

            int alignment = classInfo.TypeInfo.Alignment;
            int size = (int)classInfo.TypeInfo.Size;

            m_offsetClassInfoMappings.TryGetValue(classInfo.TypeInfo.ArrayTypeOffset, out ClassInfo arrayType);

            DbObject classObj = DbObject.CreateObject();
            classObj.SetValue("name", classInfo.TypeInfo.Name);
            classObj.SetValue("type", classInfo.TypeInfo.Type);
            classObj.SetValue("flags", (int)classInfo.TypeInfo.Flags);
            classObj.SetValue("alignment", alignment);
            classObj.SetValue("size", size);
            classObj.SetValue("runtimeSize", size);
            if (classInfo.TypeInfo.Guid != Guid.Empty)
            {
                classObj.SetValue("guid", classInfo.TypeInfo.Guid);
            }
            if (arrayType != null && arrayType.TypeInfo.Guid != Guid.Empty)
            {
                classObj.AddValue("arrayGuid", arrayType.TypeInfo.Guid);
            }
            classObj.SetValue("namespace", classInfo.TypeInfo.NameSpace);
            classObj.SetValue("fields", DbObject.CreateList());
            classObj.SetValue("parent", "");

            classInfo.TypeInfo.Modify(classObj, m_offsetClassInfoMappings);

            DbObject parameterList = new DbObject(false);

            // delegates only have a signature string in memory, not an array of ParameterInfo arguments.
            // Skip parameter array processing if profile is pvz bnf
            if (!ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville))
            {
                foreach (ParameterInfo parameter in classInfo.TypeInfo.Parameters)
                {
                    DbObject parameterObj = new DbObject();

                    // Safely retrieve the parameter type mapping
                    if (m_offsetClassInfoMappings.TryGetValue(parameter.TypeOffset, out ClassInfo parameterType))
                    {
                        parameterObj.AddValue("name", parameter.Name);
                        parameterObj.AddValue("type", parameterType.TypeInfo.Type);
                        parameterObj.AddValue("flags", (int)parameterType.TypeInfo.Flags);
                        parameterObj.AddValue("baseType", parameterType.TypeInfo.Name);
                        parameterObj.AddValue("defaultValue", parameter.DefaultValue);
                        parameterObj.AddValue("parameterType", parameter.Type);

                        parameter.Modify(parameterObj);
                        parameterList.Add(parameterObj);
                    }
                }
            }

            classObj.AddValue("parameters", parameterList);
            classList.Add(classObj);

            m_alreadyProcessedClasses.Add(classInfo.TypeInfo.Name);
        }

        //public static long AdjustOffset(long inOffset)
        //{
        //    for (int i = 0; i < sections.Length; i++)
        //    {
        //        if ((ulong)inOffset >= (optHeader.ImageBase + sections[i].VirtualAddress) && (ulong)inOffset < (optHeader.ImageBase + sections[i].VirtualAddress + sections[i].VirtualSize))
        //        {
        //            inOffset -= (sections[i].VirtualAddress - sections[i].PointerToRawData);
        //            inOffset -= (long)optHeader.ImageBase;
        //            break;
        //        }
        //    }

        //    return inOffset;
        //}
    }
}
