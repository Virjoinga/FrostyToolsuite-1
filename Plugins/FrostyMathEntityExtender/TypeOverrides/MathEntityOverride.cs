using Frosty.Core.Controls.Editors;
using Frosty.Core.Misc;
using FrostySdk.Attributes;
using FrostySdk.IO;
using FrostySdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.Eventing.Reader;
using Frosty.Core;
using Frosty.Core.Controls;
using System.Reflection;
using System.Windows;
using Frosty.Core.Windows;

namespace MathExtender.TypeOverrides
{

    #region --Combo Editors--
    public class VariableType : FrostyCustomComboDataEditor<string, string>
    {
    }

    public class OriginalVariableType : FrostyCustomComboDataEditor<string, string>
    {
    }

    public class InputType : FrostyCustomComboDataEditor<string, string>
    {
    }

    public class MathType : FrostyCustomComboDataEditor<string, string>
    {
    }

    public class MathInput : FrostyCustomComboDataEditor<string, string>
    {
    }

    public class FunctionCallInput : FrostyCustomComboDataEditor<string, string>
    {
    }

    public class FunctionCallType : FrostyCustomComboDataEditor<string, string>
    {
    }
    #endregion

    #region --Custom Types--
    [EbxClassMeta(EbxFieldType.Struct)]
    public class VariableEntity
    {
        [DisplayName("Type")]
        [Description("The type of variable this is")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(VariableType))]
        public CustomComboData<string, string> VariableTypes { get; set; }

        [IsHidden]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(OriginalVariableType))]
        public CustomComboData<string, string> OriginalType { get; set; }

        [IsHidden]
        [EbxFieldMeta(EbxFieldType.String)]
        public string Identifier { get; set; }

        [DisplayName("Value")]
        [Description("The value of this variable. If it is a Boolean, 0 will equal false 1 will equal true.\nNOTICE: Floats will be displayed incorrectly. I do not know of a way to reliably convert them(if you know of a possible way to extract an approximate value of a float from the base10, please contact ywingpilot2)")]
        [EbxFieldMeta(EbxFieldType.Float32)]
        public float VariableInput { get; set; }
    }

    [EbxClassMeta(EbxFieldType.Struct)]
    public class InputEntity
    {
        [DisplayName("Type")]
        [Description("The type of variable this is")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(InputType))]
        public CustomComboData<string, string> InputTypes { get; set; }

        [IsHidden]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(VariableType))]
        public CustomComboData<string, string> OriginalType { get; set; }

        [IsHidden]
        [EbxFieldMeta(EbxFieldType.String)]
        public string Identifier { get; set; }

        [DisplayName("Input name")]
        [Description("The name of the input in the property connections")]
        [EbxFieldMeta(EbxFieldType.String)]
        public string VariableInput { get; set; }
    }

    [EbxClassMeta(EbxFieldType.Struct)]
    public class MathInstructionEntity
    {

        [DisplayName("Operation")]
        [Description("The math operation we should be doing\n")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(MathType))]
        public CustomComboData<string, string> MathTypes { get; set; }

        [DisplayName("Operation Type")]
        [Description("If this is set to Function or Field, this will specify what the instruction needs to do.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(MathType))]
        public CustomComboData<string, string> FunctionTypes { get; set; }

        [IsHidden]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(VariableType))]
        public CustomComboData<string, string> OriginalType { get; set; }

        [IsHidden]
        [EbxFieldMeta(EbxFieldType.String)]
        public string Identifier { get; set; } //This value will likely always be null, since only vectors and transforms can have valid identifiers

        [DisplayName("Output to")]
        [Description("The variable this instruction should output its result to")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FunctionCallInput))]
        public CustomComboData<string, string> OutputTo { get; set; }

        [IsHidden]
        [EbxFieldMeta(EbxFieldType.String)]
        public string ReferencedValue { get; set; }

        [DisplayName("Inputs")]
        [Description("The inputs for this instruction. These reference any of the other items in the Math Entity such as inputs, variables, and some instructions.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<FunctionInputEntity> FunctionInputs { get; set; } = new List<FunctionInputEntity>();
    }

    [EbxClassMeta(EbxFieldType.Struct)]
    public class FunctionInputEntity
    {
        [DisplayName("Input")]
        [Description("An input for the Instruction. This references the Variables and Inputs, as well as some other instructions.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FunctionCallInput))]
        public CustomComboData<string, string> FunctionInput { get; set; }

        [IsHidden]
        [EbxFieldMeta(EbxFieldType.String)]
        public string ReferencedValue { get; set; }
    }
    #endregion

    public class MathEntityDataOverride : BaseTypeOverride
    {

        #region --Properties--
        [DisplayName("Variables")]
        [Description("Variables for the math entity to declare. Variables are values such as bools or ints which can be set and modified")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<VariableEntity> VariableEntities { get; set; } = new List<VariableEntity>();

        [DisplayName("Property Inputs")]
        [Description("Property connection inputs to create and use")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<InputEntity> InputEntities { get; set; } = new List<InputEntity>();

        [DisplayName("Instructions")]
        [Description("The math we need to do.\nThe last Math Instruction Entity in the created Math Instructions is the value that will be outputted")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<MathInstructionEntity> MathInstructions { get; set; } = new List<MathInstructionEntity>();

        [DisplayName("Output Type")]
        [Description("The type of value this math entity sends to the Out property connection.\nThe last Math Instruction Entity in the created Math Instructions is the value that will be outputted")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(InputType))]
        public CustomComboData<string, string> ReturnTypes { get; set; }

        [IsHidden]
        public BaseFieldOverride Assembly { get; set; }
        #endregion

        #region --Lists--
        //Create our lists to compare to

        #region --MathOpCodes--
        //These lists are for the original math entity equivalents

        //Total list of all of the MathOpCodes avaliable
        //This list will be remade if the Load method deems it inaccurate
        List<string> MathOpCodes = new List<string>()
            {
                "MathOpCode_ConstB",
                "MathOpCode_ConstI",
                "MathOpCode_ConstF",
                "MathOpCode_InputB",
                "MathOpCode_InputI",
                "MathOpCode_InputF",
                "MathOpCode_InputV2",
                "MathOpCode_InputV3",
                "MathOpCode_InputV4",
                "MathOpCode_InputT",
                "MathOpCode_OrB",
                "MathOpCode_AndB",
                "MathOpCode_GreaterI",
                "MathOpCode_GreaterF",
                "MathOpCode_GreaterEqI",
                "MathOpCode_GreaterEqF",
                "MathOpCode_LessI",
                "MathOpCode_LessF",
                "MathOpCode_LessEqI",
                "MathOpCode_LessEqF",
                "MathOpCode_NotEqI",
                "MathOpCode_NotEqF",
                "MathOpCode_NotEqB",
                "MathOpCode_EqI",
                "MathOpCode_EqF",
                "MathOpCode_EqB",
                "MathOpCode_AddI",
                "MathOpCode_AddF",
                "MathOpCode_AddV2",
                "MathOpCode_AddV3",
                "MathOpCode_AddV4",
                "MathOpCode_SubI",
                "MathOpCode_SubF",
                "MathOpCode_SubV2",
                "MathOpCode_SubV3",
                "MathOpCode_SubV4",
                "MathOpCode_MulF",
                "MathOpCode_MulI",
                "MathOpCode_MulV2F",
                "MathOpCode_MulV3F",
                "MathOpCode_MulV4F",
                "MathOpCode_MulV2I",
                "MathOpCode_MulV3I",
                "MathOpCode_MulV4I",
                "MathOpCode_MulT",
                "MathOpCode_DivI",
                "MathOpCode_DivF",
                "MathOpCode_DivV2F",
                "MathOpCode_DivV3F",
                "MathOpCode_DivV4F",
                "MathOpCode_DivV2I",
                "MathOpCode_DivV3I",
                "MathOpCode_DivV4I",
                "MathOpCode_ModI",
                "MathOpCode_ModF",
                "MathOpCode_NegI",
                "MathOpCode_NegF",
                "MathOpCode_NegV2",
                "MathOpCode_NegV3",
                "MathOpCode_NegV4",
                "MathOpCode_NotB",
                "MathOpCode_PowI",
                "MathOpCode_PowF",
                "MathOpCode_FieldV2",
                "MathOpCode_FieldV3",
                "MathOpCode_FieldV4",
                "MathOpCode_FieldT",
                "MathOpCode_Func",
                "MathOpCode_Return"
            };

        //the names of the Variable types for the MathOpCodes
        List<string> MathOpVariableTypes = new List<string>()
            {
                "MathOpCode_ConstB",
                "MathOpCode_ConstI",
                "MathOpCode_ConstF",
            };

        //the names of the Input types for the MathOpCodes
        List<string> MathOpInputTypes = new List<string>()
            {
                "MathOpCode_InputB",
                "MathOpCode_InputI",
                "MathOpCode_InputF",
                "MathOpCode_InputV2",
                "MathOpCode_InputV3",
                "MathOpCode_InputV4",
                "MathOpCode_InputT"
            };

        //The values that correspond to Return Types
        List<string> MathOpReturnValues = new List<string>()
            {
                "1", //Bool
                "2", //Int
                "4", //Float
                "8", //Vec2
                "16", //Vec3
                "32", //Vec4
                "64" //Transform
            };

        //The MathOpCodes for the actual math we could do, e.g addition subtraction
        List<string> MathOpCodeTypes = new List<string>()
            {
                "MathOpCode_Func",
                "MathOpCode_AddI",
                "MathOpCode_AddF",
                "MathOpCode_AddV2",
                "MathOpCode_AddV3",
                "MathOpCode_AddV4",
                "MathOpCode_SubI",
                "MathOpCode_SubF",
                "MathOpCode_SubV2",
                "MathOpCode_SubV3",
                "MathOpCode_SubV4",
                "MathOpCode_MulF",
                "MathOpCode_MulI",
                "MathOpCode_MulV2F",
                "MathOpCode_MulV2I",
                "MathOpCode_MulV3F",
                "MathOpCode_MulV3I",
                "MathOpCode_MulV4F",
                "MathOpCode_MulV4I",
                "MathOpCode_DivF",
                "MathOpCode_DivI",
                "MathOpCode_DivV2F",
                "MathOpCode_DivV2I",
                "MathOpCode_DivV3F",
                "MathOpCode_DivV3I",
                "MathOpCode_DivV4F",
                "MathOpCode_DivV4I",
                "MathOpCode_ModI",
                "MathOpCode_ModF",
                "MathOpCode_PowerF",
                "MathOpCode_PowerI",
                "MathOpCode_NegI",
                "MathOpCode_NegF",
                "MathOpCode_NegV2",
                "MathOpCode_NegV3",
                "MathOpCode_NegV4",
                "MathOpCode_OrB",
                "MathOpCode_AndB",
                "MathOpCode_NotB",
                "MathOpCode_GreaterI",
                "MathOpCode_GreaterF",
                "MathOpCode_GreaterEqF",
                "MathOpCode_GreaterEqI",
                "MathOpCode_EqI",
                "MathOpCode_EqF",
                "MathOpCode_EqB",
                "MathOpCode_LessI",
                "MathOpCode_LessF",
                "MathOpCode_LessEqI",
                "MathOpCode_LessEqF",
                "MathOpCode_NotEqI",
                "MathOpCode_NotEqF",
                "MathOpCode_NotEqB",
                "MathOpCode_FieldV2",
                "MathOpCode_FieldV3",
                "MathOpCode_FieldV4",
                "MathOpCode_FieldT"
            };

        //The original names for all of the functions, the Split stuff is set to Invalid since they are not real functions.
        List<string> MathOpFunctions = new List<string>()
        {
            "none",
            "none",
            "none",
            "none",
            "none",
            "none",
            "none",
            "none",
            "none",
            "absf",
            "absi",
            "mini",
            "minf",
            "maxi",
            "maxf",
            "cos",
            "sin",
            "tan",
            "acos",
            "asin",
            "atan",
            "atan2",
            "sqrtf",
            "sqrti",
            "lerpf",
            "clampf",
            "clampi",
            "floati",
            "floatb",
            "intf",
            "intb",
            "round",
            "ceil",
            "floor",
            "signf",
            "signi",
            "vec2",
            "vec3",
            "vec4",
            "dotv2",
            "dotv3",
            "dotv4",
            "cross",
            "normv2",
            "normv3",
            "normv4",
            "lerpv2",
            "lerpv3",
            "lerpv4",
            "slerp",
            "distancev2",
            "distancev3",
            "distancev4",
            "normalv2",
            "normalv3",
            "normalv4",
            "translation",
            "rotationx",
            "rotationy",
            "rotationz",
            "scale",
            "rotationAndTranslation",
            "lookAtTransform",
            "inverse",
            "fullverse",
            "rotate",
            "invRotate",
            "transform",
            "invTransform",
            "isWorldSpaceTransform",
            "asWorldSpaceTransform",
            "asLocalSpaceTransform",
            "ifb",
            "ifi",
            "iff",
            "ifv2",
            "ifv3",
            "ifv4",
            "ift", // <3
            "xorb"
        };
        #endregion

        #region --User Defined Variables--
        //These are the varialbe type names the user sees in the drop downs
        List<string> MathVariableTypes = new List<string>()
            {
                "Boolean", //MathOpCode_ConstB
                "Integer", //MathOpCode_ConstI
                "Float", //MathOpCode_ConstF
            };

        //These are the Input type names the user sees in the drop downs
        List<string> MathInputTypes = new List<string>()
            {
                "Boolean", //MathOpCode_InputB
                "Integer", //MathOpCode_InputI
                "Float", //MathOpCode_InputF
                "Vector 2", //MathOpCode_InputV2
                "Vector 3", //MathOpCode_InputV3
                "Vector 4", //MathOpCode_InputV4
                "Transform" //MathOpCode_InputT
            };

        //These are the Operation type names the user sees in the drop downs
        //These are ordered Param1 type, Param2 type(if it has a different type, if not just do the param1 type), Return type
        List<string> MathOperationTypes = new List<string>()
            {
                "Function: Any, Any", //MathOpCode_Func
                "Add: Int, Int", //MathOpCode_AddI
                "Add: Float, Float", //MathOpCode_AddF
                "Add: Vec2, Vec2", //MathOpCode_AddV2
                "Add: Vec3, Vec3", //MathOpCode_AddV3
                "Add: Vec4, Vec4", //MathOpCode_AddV4
                "Subtract: Int, Int", //MathOpCode_SubI
                "Subtract: Float, Float", //MathOpCode_SubF
                "Subtract: Vec2, Vec2", //MathOpCode_SubV2
                "Subtract: Vec3, Vec3", //MathOpCode_SubV3
                "Subtract: Vec4, Vec4", //MathOpCode_SubV4
                "Multiply: Float, Float", //MathOpCode_MulF
                "Multiply: Int, Int", //MathOpCode_MulI
                "Multiply: Vec2, Float, Vec2", //MathOpCode_MulV2F
                "Multiply: Vec2, Int, Vec2", //MathOpCode_MulV2I
                "Multiply: Vec3, Float, Vec3", //MathOpCode_MulV3F
                "Multiply: Vec3, Int, Vec3", //MathOpCode_MulV3I
                "Multiply: Vec4, Float, Vec4", //MathOpCode_MulV4F
                "Multiply: Vec4, Int, Vec4", //MathOpCode_MulV4I
                "Divide: Float, Float", //MathOpCode_DivF
                "Divide: Int, Int", //MathOpCode_DivI
                "Divide: Vec2, Float, Vec2", //MathOpCode_DivV2F
                "Divide: Vec2, Int, Vec2", //MathOpCode_V2I
                "Divide: Vec3, Float, Vec3", //MathOpCode_V3F
                "Divide: Vec3, Int, Vec3", //MathOpCode_V3I
                "Divide: Vec4, Float, Vec4", //MathOpCode_V4F
                "Divide: Vec4, Int, Vec4", //MathOpCode_V4I
                "Modulo: Int, Int", //MathOpCode_ModI
                "Modulo: Float, Float", //MathOpCode_ModF
                "Power: Float, Float", //MathOpCode_PowF
                "Power: Int, Int", //MathOpCode_PowI
                "Negative: Int, Int", //MathOpCode_NegI
                "Negative: Float, Float", //MathOpCode_NegF
                "Negative: Vec2, Vec2", //MathOpCode_NegV2
                "Negative: Vec3, Vec3", //MathOpCode_NegV3
                "Negative: Vec4, Vec4", //MathOpCode_NegV4
                "OR: Bool, Bool", //MathOpCode_OrB
                "AND: Bool, Bool", //MathOpCode_AndB
                "NOT: Bool, Bool",//MathOpCode_NotB
                ">: Int, Bool", //MathOpCode_GreaterI
                ">: Float, Bool", //MathOpCode_GreaterF
                ">=: Float, Bool", //MathOpCode_GreaterEqF
                ">=: Int, Bool", //MathOpCode_GreaterEqI
                "==: Int, Bool", //MathOpCode_EqI
                "==: Float, Bool", //MathOpCode_EqF
                "==: Bool, Bool", //MathOpCode_EqB
                "<: Int, Bool", //MathOpCode_LessI
                "<: Float, Bool", //MathOpCode_LessF
                "<=: Int, Bool", //MathOpCode_LessEqI
                "<=: Float, Bool", //MathOpCode_LessEqF
                "!=: Int, Bool", //MathOpCode_NotEqI
                "!=: Float, Bool", //MathOpCode_NotEqF
                "!=: Bool, Bool", //MathOpCode_NotEqB
                "Split Vec2: Vec2, Float", //MathOpCode_FieldV2
                "Split Vec3: Vec3, Float", //MathOpCode_FieldV3
                "Split Vec4: Vec4, Float", //MathOpCode_FieldV4
                "Split Transform: Transform, Float" //MathOpCode_FieldT
            };

        //A list of all of the functions, + options for Splitting vectors(aka fields)
        //If this list is deemed inaccurate by the load function, it will be added onto but nothing existing will be modified
        List<string> MathFunctionTypes = new List<string>()
        {
            "none",
            "Split X: Split vector, I: Any vector type, O: Float",
            "Split Y: Split vector, I: Any vector type, O: Float",
            "Split Z: Split vector, I: Vec3-4, O: Float",
            "Split W: Split vector, I: Vec4, O: Float",
            "Split left: Split transform, I: Transform O: Float",
            "Split up: Split transform, I: Transform O: Float",
            "Split forward: Split transform, I: Transform O: Float",
            "Split transform, I: Transform O: Vector3",
            "absf: Function, I: Float O: Float",
            "absi: Function, I: Int, O: Int",
            "mini: Function, I: Int, Int O: Int",
            "minf: Function, I: Float, Float O: Float",
            "maxi: Function, I: Int, Int O: Int",
            "maxf: Function, I: Float, Float O: Float",
            "cos: Function, I: Float O: Float",
            "sin: Function, I: Float O: Float",
            "tan: Function, I: Float O: Float",
            "acos: Function, I: Float O: Float",
            "asin: Function, I: Float O: Float",
            "atan: Function, I: Float O: Float",
            "atan2: Function, I: Float O: Float",
            "sqrtf: Function, I: Float O: Float",
            "sqrti: Function, I: Int O: Int",
            "lerpf: Function, I: Float, Float, Float O: Float",
            "clampf: Function, I: Float, Float, Float O: Float",
            "clampi: Function, I: Int, Int, Int O: Int",
            "floati: Function, I: Int O: Float",
            "floatb: Function, I: Bool O: Float",
            "intf: Function, I: Float O: Int",
            "intb: Function, I: Bool O: Int",
            "round: Function, I: Float O: Float",
            "ceil: Function, I: Float O: Float",
            "floor: Function, I: Float O: Float",
            "signf: Function, I: Float O: Float",
            "signi: Function, I: Float O: Float",
            "vec2: Function, I: Float, Float O: Vec2",
            "vec3: Function, I: Float, Float, Float O: Vec3",
            "vec4: Function, I: Float, Float, Float, Float O: Vec4",
            "dotv2: Function, I: Vec2, Vec2 O: Float",
            "dotv3: Function, I: Vec3, Vec3 O: Float",
            "dotv4: Function, I: Vec4, Vec4 O: Float",
            "cross: Function, I: Vec3, Vec3 O: Vec3",
            "normv2: Function, I: Float O: Vec2",
            "normv3: Function, I: Float O: Vec3",
            "normv4: Function, I: Float O: Vec4",
            "lerpv2: Function, I: Vec2, Vec2, Float O: Vec2",
            "lerpv3: Function, I: Vec3, Vec3, Float O: Vec3",
            "lerpv4: Function, I: Vec4, Vec4, Float O: Vec4",
            "slerp: Function, I: Vec3, Vec3, Float O: Vec3",
            "distancev2: Function, I: Vec2, Vec2 O: Float",
            "distancev3: Function, I: Vec3, Vec3 O: Float",
            "distancev4: Function, I: Vec4, Vec4 O: Float",
            "normalv2: Function, I: Vec2 O: Vec2",
            "normalv3: Function, I: Vec3 O: Vec3",
            "normalv4: Function, I: Vec4 O: Vec4",
            "translation: Function, I: Unknown, Vec3 O: Vec3",
            "rotationx: Function, I: Float O: Transform",
            "rotationy: Function, I: Float O: Transform",
            "rotationz: Function, I: Float O: Transform",
            "scale: Function, I: Vec3 O: Trans",
            "rotationAndTranslation: Function, I: Trans, Vec3 O: Trans",
            "lookAtTransform: Function, I: Vec3 O: Trans",
            "inverse: Function, I: Transform O: Transform",
            "fullverse: Function, I: Transform O: Transform",
            "rotate: Function I: Vec3, Transform O: Vec3",
            "invRotate: Function, I: Vec3, Transform O: Vec3",
            "transform: Function, I: Vec3, Transform O: Vec3",
            "invTransform: Function, I: Vec3, Transform O: Vec3",
            "isWorldSpaceTransform: Function, I: Transform O: Bool",
            "asWorldSpaceTransform: Function, I: Transform O: Transform",
            "asLocalSpaceTransform: Function, I: Transform O: Transform",
            "ifb: Function, I: Bool, Bool, Bool O: Bool",
            "ifi: Function, I: Bool, Int, Int O: Int",
            "iff: Function, I: Bool, Float, Float O: Float",
            "ifv2: Function, I: Bool, Vec2, Vec2 O: Vec2",
            "ifv3: Function, I: Bool, Vec3, Vec3 O: Vec3",
            "ifv4: Function, I: Bool, Vec4, Vec4 O: Vec4",
            "ift: Function, I: Bool, Trans, Trans O: Trans", // <3
            "xorb: Function, I: Bool, Bool, O: Bool"
        };
        #endregion

        //List of the references for math entity references
        List<string> MathVariables = new List<string>();
        List<string> MathVariableIds = new List<string>(); //each Variable Input and Instruction has a unique ID, which we keep track of too for saving purposes
        #endregion

        //Decompiling
        public override void Load()
        {
            //Assign the original type to a variable
            dynamic OriginalValues = Original;

            //Make sure we know what we are doing by checking if our lists are accurate
            Array mathOpCodeArray = ((object)TypeLibrary.CreateObject("MathOpCode")).GetType().GetEnumValues();
            List<dynamic> mathOpCodeObjects = new List<dynamic>();
            mathOpCodeObjects.AddRange(mathOpCodeArray.Cast<dynamic>());

            //If our lists aren't accurate we need to remake them
            if (MathOpCodes.Count != mathOpCodeObjects.Count)
            {
                List<string> newOpCodes = new List<string>(); //A list of new opcodes, this is separate for now to compare later on
                foreach (dynamic op in mathOpCodeObjects)
                {
                    object opCode = op as object; //Get the object of the OpCode
                    newOpCodes.Add(opCode.ToString()); //Then get it as a string so we can add it
                }

                //For each item that was added onto our current list
                bool itemsAdded = false;
                foreach (string difference in newOpCodes.Except(MathOpCodes).ToList())
                {
                    //We add it into our existing types lists
                    MathOperationTypes.Add(difference);
                    MathOpCodeTypes.Add(difference);
                    itemsAdded = true;
                }

                //For each item that is missing from our current list
                foreach (string difference in MathOpCodes.Except(newOpCodes).ToList())
                {
                    //We remove it from the original types list
                    MathOperationTypes.RemoveAt(MathOpCodeTypes.IndexOf(difference));
                    MathOpCodeTypes.Remove(difference);
                }

                //Now just remake the list with our new stuff
                MathOpCodes.Clear();
                MathOpCodes.AddRange(newOpCodes);

                //Tell the user that this has occured, that way they aren't in the dark on our antics
                if (itemsAdded)
                {
                    App.Logger.LogWarning("Difference in hard coded math op codes and actual math op codes detected, issues may occur");
                }
            }

            //Decompile the math assembly
            if (OriginalValues.Assembly.Instructions.Count != 0)
            {
                //Really annoying way to do the references list in proper order
                List<string> inputMathVariables = new List<string>();
                List<string> variableMathVariables = new List<string>();

                List<string> inputMathVariableIDs = new List<string>();
                List<string> variableMathVariableIDs = new List<string>();

                //List of references in the actual original order
                List<string> references = new List<string>();

                //Go through the variables and inputs and add them to our lists
                foreach (dynamic variable in OriginalValues.Assembly.Instructions)
                {
                    //If it is a Variable
                    if (MathOpVariableTypes.Contains(variable.Code.ToString()) && variable != null)
                    {
                        //Load the variable entity
                        VariableEntity variableEntity = LoadCompiledVariable(variable);

                        //Now we just add it to the lists
                        VariableEntities.Add(variableEntity);
                        variableMathVariables.Add(DeclareVariableName(variableEntity));
                        variableMathVariableIDs.Add(DeclareVariableName(variableEntity, true, variable.Result));
                        references.Add(variableEntity.Identifier);
                    }

                    //If it is an Input
                    else if (MathOpInputTypes.Contains(variable.Code.ToString()) && variable != null)
                    {
                        //Load the input entity
                        InputEntity inputEntity = LoadCompiledInput(variable);

                        //Now we just add it to the list
                        InputEntities.Add(inputEntity);
                        inputMathVariables.Add(DeclareInputName(inputEntity));
                        inputMathVariableIDs.Add(DeclareInputName(inputEntity, true, variable.Result));
                        references.Add(inputEntity.Identifier);
                    }

                    //If it is a math instruction
                    else if (MathOpCodeTypes.Contains(variable.Code.ToString()) && variable != null)
                    {
                        //Hacky way to get a complete list of current references

                        MathVariables.AddRange(variableMathVariables);
                        MathVariables.AddRange(inputMathVariables);

                        MathVariableIds.AddRange(variableMathVariableIDs);
                        MathVariableIds.AddRange(inputMathVariableIDs);

                        variableMathVariables.Clear();
                        inputMathVariables.Clear();
                        variableMathVariableIDs.Clear();
                        inputMathVariableIDs.Clear();

                        //Load the math entity
                        MathInstructionEntity mathEntity = LoadCompiledInstruction(variable, OriginalValues.Assembly.FunctionCalls, references);

                        //add it to reference lists
                        MathInstructions.Add(mathEntity);
                    }

                    else if (variable.Code.ToString() == "MathOpCode_Return")
                    {
                        ReturnTypes = new CustomComboData<string, string>(MathOpReturnValues, MathInputTypes);

                        //Combine all the input and variable lists into the references list
                        MathVariables.AddRange(variableMathVariables);
                        MathVariables.AddRange(inputMathVariables);

                        MathVariableIds.AddRange(variableMathVariableIDs);
                        MathVariableIds.AddRange(inputMathVariableIDs);

                        variableMathVariables.Clear();
                        inputMathVariables.Clear();
                        variableMathVariableIDs.Clear();
                        inputMathVariableIDs.Clear();

                        //Get what the return type is based off of Param1
                        switch ((int)variable.Param1)
                        {
                            case 1:
                                {
                                    ReturnTypes.SelectedIndex = 0; //Bool
                                    break;
                                }
                            case 2:
                                {
                                    ReturnTypes.SelectedIndex = 1; //Int
                                    break;
                                }
                            case 4:
                                {
                                    ReturnTypes.SelectedIndex = 2; //Float
                                    break;
                                }
                            case 8:
                                {
                                    ReturnTypes.SelectedIndex = 3; //Vec2
                                    break;
                                }
                            case 16:
                                {
                                    ReturnTypes.SelectedIndex = 4; //Vec3
                                    break;
                                }
                            case 32:
                                {
                                    ReturnTypes.SelectedIndex = 5; //Vec4
                                    break;
                                }
                            case 64:
                                {
                                    ReturnTypes.SelectedIndex = 6; //Transform
                                    break;
                                }
                            default: //Invalid, set to default
                                {
                                    ReturnTypes.SelectedIndex = 1; //Int
                                    break;
                                }
                        }
                    }
                }
            }

            else
            {
                ReturnTypes = new CustomComboData<string, string>(MathOpReturnValues, MathInputTypes) { SelectedIndex = 0 };
            }
        }

        //Compiling
        public override void Save(object e)
        {
            //Assign the original type to a variable as well as information about how we need to edit
            dynamic OriginalValues = Original;

            //List of instructions to add
            List<dynamic> instructions = new List<dynamic>();

            #region --MathInstruct Gathering--
            //This part of the save method goes through and gathers all of the Variables, Inputs, and Instructions and compiles them

            //Get the event args so we know what is being modified
            ItemModifiedEventArgs eventArgs = e as ItemModifiedEventArgs;

            //Clear the references list so we can remake it
            MathVariables.Clear();
            MathVariableIds.Clear();

            //We first need to go through and compile the variables
            foreach (VariableEntity variable in VariableEntities)
            {
                //If this value is null, that means the value is a new entry. We need to set default values
                if (variable.VariableTypes == null)
                {
                    variable.VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 0 };
                    variable.OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 0 };
                    variable.Identifier = DeclareVariableName(variable, true, VariableEntities.Count);
                }
                //If it isn't this we need to check anyway to see if we are changing the type, if we are we need to re-declare our variable
                else if(eventArgs.Item.Name == "VariableTypes")
                {
                    variable.Identifier = DeclareVariableName(variable, true, MathVariableIds.Count); //TODO: This will cause null references, pls fix
                }
                //Now we create the MathEntityInstruction
                dynamic instruction = CompileConst(variable);

                //Add this back to the Reference lists
                MathVariables.Add(DeclareVariableName(variable));
                MathVariableIds.Add(variable.Identifier);

                //The result needs to be its index, so set it to its index then add it to the instructions list
                instruction.Result = MathVariables.IndexOf(DeclareVariableName(variable));
                instructions.Add(instruction);
            }

            //Then, we go through all of the Inputs and add them
            foreach (InputEntity input in InputEntities)
            {
                //If this value is null that means its a new entry, so we need to set its default values
                if (input.InputTypes == null)
                {
                    input.InputTypes = new CustomComboData<string, string>(MathOpReturnValues, MathInputTypes) { SelectedIndex = 0 };
                    input.OriginalType = new CustomComboData<string, string>(MathOpInputTypes, MathOpInputTypes) { SelectedIndex = 0 };
                    input.VariableInput = "In" + InputEntities.IndexOf(input); //Just a fancy trick to make the names like "In1, In2, In3" for convenience
                    input.Identifier = DeclareInputName(input, true, InputEntities.Count);
                }
                //If it isn't this we need to check anyway to see if we are changing the type, if we are we need to re-declare our variable
                else if (eventArgs.Item.Name == "InpuTypes")
                {
                    input.Identifier = DeclareInputName(input, true, MathVariableIds.Count); //TODO: This will cause null references, pls fix
                }

                //Now we create the MathEntityInstruction
                dynamic instruction = CompileInput(input);

                //Add this back to the Reference lists
                MathVariables.Add(DeclareInputName(input));
                MathVariableIds.Add(input.Identifier);

                //The result needs to be its index, so set it to its index then add it to the instructions lists
                instruction.Result = MathVariables.IndexOf(DeclareInputName(input));
                instructions.Add(instruction);
            }

            //We can now, with all of our Inputs and Variables created, go through and compile our math instructions
            #region --Math Instructions--
            //Code for handling Math Instruction saving

            //Clear out the function calls so we can remake them
            OriginalValues.Assembly.FunctionCalls.Clear();

            //No references are being edited, meaning we need to make sure they stay in sync
            if (eventArgs.Item.Name != "FunctionInput" && eventArgs.Item.Name != "OutputTo" && eventArgs.Item.Name != "FunctionInputs" && VariableEntities.Count + InputEntities.Count > 0)
            {
                foreach (MathInstructionEntity math in MathInstructions)
                {
                    //If this isn't a new entry
                    if (math.FunctionTypes != null)
                    {
                        //We need to repair the references before continuing
                        math.OutputTo.SelectedIndex = MathVariableIds.IndexOf(math.ReferencedValue);
                        foreach (FunctionInputEntity functionInput in math.FunctionInputs)
                        {
                            functionInput.FunctionInput.SelectedIndex = MathVariableIds.IndexOf(functionInput.ReferencedValue);
                        }

                        //Convert the math instruction to a MathEntityInstruction(confusing naming ik)
                        dynamic instruction = CompileInstruction(math, OriginalValues.Assembly.FunctionCalls);

                        //Alert the user to compiler errors
                        bool HasError = false;
                        if (instruction.Result == -1)
                        {
                            App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                            HasError = true;
                        }

                        if (instruction.Param1 == -1)
                        {
                            App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                            foreach(FunctionInputEntity functionInput in math.FunctionInputs)
                            {
                                App.Logger.LogError("{0}, {1}", functionInput.FunctionInput.SelectedIndex, functionInput.ReferencedValue);
                            }
                            HasError = true;
                        }

                        if (instruction.Param2 == -1)
                        {
                            App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                            foreach (FunctionInputEntity functionInput in math.FunctionInputs)
                            {
                                App.Logger.LogError("{0}, {1}", functionInput.FunctionInput.SelectedIndex, functionInput.ReferencedValue);
                            }
                            HasError = true;
                        }

                        if (!HasError)
                        {
                            //Add it to the instructions
                            instructions.Add(instruction);
                        }
                    }

                    //If it is, we need to setup default values and such
                    else
                    {
                        //Setup the default values for this new entry
                        math.MathTypes = new CustomComboData<string, string>(MathOperationTypes, MathOperationTypes) { SelectedIndex = 0 };
                        math.OriginalType = new CustomComboData<string, string>(MathOpCodeTypes, MathOpCodeTypes) { SelectedIndex = 0 };
                        math.FunctionTypes = new CustomComboData<string, string>(MathOpFunctions, MathFunctionTypes) { SelectedIndex = 0 };

                        math.OutputTo = new CustomComboData<string, string>(MathVariableIds, MathVariables) { SelectedIndex = 0 };
                        math.ReferencedValue = MathVariableIds[0];
                        math.FunctionInputs.Add(new FunctionInputEntity() { FunctionInput = new CustomComboData<string, string>(MathVariableIds, MathVariables) { SelectedIndex = 0 }, ReferencedValue = MathVariableIds[0] });
                        math.FunctionInputs.Add(new FunctionInputEntity() { FunctionInput = new CustomComboData<string, string>(MathVariableIds, MathVariables) { SelectedIndex = 0 }, ReferencedValue = MathVariableIds[0] });

                        //Create the instruction
                        dynamic instruction = CompileInstruction(math, OriginalValues.Assembly.FunctionCalls);

                        //Add it to the instructions
                        instructions.Add(instruction);
                    }
                }
            }

            //This means we are editing the reference of a function input
            else if (eventArgs.OldValue != null && (eventArgs.Item.Name == "FunctionInput" || eventArgs.Item.Name == "OutputTo") && VariableEntities.Count + InputEntities.Count > 0)
            {
                foreach (MathInstructionEntity math in MathInstructions)
                {
                    //Convert the math instruction to a MathEntityInstruction
                    dynamic instruction = CompileInstruction(math, OriginalValues.Assembly.FunctionCalls);

                    //Update the referenced values
                    math.ReferencedValue = math.OutputTo.SelectedValue;
                    foreach (FunctionInputEntity functionInput in math.FunctionInputs)
                    {
                        functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                    }

                    //Alert the user to compiler errors
                    bool HasError = false;
                    if (instruction.Result == -1)
                    {
                        App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                        HasError = true;
                    }

                    if (instruction.Param1 == -1)
                    {
                        App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                        foreach (FunctionInputEntity functionInput in math.FunctionInputs)
                        {
                            App.Logger.LogError("{0}, {1}", functionInput.FunctionInput.SelectedIndex, functionInput.ReferencedValue);
                        }
                        HasError = true;
                    }

                    if (instruction.Param2 == -1)
                    {
                        App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                        foreach (FunctionInputEntity functionInput in math.FunctionInputs)
                        {
                            App.Logger.LogError("{0}, {1}", functionInput.FunctionInput.SelectedIndex, functionInput.ReferencedValue);
                        }
                        HasError = true;
                    }

                    if (!HasError)
                    {
                        //Add it to the instructions
                        instructions.Add(instruction);
                    }
                }
            }

            //This means we are adding a new Function Input
            else if (eventArgs.Item.Name == "FunctionInputs" && VariableEntities.Count + InputEntities.Count > 0)
            {
                foreach (MathInstructionEntity math in MathInstructions)
                {
                    //Go through all of the function inputs for repairs
                    foreach (FunctionInputEntity functionInput in math.FunctionInputs)
                    {
                        //This means the entry is likely new and needs to be repaired
                        if (functionInput.ReferencedValue == null)
                        {
                            //Create a new drop down menu for the FunctionInput with a selected index of 0(its -1 by default oddly), then set the referenced value to be that too
                            functionInput.FunctionInput = new CustomComboData<string, string>(MathVariableIds, MathVariables) { SelectedIndex = 0 };
                            functionInput.ReferencedValue = MathVariableIds[0];
                        }

                        //Otherwise the entry can just be kept the same
                        else
                        {
                            functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                        }
                    }

                    //Convert the math instructions to MathEntityInstructions
                    dynamic instruction = CompileInstruction(math, OriginalValues.Assembly.FunctionCalls);

                    //Alert the user to compiler errors
                    bool HasError = false;
                    if (instruction.Result == -1)
                    {
                        App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                        HasError = true;
                    }

                    if (instruction.Param1 == -1)
                    {
                        App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                        foreach (FunctionInputEntity functionInput in math.FunctionInputs)
                        {
                            App.Logger.LogError("{0}, {1}", functionInput.FunctionInput.SelectedIndex, functionInput.ReferencedValue);
                        }
                        HasError = true;
                    }

                    if (instruction.Param2 == -1)
                    {
                        App.Logger.LogError("Cannot save Instruction {0} due to a compiler bug. Debug info: {1}, {2}, {3}", MathInstructions.IndexOf(math), math.ReferencedValue, math.OutputTo.SelectedIndex, math.OriginalType.SelectedName);
                        foreach (FunctionInputEntity functionInput in math.FunctionInputs)
                        {
                            App.Logger.LogError("{0}, {1}", functionInput.FunctionInput.SelectedIndex, functionInput.ReferencedValue);
                        }
                        HasError = true;
                    }

                    if (!HasError)
                    {
                        //Add it to the instructions
                        instructions.Add(instruction);
                    }
                }
            }

            //uh oh
            else
            {
                //Catostrophic error!
                App.Logger.LogError("Encountered issue saving Math Instructions, perhaps you are missing Inputs/Entities? If you are, please clear out all math instructions as well to avoid issues. Your data will not be saved until this is resolved.");
            }

            #endregion

            #endregion

            #region --Return--
            //Saving the return Instruction

            //Create a new object to add onto
            dynamic returnInstruction = TypeLibrary.CreateObject("MathEntityInstruction");

            //We need to create a new math op code array
            Array mathOpCodeArray = ((object)TypeLibrary.CreateObject("MathOpCode")).GetType().GetEnumValues();
            List<dynamic> mathOpCodeObjects = new List<dynamic>();
            mathOpCodeObjects.AddRange(mathOpCodeArray.Cast<dynamic>());
            //Now set the MathOpCode to return
            returnInstruction.Code = mathOpCodeObjects[MathOpCodes.IndexOf("MathOpCode_Return")];

            //Get the return types SelectedValue(which stores all of the ints) and parse it for Param1
            returnInstruction.Param1 = int.Parse(ReturnTypes.SelectedValue);

            //We need to set the type of return this is
            if (MathInstructions.Count > 0 && VariableEntities.Count + InputEntities.Count > 0)
            {
                returnInstruction.Param2 = instructions.Last().Result;
            }
            //The user doesn't have anything to return to begin with(unless they want to return an input or variable, which would be pointless, so we should not account for that)
            else
            {
                App.Logger.LogError("No value can be returned by this Math Entity due to there being no Math Instructions, please add Math Instructions. Your data will not be saved until this is resolved.");
            }

            //Add it to the instructions
            instructions.Add(returnInstruction);

            #endregion

            //Edit the EBX
            OriginalValues.Assembly.Instructions.Clear(); //Clear out the original assembly instructions so we can remake it
            foreach (dynamic instruction in instructions)
            {
                OriginalValues.Assembly.Instructions.Add(instruction); //Add all of our instructions
            }
            instructions.Clear(); //As a just in case, we clear Instructions out. That way if its(somehow??) still in memory, we don't need to worry
        }

        #region --Decompiling Methods--
        //These are methods for decompiling the math entities Assembly into our own type override

        /// <summary>
        /// This will decompile a MathEntityInstruction and return the decompiled VariableEntity
        /// </summary>
        /// <param name="instructionObject">A compiled MathEntityInstruction to decompile</param>
        /// <returns>A VariableEntity created out of the provided const</returns>
        internal VariableEntity LoadCompiledVariable(dynamic instructionObject)
        {
            //create a new variable entity with new custom combo's
            VariableEntity variableEntity = new VariableEntity()
            {
                VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes),
                OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes)
            };

            //Set the type value to be that of the code
            variableEntity.VariableTypes.SelectedIndex = MathOpVariableTypes.IndexOf(instructionObject.Code.ToString());
            variableEntity.OriginalType.SelectedIndex = MathOpVariableTypes.IndexOf(instructionObject.Code.ToString());

            //Set the input of the variable to be Param1
            variableEntity.VariableInput = (float)instructionObject.Param1;

            //We also need to set the identifier
            variableEntity.Identifier = DeclareVariableName(variableEntity, true, instructionObject.Result);

            //Return the new variable
            return variableEntity;
        }

        /// <summary>
        /// This will decompile a MathEntityInstruction and return the decompiled InputEntity
        /// </summary>
        /// <param name="instructionObject">A compiled MathEntityInstruction to decompile</param>
        /// <returns>An InputEntity created out of the provided input</returns>
        internal InputEntity LoadCompiledInput(dynamic instructionObject)
        {
            //We create a new Input Entity
            InputEntity inputEntity = new InputEntity
            {
                InputTypes = new CustomComboData<string, string>(MathOpReturnValues, MathInputTypes),
                OriginalType = new CustomComboData<string, string>(MathOpInputTypes, MathOpInputTypes)
            };

            //Set the type to be the code
            inputEntity.InputTypes.SelectedIndex = MathOpInputTypes.IndexOf(instructionObject.Code.ToString());
            inputEntity.OriginalType.SelectedIndex = MathOpInputTypes.IndexOf(instructionObject.Code.ToString());

            //Get the string of the base10 hash
            inputEntity.VariableInput = Utils.GetString(instructionObject.Param1);

            //We also need to set the identifier
            inputEntity.Identifier = DeclareInputName(inputEntity, true, instructionObject.Result);

            //Return the input entity
            return inputEntity;
        }

        /// <summary>
        /// This will decompile a MathEntityInstruction and return the decompiled MathInstructionEntity
        /// </summary>
        /// <param name="instructionObject">A compiled MathEntityInstruction to decompile</param>
        /// <param name="functionCalls">The assembly's function calls</param>
        /// <param name="referencesInOrder">A list of all of the possible references in the compiled order(not decompiled order)</param>
        /// <returns>a dynamic object as a MathEntity</returns>
        internal MathInstructionEntity LoadCompiledInstruction(dynamic instructionObject, dynamic functionCalls, List<string> referencesInOrder)
        {
            //Create the math entity
            MathInstructionEntity mathEntity = new MathInstructionEntity
            {
                MathTypes = new CustomComboData<string, string>(MathOperationTypes, MathOperationTypes),
                OriginalType = new CustomComboData<string, string>(MathOpCodeTypes, MathOpCodeTypes),
                FunctionTypes = new CustomComboData<string, string>(MathOpFunctions, MathFunctionTypes)
            };

            //Set the type
            mathEntity.MathTypes.SelectedIndex = MathOpCodeTypes.IndexOf(instructionObject.Code.ToString());
            mathEntity.OriginalType.SelectedIndex = MathOpCodeTypes.IndexOf(instructionObject.Code.ToString());

            //Set the value that this Output's to
            mathEntity.OutputTo = new CustomComboData<string, string>(MathVariableIds, MathVariables);

            #region --Set the references--
            //Here we set what the references are based off of the type of math we are doing

            if (mathEntity.OriginalType.SelectedValue != "MathOpCode_Func" && !mathEntity.OriginalType.SelectedValue.StartsWith("MathOpCode_Field"))
            {
                //Create 2 new function inputs for the 2 params
                FunctionInputEntity functionInput = new FunctionInputEntity()
                {
                    FunctionInput = new CustomComboData<string, string>(MathVariableIds, MathVariables)
                };

                //Different values can share the same ID, so e.g a bool and an int can both have id 0 without fucking with eachother, but 2 ints need to have id 0 and 1
                //We need to grab different references depending on what type we are as a result
                try
                {
                    switch (mathEntity.OriginalType.SelectedName)
                    {
                        #region --Ints--
                        case "MathOpCode_AddI":
                        case "MathOpCode_SubI":
                        case "MathOpCode_MulI":
                        case "MathOpCode_DivI":
                        case "MathOpCode_ModI":
                        case "MathOpCode_PowerI":
                        case "MathOpCode_GreaterI":
                        case "MathOpCode_GreaterEqI":
                        case "MathOpCode_EqI":
                        case "MathOpCode_LessI":
                        case "MathOpCode_LessEqI":
                        case "MathOpCode_NotEqI":
                        case "MathOpCode_NegI":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("I_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                        #endregion

                        #region --Floats--
                        case "MathOpCode_AddF":
                        case "MathOpCode_SubF":
                        case "MathOpCode_MulF":
                        case "MathOpCode_DivF":
                        case "MathOpCode_ModF":
                        case "MathOpCode_PowerF":
                        case "MathOpCode_GreaterF":
                        case "MathOpCode_GreaterEqF":
                        case "MathOpCode_EqF":
                        case "MathOpCode_LessF":
                        case "MathOpCode_LessEqF":
                        case "MathOpCode_NotEqF":
                        case "MathOpCode_NegF":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                        #endregion

                        #region --Bools--
                        case "MathOpCode_OrB":
                        case "MathOpCode_AndB":
                        case "MathOpCode_NotB":
                        case "MathOpCode_NotEqB":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                        #endregion

                        #region --Vec2--
                        case "MathOpCode_AddV2":
                        case "MathOpCode_SubV2":
                        case "MathOpCode_MulV2F":
                        case "MathOpCode_DivV2F":
                        case "MathOpCode_NegV2":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                        #endregion

                        #region --Vec3--
                        case "MathOpCode_AddV3":
                        case "MathOpCode_SubV3":
                        case "MathOpCode_MulV3F":
                        case "MathOpCode_DivV3F":
                        case "MathOpCode_NegV3":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("3_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                        #endregion

                        #region --Vec4--
                        case "MathOpCode_AddV4":
                        case "MathOpCode_SubV4":
                        case "MathOpCode_MulV4F":
                        case "MathOpCode_DivV4F":
                        case "MathOpCode_NegV4":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("4_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                            #endregion
                    }
                }
                catch
                {
                    App.Logger.LogError("Failed to Decompile input for Instruction {0} due to the reference being invalid.", MathInstructions.Count);
                }

                mathEntity.FunctionInputs.Add(functionInput);

                if (!(mathEntity.OriginalType.SelectedName == "MathOpCode_NotB" || mathEntity.OriginalType.SelectedName.StartsWith("MathOpCode_Neg") || mathEntity.OriginalType.SelectedName.StartsWith("MathOpCode_Pow")))
                {
                    FunctionInputEntity functionInputTwo = new FunctionInputEntity()
                    {
                        FunctionInput = new CustomComboData<string, string>(MathVariableIds, MathVariables)
                    };
                    //Different values can share the same ID, so e.g a bool and an int can both have id 0 without fucking with eachother, but 2 ints need to have id 0 and 1
                    //We need to grab different references depending on what type we are as a result
                    try
                    {
                        switch (mathEntity.OriginalType.SelectedName)
                        {
                            #region --Ints--
                            case "MathOpCode_AddI":
                            case "MathOpCode_SubI":
                            case "MathOpCode_MulI":
                            case "MathOpCode_DivI":
                            case "MathOpCode_ModI":
                            case "MathOpCode_GreaterI":
                            case "MathOpCode_GreaterEqI":
                            case "MathOpCode_EqI":
                            case "MathOpCode_LessI":
                            case "MathOpCode_LessEqI":
                            case "MathOpCode_NotEqI":
                                {
                                    functionInputTwo.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("I_" + instructionObject.Param2.ToString());
                                    functionInputTwo.ReferencedValue = functionInputTwo.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Floats--
                            case "MathOpCode_AddF":
                            case "MathOpCode_SubF":
                            case "MathOpCode_MulF":
                            case "MathOpCode_DivF":
                            case "MathOpCode_ModF":
                            case "MathOpCode_GreaterF":
                            case "MathOpCode_GreaterEqF":
                            case "MathOpCode_EqF":
                            case "MathOpCode_LessF":
                            case "MathOpCode_LessEqF":
                            case "MathOpCode_NotEqF":
                                {
                                    functionInputTwo.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + instructionObject.Param2.ToString());
                                    functionInputTwo.ReferencedValue = functionInputTwo.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Bools--
                            case "MathOpCode_OrB":
                            case "MathOpCode_AndB":
                            case "MathOpCode_NotEqB":
                                {
                                    functionInputTwo.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + instructionObject.Param2.ToString());
                                    functionInputTwo.ReferencedValue = functionInputTwo.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec2--
                            case "MathOpCode_AddV2":
                            case "MathOpCode_SubV2":
                            case "MathOpCode_MulV2F":
                            case "MathOpCode_DivV2F":
                                {
                                    functionInputTwo.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + instructionObject.Param2.ToString()); ;
                                    functionInputTwo.ReferencedValue = functionInputTwo.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec3--
                            case "MathOpCode_AddV3":
                            case "MathOpCode_SubV3":
                            case "MathOpCode_MulV3F":
                            case "MathOpCode_DivV3F":
                                {
                                    functionInputTwo.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("3_" + instructionObject.Param2.ToString());
                                    functionInputTwo.ReferencedValue = functionInputTwo.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec4--
                            case "MathOpCode_AddV4":
                            case "MathOpCode_SubV4":
                            case "MathOpCode_MulV4F":
                            case "MathOpCode_DivV4F":
                                {
                                    functionInputTwo.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("4_" + instructionObject.Param2.ToString()); ;
                                    functionInputTwo.ReferencedValue = functionInputTwo.FunctionInput.SelectedValue;
                                    break;
                                }
                                #endregion
                        }
                    }
                    catch
                    {
                        App.Logger.LogError("Failed to Decompile input for Instruction {0} due to the reference being invalid.", MathInstructions.Count);
                    }

                    //Add them to the params
                    mathEntity.FunctionInputs.Add(functionInputTwo);
                }
            }

            else if (mathEntity.OriginalType.SelectedValue.StartsWith("MathOpCode_Field"))
            {
                if (instructionObject.Code.ToString() == "MathOpCode_FieldT")
                {
                    mathEntity.FunctionTypes.SelectedIndex = instructionObject.Param2 + 5;
                }
                else
                {
                    mathEntity.FunctionTypes.SelectedIndex = instructionObject.Param2 + 1;
                }

                //Create a new function input
                FunctionInputEntity functionInput = new FunctionInputEntity()
                {
                    FunctionInput = new CustomComboData<string, string>(MathVariableIds, MathVariables)
                };

                try
                {
                    switch (mathEntity.OriginalType.SelectedName)
                    {
                        #region --Vec2--
                        case "MathOpCode_FieldV2":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                        #endregion

                        #region --Vec3--
                        case "MathOpCode_FieldV3":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("3_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                        #endregion

                        #region --Vec4--
                        case "MathOpCode_FieldV4":
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("4_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                        #endregion

                        #region --Transforms--
                        case "MathOpCode_FieldT": //Transform
                            {
                                functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("T_" + instructionObject.Param1.ToString());
                                functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                break;
                            }
                            #endregion
                    }
                }
                catch
                {
                    App.Logger.LogError("Failed to Decompile input 0 for Instruction {0} due to the reference being invalid.", MathInstructions.Count);
                }

                //Add them to the params
                mathEntity.FunctionInputs.Add(functionInput);
            }

            //Functions
            else
            {
                mathEntity.FunctionTypes.SelectedIndex = MathOpFunctions.IndexOf(Utils.GetString(instructionObject.Param1));
                if (mathEntity.FunctionTypes.SelectedIndex == -1) //This means its not hard coded
                {
                    string hash = Utils.GetString(instructionObject.Param1);
                    if (hash.StartsWith("0x"))
                    {
                        hash.Remove(0, 2);
                    }
                    MathOpFunctions.Add(hash);
                    MathFunctionTypes.Add(hash);
                    App.Logger.LogWarning("A function was found that is not in hard coded Function Types, issues may occur(are you sure your strings.txt is complete?)");
                }

                //Add the functions
                foreach (dynamic param in functionCalls[instructionObject.Param2].Parameters)
                {
                    FunctionInputEntity functionInput = new FunctionInputEntity()
                    {
                        FunctionInput = new CustomComboData<string, string>(MathVariableIds, MathVariables)
                    };

                    try
                    {
                        switch (Utils.GetString(instructionObject.Param1))
                        {
                            #region --Ints--
                            case "mini":
                            case "maxi":
                            case "sqrti":
                            case "clampi":
                            case "absi":
                            case "signi":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("I_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "intf":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "intb":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "ifi":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 0)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("I_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Floats--
                            case "minf":
                            case "maxf":
                            case "cos":
                            case "sin":
                            case "tan":
                            case "acos":
                            case "asin":
                            case "atan":
                            case "atan2":
                            case "sqrtf":
                            case "lerpf":
                            case "clampf":
                            case "round":
                            case "ceil":
                            case "absf":
                            case "floor":
                            case "signf":
                            case "rotationx":
                            case "rotationy":
                            case "rotationz":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "floati":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("I_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "floatb":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "iff":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 0)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Bools--
                            case "ifb":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec2--
                            case "vec2":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "normalv2":
                            case "dotv2":
                            case "distancev2":
                            case "normv2":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "lerpv2":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 2)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "ifv2":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 0)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec3--
                            case "vec3":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "normalv3":
                            case "cross":
                            case "translation":
                            case "dotv3":
                            case "scale":
                            case "distancev3":
                            case "normv3":
                            case "lookAtTransform":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("3_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "lerpv3":
                            case "slerp":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 2)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("3_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "ifv3":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 0)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec4--
                            case "vec4":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "normalv4":
                            case "dotv4":
                            case "distancev4":
                            case "normv4":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "lerpv4":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 2)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("F_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "ifv4":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 0)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("2_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Transform--
                            case "asLocalSpaceTransform": //Implicit declaration
                            case "asWorldSpaceTransform":
                            case "inverse":
                            case "fullInverse":
                            case "isWorldSpaceTransform":
                            case "rotationAndTranslation":
                                {
                                    functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("T_" + param.ToString());
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }

                            case "ift":
                                {
                                    if (functionCalls[instructionObject.Param2].Parameters.IndexOf(param) != 0)
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("T_" + param.ToString());
                                    }
                                    else
                                    {
                                        functionInput.FunctionInput.SelectedIndex = MathVariableIds.LastIndexOf("B_" + param.ToString());
                                    }
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }
                            #endregion

                            default: //Anything that isn't above is unknown(or I forgor)
                                {
                                    functionInput.FunctionInput.SelectedIndex = (int)param;
                                    functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                                    break;
                                }
                        }
                        
                        mathEntity.FunctionInputs.Add(functionInput);
                    }

                    catch
                    {
                        App.Logger.LogError("Failed to Decompile input {0} for Instruction {1} due to the reference being invalid.", functionCalls.IndexOf(param), MathInstructions.Count);
                    }
                }
            }

            #endregion

            #region --Set the output reference--
            //We need to first set the output reference

            //Different types can have the same ID, so e.g an int can have id 0 while a bool has id 0 too, so we need to get the reference based on this
            switch (mathEntity.OriginalType.SelectedName)
            {
                #region --Ints--
                case "MathOpCode_AddI":
                case "MathOpCode_SubI":
                case "MathOpCode_MulI":
                case "MathOpCode_DivI":
                case "MathOpCode_ModI":
                case "MathOpCode_PowerI":
                case "MathOpCode_NegI":
                    {
                        int idx = MathVariableIds.LastIndexOf("I_" + instructionObject.Result.ToString());
                        if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                        {
                            VariableEntity variable = new VariableEntity()
                            {
                                OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 1 },
                                VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 1 }
                            };
                            variable.VariableTypes.SelectedIndex = 1; //Int
                            variable.OriginalType.SelectedIndex = 1; //Int
                            VariableEntities.Add(variable);
                            MathVariables.Add(DeclareVariableName(variable));
                            MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                            referencesInOrder.Add(DeclareVariableName(variable, true, instructionObject.Result));
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                        }
                        else
                        {
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                        }
                        mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                        break;
                    }
                #endregion

                #region --Floats--
                case "MathOpCode_AddF":
                case "MathOpCode_SubF":
                case "MathOpCode_MulF":
                case "MathOpCode_DivF":
                case "MathOpCode_ModF":
                case "MathOpCode_PowerF":
                case "MathOpCode_NegF":
                case "MathOpCode_FieldV2":
                case "MathOpCode_FieldV3":
                case "MathOpCode_FieldV4":
                    {
                        int idx = MathVariableIds.LastIndexOf("F_" + instructionObject.Result.ToString());
                        if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                        {
                            VariableEntity variable = new VariableEntity()
                            {
                                OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 2 },
                                VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 2 }
                            };
                            VariableEntities.Add(variable);
                            MathVariables.Add(DeclareVariableName(variable));
                            MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                            referencesInOrder.Add(DeclareVariableName(variable, true, instructionObject.Result));
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                        }
                        else
                        {
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                        }
                        mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                        break;
                    }
                #endregion

                #region --Bools--
                case "MathOpCode_OrB":
                case "MathOpCode_AndB":
                case "MathOpCode_NotB":
                case "MathOpCode_GreaterI":
                case "MathOpCode_GreaterEqI":
                case "MathOpCode_EqI":
                case "MathOpCode_LessI":
                case "MathOpCode_LessEqI":
                case "MathOpCode_NotEqI":
                case "MathOpCode_NotEqB":
                case "MathOpCode_GreaterF":
                case "MathOpCode_GreaterEqF":
                case "MathOpCode_EqF":
                case "MathOpCode_LessF":
                case "MathOpCode_LessEqF":
                case "MathOpCode_NotEqF":
                    {
                        int idx = MathVariableIds.LastIndexOf("B_" + instructionObject.Result.ToString());
                        if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                        {
                            VariableEntity variable = new VariableEntity()
                            {
                                OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 0 },
                                VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 0 }
                            };
                            VariableEntities.Add(variable);
                            MathVariables.Add(DeclareVariableName(variable));
                            MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                            referencesInOrder.Add(DeclareVariableName(variable, true, instructionObject.Result));
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                        }
                        else
                        {
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                        }
                        mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                        break;
                    }
                #endregion

                #region --Vec2--
                case "MathOpCode_AddV2":
                case "MathOpCode_SubV2":
                case "MathOpCode_MulV2F":
                case "MathOpCode_DivV2F":
                case "MathOpCode_NegV2":
                    {
                        int idx = MathVariableIds.LastIndexOf("2_" + instructionObject.Result.ToString());
                        if (idx == -1) //If its -1 that means the input doesn't exist, so we are declaring a reference
                        {
                            MathVariables.Add(DeclareVariableName(mathEntity));
                            MathVariableIds.Add(DeclareVariableName(mathEntity, true, instructionObject.Result));
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(mathEntity, true, instructionObject.Result));
                        }
                        else
                        {
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                        }
                        mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                        mathEntity.Identifier = DeclareVariableName(mathEntity, true, instructionObject.Result);
                        break;
                    }
                #endregion

                #region --Vec3--
                case "MathOpCode_AddV3":
                case "MathOpCode_SubV3":
                case "MathOpCode_MulV3F":
                case "MathOpCode_DivV3F":
                case "MathOpCode_NegV3":
                    {
                        int idx = MathVariableIds.LastIndexOf("3_" + instructionObject.Result.ToString());
                        if (idx == -1) //If its -1 that means the input doesn't exist, so we are declaring a reference
                        {
                            MathVariables.Add(DeclareVariableName(mathEntity));
                            MathVariableIds.Add(DeclareVariableName(mathEntity, true, instructionObject.Result));
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(mathEntity, true, instructionObject.Result));
                        }
                        else
                        {
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                        }
                        mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                        mathEntity.Identifier = DeclareVariableName(mathEntity, true, instructionObject.Result);
                        break;
                    }
                #endregion

                #region --Vec4--
                case "MathOpCode_AddV4":
                case "MathOpCode_SubV4":
                case "MathOpCode_MulV4F":
                case "MathOpCode_DivV4F":
                case "MathOpCode_NegV4":
                    {
                        int idx = MathVariableIds.LastIndexOf("4_" + instructionObject.Result.ToString());
                        if (idx == -1) //If its -1 that means the input doesn't exist, so we are declaring a reference
                        {
                            MathVariables.Add(DeclareVariableName(mathEntity));
                            MathVariableIds.Add(DeclareVariableName(mathEntity, true, instructionObject.Result));
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(mathEntity, true, instructionObject.Result));
                        }
                        else
                        {
                            mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                        }
                        mathEntity.Identifier = DeclareVariableName(mathEntity, true, instructionObject.Result);
                        mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                        break;
                    }
                #endregion

                #region --Transform
                case "MathOpCode_FieldT":
                    {
                        if (!mathEntity.FunctionTypes.SelectedName.StartsWith("Split transform"))
                        {
                            int idx = MathVariableIds.LastIndexOf("F_" + instructionObject.Result.ToString());
                            if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                            {
                                VariableEntity variable = new VariableEntity()
                                {
                                    OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 2 },
                                    VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 2 }
                                };
                                VariableEntities.Add(variable);
                                MathVariables.Add(DeclareVariableName(variable));
                                MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                referencesInOrder.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                            }
                            else
                            {
                                mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                            }
                            mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                        }
                        else
                        {
                            int idx = MathVariableIds.LastIndexOf("3_" + instructionObject.Result.ToString());
                            if (idx == -1) //If its -1 that means the input doesn't exist, so we are declaring a reference
                            {
                                MathVariables.Add(DeclareVariableName(mathEntity));
                                MathVariableIds.Add(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(mathEntity, true, instructionObject.Result));
                            }
                            else
                            {
                                mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                            }
                            mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                            mathEntity.Identifier = DeclareVariableName(mathEntity, true, instructionObject.Result);
                        }
                        break;
                    }
                #endregion

                #region --Functions--
                //Functions are unique, as they have their own outputs we need to account for
                case "MathOpCode_Func":
                    {
                        switch (mathEntity.FunctionTypes.SelectedValue.Split(':').First())
                        {
                            #region --Ints--
                            case "mini":
                            case "maxi":
                            case "sqrti":
                            case "clampi":
                            case "intf":
                            case "intb":
                            case "absi":
                            case "signi":
                            case "ifi":
                                {
                                    int idx = MathVariableIds.LastIndexOf("I_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                                    {
                                        VariableEntity variable = new VariableEntity()
                                        {
                                            OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 1},
                                            VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 1}
                                        };
                                        VariableEntities.Add(variable);
                                        MathVariables.Add(DeclareVariableName(variable));
                                        MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Floats--
                            case "minf":
                            case "maxf":
                            case "cos":
                            case "sin":
                            case "tan":
                            case "acos":
                            case "asin":
                            case "atan":
                            case "atan2":
                            case "sqrtf":
                            case "lerpf":
                            case "clampf":
                            case "floati":
                            case "floatb":
                            case "round":
                            case "ceil":
                            case "absf":
                            case "floor":
                            case "signf":
                            case "iff":
                                {
                                    int idx = MathVariableIds.LastIndexOf("F_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                                    {
                                        VariableEntity variable = new VariableEntity()
                                        {
                                            OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 2 },
                                            VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 2 }
                                        };
                                        VariableEntities.Add(variable);
                                        MathVariables.Add(DeclareVariableName(variable));
                                        MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Bools--
                            case "ifb":
                                {
                                    int idx = MathVariableIds.LastIndexOf("B_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                                    {
                                        VariableEntity variable = new VariableEntity()
                                        {
                                            OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 2 },
                                            VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 2 }
                                        };
                                        VariableEntities.Add(variable);
                                        MathVariables.Add(DeclareVariableName(variable));
                                        MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec2--
                            case "vec2": //Implicit Declaration
                            case "normv2":  //Implicit
                            case "lerpv2": //Implicit
                            case "ifv2":
                            case "normalv2":
                                {
                                    int idx = MathVariableIds.LastIndexOf("2_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the input doesn't exist, so we are declaring a reference
                                    {
                                        MathVariables.Add(DeclareVariableName(mathEntity));
                                        MathVariableIds.Add(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.Identifier = DeclareVariableName(mathEntity, true, instructionObject.Result);
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }

                            case "distancev2": //Implicit to Explicit
                            case "dotv2":
                                {
                                    int idx = MathVariableIds.LastIndexOf("F_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                                    {
                                        VariableEntity variable = new VariableEntity()
                                        {
                                            OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 2 },
                                            VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 2 }
                                        };
                                        VariableEntities.Add(variable);
                                        MathVariables.Add(DeclareVariableName(variable));
                                        MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec3--
                            case "vec3": //Implicit Declaration
                            case "normv3":  //Implicit
                            case "lerpv3": //Implicit
                            case "rotate":
                            case "invRotate":
                            case "invTransform":
                            case "ifv3":
                            case "transform":
                            case "cross":
                            case "slerp":
                            case "normalv3":
                            case "translation":
                                {
                                    int idx = MathVariableIds.LastIndexOf("3_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the input doesn't exist, so we are declaring a reference
                                    {
                                        MathVariables.Add(DeclareVariableName(mathEntity));
                                        MathVariableIds.Add(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.Identifier = DeclareVariableName(mathEntity, true, instructionObject.Result);
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }

                            case "distancev3": //Implicit to Explicit
                            case "dotv3":
                                {
                                    int idx = MathVariableIds.LastIndexOf("F_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                                    {
                                        VariableEntity variable = new VariableEntity()
                                        {
                                            OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 2 },
                                            VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 2 }
                                        };
                                        VariableEntities.Add(variable);
                                        MathVariables.Add(DeclareVariableName(variable));
                                        MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Vec4--
                            case "vec4": //Implicit Declaration
                            case "normv4":  //Implicit
                            case "lerpv4": //Implicit
                            case "ifv4":
                            case "normalv4":
                                {
                                    int idx = MathVariableIds.LastIndexOf("4_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the input doesn't exist, so we are declaring a reference
                                    {
                                        MathVariables.Add(DeclareVariableName(mathEntity));
                                        MathVariableIds.Add(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }

                            case "distancev4": //Implicit to Explicit
                            case "dotv4":
                                {
                                    int idx = MathVariableIds.LastIndexOf("F_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                                    {
                                        VariableEntity variable = new VariableEntity()
                                        {
                                            OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 2 },
                                            VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 2 }
                                        };
                                        VariableEntities.Add(variable);
                                        MathVariables.Add(DeclareVariableName(variable));
                                        MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }
                            #endregion

                            #region --Transform--
                            case "asLocalSpaceTransform": //Implicit declaration
                            case "asWorldSpaceTransform":
                            case "lookAtTransform":
                            case "inverse":
                            case "fullInverse":
                            case "scale":
                            case "rotationAndTranslation":
                            case "rotationx":
                            case "rotationy":
                            case "rotationz":
                                {
                                    int idx = MathVariableIds.LastIndexOf("T_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the input doesn't exist, so we are declaring a reference
                                    {
                                        MathVariables.Add(DeclareVariableName(mathEntity));
                                        MathVariableIds.Add(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(mathEntity, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.Identifier = DeclareVariableName(mathEntity, true, instructionObject.Result);
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }

                            case "isWorldSpaceTransform":
                                {
                                    int idx = MathVariableIds.LastIndexOf("B_" + instructionObject.Result.ToString());
                                    if (idx == -1) //If its -1 that means the variable doesn't exist, lets assume its referencing itself
                                    {
                                        VariableEntity variable = new VariableEntity()
                                        {
                                            OriginalType = new CustomComboData<string, string>(MathOpVariableTypes, MathOpVariableTypes) { SelectedIndex = 0 },
                                            VariableTypes = new CustomComboData<string, string>(MathVariableTypes, MathVariableTypes) { SelectedIndex = 0 }
                                        };
                                        VariableEntities.Add(variable);
                                        MathVariables.Add(DeclareVariableName(variable));
                                        MathVariableIds.Add(DeclareVariableName(variable, true, instructionObject.Result));
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(DeclareVariableName(variable, true, instructionObject.Result));
                                    }
                                    else
                                    {
                                        mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(MathVariableIds[idx]);
                                    }
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }
                            #endregion

                            default: //Anything that isn't above is unknown(or I forgor)
                                {
                                    mathEntity.OutputTo.SelectedIndex = instructionObject.Result;
                                    mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                                    break;
                                }
                        }
                        break;
                    }

                    #endregion
            }

            #endregion

            //Return the value
            return mathEntity;
        }
        #endregion

        #region --Compiling Methods--
        //These methods re-compile our human readable items back into the assembly's Instructions and FunctionCalls

        /// <summary>
        /// Compile a VariableEntity back into a MathEntityInstruction
        /// </summary>
        /// <param name="variable">The VariableEntity to compile</param>
        /// <returns>the provided VariableEntity as a MathEntityInstruction</returns>
        internal dynamic CompileConst(VariableEntity variable)
        {
            //Create a new object to add onto
            dynamic Instruction = TypeLibrary.CreateObject("MathEntityInstruction");

            //Set the Code to be the input type
            Array mathOpCodeArray = ((object)TypeLibrary.CreateObject("MathOpCode")).GetType().GetEnumValues();
            List<dynamic> mathOpCodeObjects = new List<dynamic>();
            mathOpCodeObjects.AddRange(mathOpCodeArray.Cast<dynamic>());

            //Set the code
            variable.OriginalType.SelectedIndex = variable.VariableTypes.SelectedIndex; //Make sure to update the selected Index
            Instruction.Code = mathOpCodeObjects[MathOpCodes.IndexOf(variable.OriginalType.SelectedValue)];

            //Set the param
            Instruction.Param1 = Base10Of(variable.VariableInput);

            //Return
            return Instruction;
        }

        /// <summary>
        /// Compile an Input back into a MathEntityInstruction
        /// </summary>
        /// <param name="input">the InputEntity to compile</param>
        /// <returns>the provided InputEntity as a MathEntityInstruction</returns>
        internal dynamic CompileInput(InputEntity input)
        {
            //Create a new object to add onto
            dynamic Instruction = TypeLibrary.CreateObject("MathEntityInstruction");

            //Set the Code to be the input type
            Array mathOpCodeArray = ((object)TypeLibrary.CreateObject("MathOpCode")).GetType().GetEnumValues();
            List<dynamic> mathOpCodeObjects = new List<dynamic>();
            mathOpCodeObjects.AddRange(mathOpCodeArray.Cast<dynamic>());

            //Set the code
            input.OriginalType.SelectedIndex = input.InputTypes.SelectedIndex; //Make sure to update the selected Index
            Instruction.Code = mathOpCodeObjects[MathOpCodes.IndexOf(input.OriginalType.SelectedValue)];

            //Set the param
            Instruction.Param1 = Base10Of(input.VariableInput);

            //Return
            return Instruction;
        }

        /// <summary>
        /// This will compile the entire Math Instruction with all of its inputs into a proper MathEntityInstruction as well as function calls if needed
        /// </summary>
        /// <param name="mathEntity"></param>
        /// <param name="functionCalls">The assembly's FunctionCalls</param>
        /// <returns>the provided MathEntity as a MathEntityInstruction</returns>
        internal dynamic CompileInstruction(MathInstructionEntity mathEntity, dynamic functionCalls)
        {
            //Create a new object to add onto
            dynamic Instruction = TypeLibrary.CreateObject("MathEntityInstruction");

            //We need to create a new math op code array
            Array mathOpCodeArray = ((object)TypeLibrary.CreateObject("MathOpCode")).GetType().GetEnumValues();
            List<dynamic> mathOpCodeObjects = new List<dynamic>();
            mathOpCodeObjects.AddRange(mathOpCodeArray.Cast<dynamic>());
            mathEntity.OriginalType.SelectedIndex = mathEntity.MathTypes.SelectedIndex; //Update the Original Type index

            //Then, we need to assign the original values
            Instruction.Code = mathOpCodeObjects[MathOpCodes.IndexOf(mathEntity.OriginalType.SelectedValue)]; //Assign the code
            Instruction.Result = mathEntity.OutputTo.SelectedIndex; //Set the result to be the OutputTo index
            if (Instruction.Result == -1) //If the index is invalid, then repair the reference and instruction as well as warn the user
            {
                Instruction.Result = 0;
                mathEntity.OutputTo.SelectedIndex = 0;
                mathEntity.ReferencedValue = mathEntity.OutputTo.SelectedValue;
                App.Logger.LogWarning("Reset the output of {0} due to reference being null", DeclareInstructionName(mathEntity));
            }

            #region --Save parameters--
            //We need to check to see if this is a function or field, as we have to save those differently
            if (mathEntity.OriginalType.SelectedValue != "MathOpCode_Func" && !mathEntity.OriginalType.SelectedValue.StartsWith("MathOpCode_Field"))
            {
                if (mathEntity.FunctionInputs.Count == 2 && !(mathEntity.OriginalType.SelectedName == "MathOpCode_NotB" || mathEntity.OriginalType.SelectedName.StartsWith("MathOpCode_Neg") || mathEntity.OriginalType.SelectedName.StartsWith("MathOpCode_Pow")))
                {
                    Instruction.Param1 = mathEntity.FunctionInputs[0].FunctionInput.SelectedIndex;
                    Instruction.Param2 = mathEntity.FunctionInputs[1].FunctionInput.SelectedIndex;
                    if (Instruction.Param1 == -1)
                    {
                        Instruction.Param1 = 0;
                        mathEntity.FunctionInputs[0].FunctionInput.SelectedIndex = 0;
                        mathEntity.FunctionInputs[0].ReferencedValue = mathEntity.FunctionInputs[0].FunctionInput.SelectedValue;
                        App.Logger.LogWarning("Reset the first input of {0} due to reference being null", DeclareInstructionName(mathEntity));
                    }
                    if (Instruction.Param2 == -1)
                    {
                        Instruction.Param2 = 0;
                        mathEntity.FunctionInputs[1].FunctionInput.SelectedIndex = 0;
                        mathEntity.FunctionInputs[1].ReferencedValue = mathEntity.FunctionInputs[1].FunctionInput.SelectedValue;
                        App.Logger.LogWarning("Reset the second input of {0} due to reference being null", DeclareInstructionName(mathEntity));
                    }
                }

                else if ((mathEntity.OriginalType.SelectedName == "MathOpCode_NotB" || mathEntity.OriginalType.SelectedName.StartsWith("MathOpCode_Neg")) && mathEntity.FunctionInputs.Count == 1)
                {
                    Instruction.Param1 = mathEntity.FunctionInputs[0].FunctionInput.SelectedIndex;
                    if (Instruction.Param1 == -1)
                    {
                        Instruction.Param1 = 0;
                        mathEntity.FunctionInputs[0].FunctionInput.SelectedIndex = 0;
                        mathEntity.FunctionInputs[0].ReferencedValue = mathEntity.FunctionInputs[0].FunctionInput.SelectedValue;
                        App.Logger.LogWarning("Reset the first input of {0} due to reference being null", DeclareInstructionName(mathEntity));
                    }
                }
                
                else
                {
                    switch (mathEntity.OriginalType.SelectedName)
                    {
                        case "MathOpCode_NotB":
                            {
                                App.Logger.LogError("Math type {0} requires exactly 1 input, please ensure the amount of inputs set matches this. Your data will not be saved until this is resolved.", mathEntity.MathTypes.SelectedValue);
                                break;
                            }
                        case "MathOpCode_NegI":
                            {
                                App.Logger.LogError("Math type {0} requires exactly 1 input, please ensure the amount of inputs set matches this. Your data will not be saved until this is resolved.", mathEntity.MathTypes.SelectedValue);
                                break;
                            }
                        case "MathOpCode_NegF":
                            {
                                App.Logger.LogError("Math type {0} requires exactly 1 input, please ensure the amount of inputs set matches this. Your data will not be saved until this is resolved.", mathEntity.MathTypes.SelectedValue);
                                break;
                            }
                        case "MathOpCode_NegV2":
                            {
                                App.Logger.LogError("Math type {0} requires exactly 1 input, please ensure the amount of inputs set matches this. Your data will not be saved until this is resolved.", mathEntity.MathTypes.SelectedValue);
                                break;
                            }
                        case "MathOpCode_NegV3":
                            {
                                App.Logger.LogError("Math type {0} requires exactly 1 input, please ensure the amount of inputs set matches this. Your data will not be saved until this is resolved.", mathEntity.MathTypes.SelectedValue);
                                break;
                            }
                        case "MathOpCode_NegV4":
                            {
                                App.Logger.LogError("Math type {0} requires exactly 1 input, please ensure the amount of inputs set matches this. Your data will not be saved until this is resolved.", mathEntity.MathTypes.SelectedValue);
                                break;
                            }
                        default:
                            {
                                App.Logger.LogError("Math type {0} requires exactly 2 inputs, please ensure the amount of inputs set matches this. Your data will not be saved until this is resolved.", mathEntity.MathTypes.SelectedValue);
                                break;
                            }
                    }
                }
            }

            else if (mathEntity.OriginalType.SelectedValue.StartsWith("MathOpCode_Field"))
            {
                if (mathEntity.FunctionInputs.Count == 1)
                {
                    Instruction.Param1 = mathEntity.FunctionInputs[0].FunctionInput.SelectedIndex;
                    if (Instruction.Param1 == -1)
                    {
                        Instruction.Param1 = 0;
                        mathEntity.FunctionInputs[0].FunctionInput.SelectedIndex = 0;
                        mathEntity.FunctionInputs[0].ReferencedValue = mathEntity.FunctionInputs[0].FunctionInput.SelectedValue;
                        App.Logger.LogWarning("Reset the first input of {0} due to reference being null", DeclareInstructionName(mathEntity));
                    }

                    switch (mathEntity.FunctionTypes.SelectedValue)
                    {
                        case "Split X: Split vector":
                            {
                                Instruction.Param2 = 0;
                                break;
                            }
                        case "Split Y: Split vector":
                            {
                                Instruction.Param2 = 1;
                                break;
                            }
                        case "Split Z: Split vector":
                            {
                                Instruction.Param2 = 2;
                                break;
                            }
                        case "Split W: Split vector":
                            {
                                Instruction.Param2 = 3;
                                break;
                            }
                        case "Split left: Split transform":
                            {
                                Instruction.Param2 = 0;
                                break;
                            }
                        case "Split up: Split transform":
                            {
                                Instruction.Param2 = 1;
                                break;
                            }
                        case "Split forward: Split transform":
                            {
                                Instruction.Param2 = 2;
                                break;
                            }
                        case "Trans: Split transform":
                            {
                                Instruction.Param2 = 3;
                                break;
                            }
                    }
                }
                else
                {
                    App.Logger.LogError("Math type {0} requires exactly 1 input, please ensure the amount of inputs set matches this. Your data will not be saved until this is resolved.", mathEntity.MathTypes.SelectedValue);
                }
            }

            //If it isn't any of these it has to be a function
            else
            {
                Instruction.Param1 = Base10Of(mathEntity.FunctionTypes.SelectedValue.Split(':').First());
                //Since this is a function, we need to get its parameters setup in the FunctionCalls
                dynamic functionCall = TypeLibrary.CreateObject("MathEntityFunctionCall");

                //For each parameter the user has defined in the math instruction, create a new FunctionCall param
                foreach (FunctionInputEntity functionInput in mathEntity.FunctionInputs)
                {
                    if (functionInput.FunctionInput.SelectedIndex != -1)
                    {
                        functionCall.Parameters.Add((uint)functionInput.FunctionInput.SelectedIndex);
                    }
                    else
                    {
                        functionCall.Parameters.Add(0u);
                        functionInput.FunctionInput.SelectedIndex = 0;
                        functionInput.ReferencedValue = functionInput.FunctionInput.SelectedValue;
                        App.Logger.LogWarning("Reset input {0} in {1} due to the reference being null", mathEntity.FunctionInputs.IndexOf(functionInput), DeclareInstructionName(mathEntity));
                    }
                }

                //Now we just add it to the FunctionCalls
                functionCalls.Add(functionCall);

                Instruction.Param2 = functionCalls.IndexOf(functionCall);

                Instruction.Result = MathVariableIds.IndexOf(mathEntity.ReferencedValue);
                //If it is -1 we are declaring a new vector most likely
                if (Instruction.Result == -1)
                {
                    MathVariables.Add(DeclareVariableName(mathEntity));
                    MathVariableIds.Add(DeclareVariableName(mathEntity, true, MathVariableIds.Count));
                    Instruction.Result = MathVariableIds.IndexOf(mathEntity.ReferencedValue);
                    mathEntity.OutputTo.SelectedIndex = MathVariableIds.IndexOf(mathEntity.ReferencedValue); //Repair the reference
                }

            }

            #endregion

            return Instruction;
        }

        #endregion

        #region --Helpers--
        //These are methods for helping us convert our data

        /// <summary>
        /// Gets the Base10 of a value(if it needs to)
        /// </summary>
        /// <param name="ValueToConvert">String or Float to convert</param>
        /// <returns>The base10 of the inputted value</returns>
        internal int Base10Of(object ValueToConvert)
        {
            if (ValueToConvert.GetType() == typeof(float))
            {
                float toConvert = (float)ValueToConvert;
                if (toConvert % 1 != 0)
                {
                    return Utils.HashString(toConvert.ToString());
                }
                else
                {
                    return (int)toConvert;
                }
            }
            else if(ValueToConvert.GetType() == typeof(string))
            {
                string toConvert = (string)ValueToConvert;
                if (toConvert.StartsWith("0x"))
                {
                    return Utils.HashString(toConvert.Remove(0, 2)); //The string needs to be in Base10, and we cannot include 0x for hashes
                }
                else
                {
                    return Utils.HashString(toConvert); //The string needs to be in base10
                }
            }
            else
            {
                App.Logger.LogError("Value is inproper, cannot convert to base10");
                return Utils.HashString("");
            }
        }

        #endregion

        #region --MathOp Declaration--
        //These are just for consistency, I got tired of having to remember the actual format of the different math operators

        /// <summary>
        /// Declare a new Variable name for adding to the list of references
        /// </summary>
        /// <param name="objectToDeclare">The variable to declare. Can either be a Variable Entity or a MathEntity with a vector delcaring function</param>
        /// <param name="isID">Whether or not we are declaring an ID</param>
        /// <param name="ID">The ID to set this variable to</param>
        /// <returns>The variable reference name or ID</returns>
        internal string DeclareVariableName(object objectToDeclare, bool isID = false, int ID = 0)
        {
            if (objectToDeclare.GetType() == typeof(VariableEntity))
            {
                VariableEntity variable = (VariableEntity)objectToDeclare;
                if (isID)
                {
                    return variable.OriginalType.SelectedValue.Last() + "_" + ID;
                }
                else
                {
                    switch (variable.VariableTypes.SelectedValue)
                    {
                        case "Boolean":
                            {
                                return "Var " + variable.VariableTypes.SelectedValue + " " + Convert.ToBoolean(variable.VariableInput).ToString();
                            }
                        default:
                            {
                                return "Var " + variable.VariableTypes.SelectedValue + " " + variable.VariableInput.ToString();
                            }
                    }
                }
            }

            else
            {
                MathInstructionEntity math = (MathInstructionEntity)objectToDeclare;
                if (isID)
                {
                    return math.FunctionTypes.SelectedName.Last() + "_" + ID;
                }

                else
                {
                    switch(math.FunctionTypes.SelectedValue.Split(':').First())
                    {
                        case "vec2":
                            {
                                return "Var vec2 " + math.FunctionInputs[0].FunctionInput.SelectedName + ',' + math.FunctionInputs[1].FunctionInput.SelectedName;
                            }
                        case "vec3":
                            {
                                return "Var vec3 " + math.FunctionInputs[0].FunctionInput.SelectedName + ',' + math.FunctionInputs[1].FunctionInput.SelectedName + ',' + math.FunctionInputs[2].FunctionInput.SelectedName;
                            }
                        case "vec4":
                            {
                                return "Var vec4 " + math.FunctionInputs[0].FunctionInput.SelectedName + ',' + math.FunctionInputs[1].FunctionInput.SelectedName + ',' + math.FunctionInputs[2].FunctionInput.SelectedName + ',' + math.FunctionInputs[3].FunctionInput.SelectedName;
                            }

                        case "asLocalSpaceTransform":
                        case "asWorldSpaceTransform":
                        case "lookAtTransform":
                        case "inverse":
                        case "fullInverse":
                        case "scale":
                        case "rotationx":
                        case "rotationy":
                        case "rotationz":
                            {
                                return "Var trans " + MathVariables.Count.ToString();
                            }

                        default:
                            {
                                return "Var " + MathVariables.Count.ToString();
                            }
                    }
                }
            }
        }

        /// <summary>
        /// Declare a new Input name for adding to the list of references
        /// </summary>
        /// <param name="input">The input to declare</param>
        /// <param name="isID">Whether or not we are declaring an ID</param>
        /// <param name="ID">The ID to set this input to</param>
        /// <returns>The input reference name or ID</returns>
        internal string DeclareInputName(InputEntity input, bool isID = false, int ID = 0)
        {
            if (isID)
            {
                return input.OriginalType.SelectedName.Last() + "_" + ID;
            }
            else
            {
                return "Input " + input.VariableInput + "(" + input.InputTypes.SelectedName + ")";
            }
        }

        /// <summary>
        /// Declare a new Instruction name for adding to the list of references
        /// </summary>
        /// <param name="instruction">The instruction to declare</param>
        /// <param name="isID">Whether or not we are declaring an ID</param>
        /// <returns>The instruction reference name or ID</returns>
        internal string DeclareInstructionName(MathInstructionEntity instruction, bool isID = false)
        {
            if (isID)
            {
                return "Instruct" + MathInstructions.IndexOf(instruction);
            }
            else
            {
                if (instruction.OriginalType.SelectedValue != "MathOpCode_Func")
                {
                    switch (instruction.MathTypes.SelectedName.Remove(instruction.MathTypes.SelectedName.IndexOf(':')))
                    {
                        case "Add":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + "+" + " " + instruction.FunctionInputs.LastOrDefault().FunctionInput.SelectedName;
                            }
                        case "Subtract":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + "-" + " " + instruction.FunctionInputs.LastOrDefault().FunctionInput.SelectedName;
                            }
                        case "Multiply":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + "*" + " " + instruction.FunctionInputs.LastOrDefault().FunctionInput.SelectedName;
                            }
                        case "Divide":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + "/" + " " + instruction.FunctionInputs.LastOrDefault().FunctionInput.SelectedName;
                            }
                        case "Modulo":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + "%" + " " + instruction.FunctionInputs.LastOrDefault().FunctionInput.SelectedName;
                            }
                        case "Power":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + "^" + " " + instruction.FunctionInputs.LastOrDefault().FunctionInput.SelectedName;
                            }
                        case "Negative":
                            {
                                return "Instruction " + "-" + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName;
                            }
                        case "Split Vec2":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + instruction.FunctionTypes.SelectedName.Remove(instruction.MathTypes.SelectedName.IndexOf(':'));
                            }
                        case "Split Vec3":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + instruction.FunctionTypes.SelectedName.Remove(instruction.MathTypes.SelectedName.IndexOf(':'));
                            }
                        case "Split Vec4":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + instruction.FunctionTypes.SelectedName.Remove(instruction.MathTypes.SelectedName.IndexOf(':'));
                            }
                        case "Split Transform":
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + instruction.FunctionTypes.SelectedName.Remove(instruction.MathTypes.SelectedName.IndexOf(':'));
                            }
                        case "NOT":
                            {
                                return "Instruction NOT " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName;
                            }
                        default:
                            {
                                return "Instruction " + instruction.FunctionInputs.FirstOrDefault().FunctionInput.SelectedName + " " + instruction.MathTypes.SelectedName.Remove(instruction.MathTypes.SelectedName.IndexOf(':')) + " " + instruction.FunctionInputs.LastOrDefault().FunctionInput.SelectedName;
                            }
                    }
                }

                else
                {
                    return "Function " + instruction.FunctionTypes.SelectedName;
                }
            }
        }
        #endregion
    }

}
