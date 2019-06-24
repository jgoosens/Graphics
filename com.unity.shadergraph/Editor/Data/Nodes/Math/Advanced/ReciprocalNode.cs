using System.Reflection;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEditor.ShaderGraph.Hlsl;
using static UnityEditor.ShaderGraph.Hlsl.Intrinsics;

namespace UnityEditor.ShaderGraph
{
    enum ReciprocalMethod
    {
        Default,
        Fast
    };

    [Title("Math", "Advanced", "Reciprocal")]
    class ReciprocalNode : CodeFunctionNode
    {
        public ReciprocalNode()
        {
            name = "Reciprocal";
        }


        [SerializeField]
        private ReciprocalMethod m_ReciprocalMethod = ReciprocalMethod.Default;

        [EnumControl("Method")]
        public ReciprocalMethod reciprocalMethod
        {
            get { return m_ReciprocalMethod; }
            set
            {
                if (m_ReciprocalMethod == value)
                    return;

                m_ReciprocalMethod = value;
                Dirty(ModificationScope.Graph);
            }
        }

        protected override MethodInfo GetFunctionToConvert()
        {
            switch (m_ReciprocalMethod)
            {
                case ReciprocalMethod.Fast:
                    return GetType().GetMethod("Unity_Reciprocal_Fast", BindingFlags.Static | BindingFlags.NonPublic);
                default:
                    return GetType().GetMethod("Unity_Reciprocal", BindingFlags.Static | BindingFlags.NonPublic);
            }
        }

        [HlslCodeGen]
        static void Unity_Reciprocal(
            [Slot(0, Binding.None, 1, 1, 1, 1)] [AnyDimension] Float4 In,
            [Slot(1, Binding.None)] [AnyDimension] out Float4 Out)
        {
            Out = 1.0 / In;
        }

        [HlslCodeGen]
        static void Unity_Reciprocal_Fast(
            [Slot(0, Binding.None, 1, 1, 1, 1)] [AnyDimension] Float4 In,
            [Slot(1, Binding.None)] [AnyDimension] out Float4 Out)
        {
            Out = rcp(In);
        }
    }
}
