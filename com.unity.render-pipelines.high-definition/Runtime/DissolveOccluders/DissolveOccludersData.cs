// custom-begin:
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public static class DissolveOccludersData
    {
        [GenerateHLSL]
        public struct DissolveOccludersCylinder
        {
            public Vector3 positionNDC;
            public Vector2 radiusScaleBiasNDC;
        }
    }
}
// custom-end