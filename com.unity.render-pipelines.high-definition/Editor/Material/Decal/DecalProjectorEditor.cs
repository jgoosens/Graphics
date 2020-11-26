using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.ShortcutManagement;
using static UnityEditorInternal.EditMode;
using UnityEditor.IMGUI.Controls;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace UnityEditor.Rendering.HighDefinition
{
    [CustomEditor(typeof(DecalProjector))]
    [CanEditMultipleObjects]
    partial class DecalProjectorEditor : Editor
    {
        MaterialEditor m_MaterialEditor = null;
        SerializedProperty m_MaterialProperty;
        SerializedProperty m_DrawDistanceProperty;
        SerializedProperty m_FadeScaleProperty;
        SerializedProperty m_StartAngleFadeProperty;
        SerializedProperty m_EndAngleFadeProperty;
        SerializedProperty m_UVScaleProperty;
        SerializedProperty m_UVBiasProperty;
        SerializedProperty m_AffectsTransparencyProperty;
        SerializedProperty m_Size;
        SerializedProperty[] m_SizeValues;
        SerializedProperty m_Offset;
        SerializedProperty m_OffsetZ;
        SerializedProperty m_FadeFactor;
        SerializedProperty m_DecalLayerMask;

        int layerMask => (target as Component).gameObject.layer;
        bool layerMaskHasMultipleValue
        {
            get
            {
                if (targets.Length < 2)
                    return false;
                int layerMask = (targets[0] as Component).gameObject.layer;
                for (int index = 0; index < targets.Length; ++index)
                {
                    if ((targets[index] as Component).gameObject.layer != layerMask)
                        return true;
                }
                return false;
            }
        }

        bool showAffectTransparency => ((target as DecalProjector).material != null) && DecalSystem.IsHDRenderPipelineDecal((target as DecalProjector).material.shader);

        bool showAffectTransparencyHaveMultipleDifferentValue
        {
            get
            {
                if (targets.Length < 2)
                    return false;
                DecalProjector decalProjector0 = (targets[0] as DecalProjector);
                bool show = decalProjector0.material != null && DecalSystem.IsHDRenderPipelineDecal(decalProjector0.material.shader);
                for (int index = 0; index < targets.Length; ++index)
                {
                    if ((targets[index] as DecalProjector).material != null)
                    {
                        DecalProjector decalProjectori = (targets[index] as DecalProjector);
                        if (decalProjectori != null && DecalSystem.IsHDRenderPipelineDecal(decalProjectori.material.shader) ^ show)
                            return true;
                    }
                }
                return false;
            }
        }

        static HierarchicalBox s_Handle;
        static HierarchicalBox handle
        {
            get
            {
                if (s_Handle == null || s_Handle.Equals(null))
                {
                    s_Handle = new HierarchicalBox(k_GizmoColorBase, k_BaseHandlesColor);
                    s_Handle.monoHandle = false;
                }
                return s_Handle;
            }
        }
        
        static readonly BoxBoundsHandle s_AreaLightHandle =
            new BoxBoundsHandle { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y };

        const SceneViewEditMode k_EditShapeWithoutPreservingUV = (SceneViewEditMode)90;
        const SceneViewEditMode k_EditShapePreservingUV = (SceneViewEditMode)91;
        const SceneViewEditMode k_EditUVAndPivot = (SceneViewEditMode)92;
        static readonly SceneViewEditMode[] k_EditVolumeModes = new SceneViewEditMode[]
        {
            k_EditShapeWithoutPreservingUV,
            k_EditShapePreservingUV
        };
        static readonly SceneViewEditMode[] k_EditUVAndPivotModes = new SceneViewEditMode[]
        {
            k_EditUVAndPivot
        };

        static Func<Vector3, Quaternion, Vector3> s_DrawPivotHandle;

        static GUIContent[] k_EditVolumeLabels = null;
        static GUIContent[] editVolumeLabels => k_EditVolumeLabels ?? (k_EditVolumeLabels = new GUIContent[]
        {
            EditorGUIUtility.TrIconContent("d_ScaleTool", k_EditShapeWithoutPreservingUVTooltip),
            EditorGUIUtility.TrIconContent("d_RectTool", k_EditShapePreservingUVTooltip)
        });
        static GUIContent[] k_EditPivotLabels = null;
        static GUIContent[] editPivotLabels => k_EditPivotLabels ?? (k_EditPivotLabels = new GUIContent[]
        {
            EditorGUIUtility.TrIconContent("d_MoveTool", k_EditUVTooltip)
        });

        static List<DecalProjectorEditor> s_Instances = new List<DecalProjectorEditor>();

        static DecalProjectorEditor FindEditorFromSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            DecalProjector[] selectionTargets = Selection.GetFiltered<DecalProjector>(SelectionMode.Unfiltered);

            foreach (DecalProjectorEditor editor in s_Instances)
            {
                Debug.Log("Check Start");
                if (selectionTargets.Length != editor.targets.Length)
                {
                    Debug.Log("Length missmatch");
                    continue;
                }
                bool allOk = true;
                foreach (DecalProjector selectionTarget in selectionTargets)
                    if (!Array.Find(editor.targets, t => t == selectionTarget))
                    {
                        Debug.Log("Missing " + selectionTarget);
                        allOk = false;
                        break;
                    }
                if (!allOk)
                    continue;
                Debug.Log("All good");
                return editor;
            }
            return null;
        }

        static DecalProjectorEditor()
        {
            Type TransformHandleIdsType = Type.GetType("UnityEditor.Handles.TransformHandleIds, UnityEditor");
            var transformTranslationXHash = Expression.Variable(typeof(int), "TransformTranslationXHash");
            var transformTranslationYHash = Expression.Variable(typeof(int), "TransformTranslationYHash");
            var transformTranslationXYHash = Expression.Variable(typeof(int), "TransformTranslationXYHash");
            Type transformHandleParamType = Type.GetType("UnityEditor.Handles.TransformHandleParam, UnityEditor");
            //var transformHandleParam = Expression.Variable(transformHandleParamType, "TransformHandleParam");

        }


        private void OnEnable()
        {
            s_Instances.Add(this);

            // Create an instance of the MaterialEditor
            UpdateMaterialEditor();
            foreach (var decalProjector in targets)
            {
                (decalProjector as DecalProjector).OnMaterialChange += RequireUpdateMaterialEditor;
            }

            // Fetch serialized properties
            m_MaterialProperty = serializedObject.FindProperty("m_Material");
            m_DrawDistanceProperty = serializedObject.FindProperty("m_DrawDistance");
            m_FadeScaleProperty = serializedObject.FindProperty("m_FadeScale");
            m_StartAngleFadeProperty = serializedObject.FindProperty("m_StartAngleFade");
            m_EndAngleFadeProperty = serializedObject.FindProperty("m_EndAngleFade");
            m_UVScaleProperty = serializedObject.FindProperty("m_UVScale");
            m_UVBiasProperty = serializedObject.FindProperty("m_UVBias");
            m_AffectsTransparencyProperty = serializedObject.FindProperty("m_AffectsTransparency");
            m_Size = serializedObject.FindProperty("m_Size");
            m_SizeValues = new[]
            {
                m_Size.FindPropertyRelative("x"),
                m_Size.FindPropertyRelative("y"),
                m_Size.FindPropertyRelative("z"),
            };
            m_Offset = serializedObject.FindProperty("m_Offset");
            m_OffsetZ = m_Offset.FindPropertyRelative("z");
            m_FadeFactor = serializedObject.FindProperty("m_FadeFactor");
            m_DecalLayerMask = serializedObject.FindProperty("m_DecalLayerMask");
        }

        private void OnDisable()
        {
            foreach (DecalProjector decalProjector in targets)
            {
                if (decalProjector != null)
                    decalProjector.OnMaterialChange -= RequireUpdateMaterialEditor;
            }

            s_Instances.Remove(this);
        }

        private void OnDestroy() =>
            DestroyImmediate(m_MaterialEditor);

        public bool HasFrameBounds()
        {
            return true;
        }

        public Bounds OnGetFrameBounds()
        {
            DecalProjector decalProjector = target as DecalProjector;

            return new Bounds(decalProjector.transform.position, handle.size);
        }

        private bool m_RequireUpdateMaterialEditor = false;

        private void RequireUpdateMaterialEditor() => m_RequireUpdateMaterialEditor = true;

        public void UpdateMaterialEditor()
        {
            int validMaterialsCount = 0;
            for (int index = 0; index < targets.Length; ++index)
            {
                DecalProjector decalProjector = (targets[index] as DecalProjector);
                if ((decalProjector != null) && (decalProjector.material != null))
                    validMaterialsCount++;
            }
            // Update material editor with the new material
            UnityEngine.Object[] materials = new UnityEngine.Object[validMaterialsCount];
            validMaterialsCount = 0;
            for (int index = 0; index < targets.Length; ++index)
            {
                DecalProjector decalProjector = (targets[index] as DecalProjector);

                if ((decalProjector != null) && (decalProjector.material != null))
                    materials[validMaterialsCount++] = (targets[index] as DecalProjector).material;
            }
            m_MaterialEditor = (MaterialEditor)CreateEditor(materials);
        }

        void OnSceneGUI()
        {
            //called on each targets
            DrawHandles();
        }

        void DrawHandles()
        {
            DecalProjector decalProjector = target as DecalProjector;

            if (editMode == k_EditShapePreservingUV || editMode == k_EditShapeWithoutPreservingUV)
            {
                using (new Handles.DrawingScope(Color.white, Matrix4x4.TRS(decalProjector.transform.position, decalProjector.transform.rotation, Vector3.one)))
                {
                    bool needToRefreshDecalProjector = false;

                    Vector3 centerStart = new Vector3(
                        -decalProjector.offset.x,
                        -decalProjector.offset.y,
                        decalProjector.size.z * .5f - decalProjector.offset.z);
                    handle.center = centerStart;
                    handle.size = decalProjector.size;

                    Vector3 boundsSizePreviousOS = handle.size;
                    Vector3 boundsMinPreviousOS = handle.size * -0.5f + handle.center;

                    EditorGUI.BeginChangeCheck();
                    handle.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        needToRefreshDecalProjector = true;

                        // Adjust decal transform if handle changed.
                        Undo.RecordObjects(new UnityEngine.Object[] { decalProjector, decalProjector.transform }, "Decal Projector Change");

                        decalProjector.size = handle.size;
                        //decalProjector.offset = handle.center;
                        decalProjector.transform.position += decalProjector.transform.rotation * (handle.center - centerStart);

                        Vector3 boundsSizeCurrentOS = handle.size;
                        Vector3 boundsMinCurrentOS = handle.size * -0.5f + handle.center;

                        if (editMode == k_EditShapePreservingUV)
                        {
                            // Treat decal projector bounds as a crop tool, rather than a scale tool.
                            // Compute a new uv scale and bias terms to pin decal projection pixels in world space, irrespective of projector bounds.
                            Vector2 uvScale = decalProjector.uvScale;
                            uvScale.x *= Mathf.Max(1e-5f, boundsSizeCurrentOS.x) / Mathf.Max(1e-5f, boundsSizePreviousOS.x);
                            uvScale.y *= Mathf.Max(1e-5f, boundsSizeCurrentOS.y) / Mathf.Max(1e-5f, boundsSizePreviousOS.y);
                            decalProjector.uvScale = uvScale;

                            Vector2 uvBias = decalProjector.uvBias;
                            uvBias.x += (boundsMinCurrentOS.x - boundsMinPreviousOS.x) / Mathf.Max(1e-5f, boundsSizeCurrentOS.x) * decalProjector.uvScale.x;
                            uvBias.y += (boundsMinCurrentOS.y - boundsMinPreviousOS.y) / Mathf.Max(1e-5f, boundsSizeCurrentOS.y) * decalProjector.uvScale.y;
                            decalProjector.uvBias = uvBias;
                        }

                        if (PrefabUtility.IsPartOfNonAssetPrefabInstance(decalProjector))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(decalProjector);
                        }
                    }

                    // Automatically recenter our transform component if necessary.
                    // In order to correctly handle world-space snapping, we only perform this recentering when the user is no longer interacting with the gizmo.
                    //if (GUIUtility.hotControl == 0 && decalProjector.offset != Vector3.zero)
                    //{
                    //    needToRefreshDecalProjector = true;

                    //    // Both the DecalProjectorComponent, and the transform will be modified.
                    //    // The undo system will automatically group all RecordObject() calls here into a single action.
                    //    Undo.RecordObject(decalProjector.transform, "Decal Projector Change");

                    //    // Re-center the transform to the center of the decal projector bounds,
                    //    // while maintaining the world-space coordinates of the decal projector boundings vertices.
                    //    // Center of the decal projector is not the same of the HierarchicalBox as we want it to be on the z face as lights
                    //    decalProjector.transform.Translate(decalProjector.offset + new Vector3(0f, 0f, handle.size.z * -0.5f), Space.Self);

                    //    decalProjector.offset = new Vector3(0f, 0f, handle.size.z * 0.5f);
                    //    if (PrefabUtility.IsPartOfNonAssetPrefabInstance(decalProjector))
                    //    {
                    //        PrefabUtility.RecordPrefabInstancePropertyModifications(decalProjector);
                    //    }
                    //}

                    if (needToRefreshDecalProjector)
                    {
                        // Smoothly update the decal image projected
                        DecalSystem.instance.UpdateCachedData(decalProjector.Handle, decalProjector.GetCachedDecalData());
                    }
                }
            }

            else if (editMode == k_EditUVAndPivot)
            {
                // Pivot
                using (new Handles.DrawingScope(Color.white, Matrix4x4.TRS(Vector3.zero, decalProjector.transform.rotation, Vector3.one)))
                {
                    var isHot = ids.Has(GUIUtility.hotControl);
                    var planeSize = isHot ? paramXY.planeSize + paramXY.planeOffset : paramXY.planeSize;
                    var planarSize = Mathf.Max(planeSize[0], planeSize[s_DoPositionHandle_Internal_NextIndex[0]]);
                    Vector3 sliderRotatedWorldPos = Quaternion.Inverse(decalProjector.transform.rotation) * decalProjector.transform.position;
                    Vector3 beforeManipulationRotatedWorldPos = sliderRotatedWorldPos;
                    var size1D = HandleUtility.GetHandleSize(sliderRotatedWorldPos);
                    var size2D = HandleUtility.GetHandleSize(sliderRotatedWorldPos - new Vector3(0, 0, decalProjector.offset.z)) * planarSize * .5f;
                    Vector3 depthSlider = sliderRotatedWorldPos;

                    EditorGUI.BeginChangeCheck();
                    {
                        // dot offset = transform position seen as a sphere
                        EditorGUI.BeginChangeCheck();
                        depthSlider = Handles.Slider(depthSlider, Vector3.forward, size1D * .1f, Handles.SphereHandleCap, -1);
                        if (EditorGUI.EndChangeCheck())
                            sliderRotatedWorldPos.z = depthSlider.z;

                        // 2D slider: square xy-axis
                        Vector3 sliderFaceProjected = sliderRotatedWorldPos - new Vector3(0, 0, decalProjector.offset.z);
                        sliderFaceProjected.x += size2D;
                        sliderFaceProjected.y += size2D;
                        using (new Handles.DrawingScope(Handles.zAxisColor))
                        {
                            verts[0] = sliderFaceProjected + (Vector3.right + Vector3.up) * size2D;
                            verts[1] = sliderFaceProjected + (-Vector3.right + Vector3.up) * size2D;
                            verts[2] = sliderFaceProjected + (-Vector3.right - Vector3.up) * size2D;
                            verts[3] = sliderFaceProjected + (Vector3.right - Vector3.up) * size2D;
                            float faceOpacity = 0.8f;
                            if (GUIUtility.hotControl == ids.xy)
                                Handles.color = Handles.selectedColor;
                            else if (IsHovering(ids.xy, Event.current))
                                faceOpacity = 0.4f;
                            else
                                faceOpacity = 0.1f;
                            Color faceColor = new Color(Handles.zAxisColor.r, Handles.zAxisColor.g, Handles.zAxisColor.b, Handles.zAxisColor.a * faceOpacity);
                            Handles.DrawSolidRectangleWithOutline(verts, faceColor, Color.clear);
                            EditorGUI.BeginChangeCheck();
                            sliderFaceProjected = Handles.Slider2D(ids.xy, sliderFaceProjected, Vector3.forward, Vector3.right, Vector3.up, size2D, Handles.RectangleHandleCap, GridSnapping.active ? Vector2.zero : new Vector2(EditorSnapSettings.move[0], EditorSnapSettings.move[1]), false);
                            if (EditorGUI.EndChangeCheck())
                            {
                                sliderRotatedWorldPos.x = sliderFaceProjected.x;
                                sliderRotatedWorldPos.y = sliderFaceProjected.y;
                            }
                        }
                        sliderFaceProjected.x -= size2D;
                        sliderFaceProjected.y -= size2D;

                        // 2D slider: x-axis
                        EditorGUI.BeginChangeCheck();
                        using (new Handles.DrawingScope(Handles.xAxisColor))
                            sliderFaceProjected = Handles.Slider(sliderFaceProjected, Vector3.right);
                        if (EditorGUI.EndChangeCheck())
                            sliderRotatedWorldPos.x = sliderFaceProjected.x;

                        // 2D slider: y-axis
                        EditorGUI.BeginChangeCheck();
                        using (new Handles.DrawingScope(Handles.yAxisColor))
                            sliderFaceProjected = Handles.Slider(sliderFaceProjected, Vector3.up);
                        if (EditorGUI.EndChangeCheck())
                            sliderRotatedWorldPos.y = sliderFaceProjected.y;

                        // depth: z-axis
                        EditorGUI.BeginChangeCheck();
                        using (new Handles.DrawingScope(Handles.zAxisColor))
                            depthSlider = Handles.Slider(depthSlider, Vector3.forward);
                        if (EditorGUI.EndChangeCheck())
                            sliderRotatedWorldPos.z = depthSlider.z;
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        Vector3 newPosition = decalProjector.transform.rotation * sliderRotatedWorldPos;
                        decalProjector.transform.position = newPosition;
                        decalProjector.offset += sliderRotatedWorldPos - beforeManipulationRotatedWorldPos;
                    }
                }

                // UV
                //using (new Handles.DrawingScope(Matrix4x4.TRS(decalProjector.transform.position + decalProjector.transform.rotation * new Vector3(-decalProjector.offset.x, -decalProjector.offset.y, -decalProjector.offset.z), decalProjector.transform.rotation, Vector3.one)))
                using (new Handles.DrawingScope(Matrix4x4.TRS(decalProjector.transform.position - decalProjector.transform.rotation * (new Vector3(decalProjector.size.x * 0.5f, decalProjector.size.y * 0.5f, 0) + decalProjector.offset), decalProjector.transform.rotation, Vector3.one)))
                {
                    const float k_Limit = 100000;
                    const float k_LimitInv = 1/k_Limit;
                    Vector2 UVSize = new Vector2(
                        decalProjector.uvScale.x > k_Limit || decalProjector.uvScale.x < -k_Limit ? 0f : decalProjector.size.x / decalProjector.uvScale.x,
                        decalProjector.uvScale.y > k_Limit || decalProjector.uvScale.y < -k_Limit ? 0f : decalProjector.size.y / decalProjector.uvScale.y
                        );
                    Vector2 UVStart = -new Vector2(decalProjector.uvBias.x * UVSize.x, decalProjector.uvBias.y * UVSize.y);
                    Vector2 UVCenter = UVStart + UVSize * 0.5f;
                    EditorGUI.BeginChangeCheck();
                    UVSize = Handles.DoRectHandles(Quaternion.identity, UVCenter /*decalProjector.uvBias*/, UVSize, handlesOnly: true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Vector2 limit = new Vector2(Mathf.Abs(decalProjector.size.x * k_LimitInv), Mathf.Abs(decalProjector.size.y * k_LimitInv));
                        Vector2 uvScale = decalProjector.uvScale;
                        for (int channel = 0; channel < 2; channel++)
                        {
                            if (Mathf.Abs(UVSize[channel]) < limit[channel])
                                uvScale[channel] = decalProjector.size[channel] / UVSize[channel];
                            else
                                uvScale[channel] = Mathf.Sign(decalProjector.size[channel]) * Mathf.Sign(UVSize[channel]) * k_Limit;
                        }
                        decalProjector.uvScale = uvScale;
                    }
                }
            }
        }

        static Handles.PositionHandleParam paramXY = new Handles.PositionHandleParam(
            Handles.PositionHandleParam.Handle.X | Handles.PositionHandleParam.Handle.Y | Handles.PositionHandleParam.Handle.XY,
            Vector3.zero, Vector3.one, Vector3.zero, Vector3.one * .25f,
            Handles.PositionHandleParam.Orientation.Signed, Handles.PositionHandleParam.Orientation.Camera);

        static Handles.PositionHandleParam paramZ = new Handles.PositionHandleParam(
            Handles.PositionHandleParam.Handle.Z,
            Vector3.zero, Vector3.one, Vector3.zero, Vector3.one * .25f,
            Handles.PositionHandleParam.Orientation.Signed, Handles.PositionHandleParam.Orientation.Camera);

        static Handles.PositionHandleIds ids = new Handles.PositionHandleIds(
            "xAxisDecalPivot".GetHashCode(),
            "yAxisDecalPivot".GetHashCode(),
            "zAxisDecalPivot".GetHashCode(),
            "xyAxisDecalPivot".GetHashCode(),
            0, //unused: "xzAxisDecalPivot".GetHashCode(),
            0, //unused: "yzAxisDecalPivot".GetHashCode(),
            0  //unused: "FreeMoveDecalPivot".GetHashCode()
        );

        // If the user is currently mouse dragging then this value will be True
        // and will disallow toggling Free Move mode on or off, or changing the octant of the planar handles.
        static bool currentlyDragging { get { return EditorGUIUtility.hotControl != 0; } }

        // While the user has Free Move mode turned on by holding 'shift' or 'V' (for Vertex Snapping),
        // this variable will be set to True.
        static bool s_FreeMoveMode = false;

        static float[] s_DoPositionHandle_Internal_CameraViewLerp = new float[6];

        static Vector3 DoPivotHandle(Vector3 position, Quaternion rotation)
        {
            //EditorGUI.BeginChangeCheck();
            //Vector3 res = Handles.DoPositionHandle_Internal(ids, (Vector2)position, Quaternion.identity, paramXY);
            //Vector3 res2 = Handles.DoPositionHandle_Internal(ids, position, Quaternion.identity, paramZ);
            //if (EditorGUI.EndChangeCheck())
            //{
            //    position.x = res.x;
            //    position.y = res.y;
            //    position.z = res2.z;
            //}
            //return position;

            // Calculate the camera view vector in Handle draw space
            // this handle the case where the matrix is skewed
            var handlePosition = Handles.matrix.MultiplyPoint3x4(position);
            var drawToWorldMatrix = Handles.matrix * Matrix4x4.TRS(position, rotation, Vector3.one);
            var invDrawToWorldMatrix = drawToWorldMatrix.inverse;
            var viewVectorDrawSpace = GetCameraViewFrom(handlePosition, invDrawToWorldMatrix);
            
            var size = HandleUtility.GetHandleSize(position);

            // Calculate per axis camera lerp
            for (var i = 0; i < 3; ++i)
                s_DoPositionHandle_Internal_CameraViewLerp[i] = ids[i] == GUIUtility.hotControl ? 0 : GetCameraViewLerpForWorldAxis(viewVectorDrawSpace, s_AxisVector[i]);
            // Calculate per plane camera lerp (xy, yz, xz)
            for (var i = 0; i < 3; ++i)
                s_DoPositionHandle_Internal_CameraViewLerp[3 + i] = Mathf.Max(s_DoPositionHandle_Internal_CameraViewLerp[i], s_DoPositionHandle_Internal_CameraViewLerp[(i + 1) % 3]);
            
            var isHot = ids.Has(GUIUtility.hotControl);
            var planeOffset = paramXY.planeOffset;
            if (isHot)
            {
                //axisOffset = Vector3.zero;
                planeOffset = Vector3.zero;
            }

            var planeSize = isHot ? paramXY.planeSize + paramXY.planeOffset : paramXY.planeSize;

            int i_xy = 0;
            if (paramXY.ShouldShow(3 + i_xy) && (!isHot || ids[3 + i_xy] == GUIUtility.hotControl))
            {
                var cameraLerp = isHot ? 0 : s_DoPositionHandle_Internal_CameraViewLerp[3 + i_xy];
                if (cameraLerp <= kCameraViewThreshold)
                {
                    var offset = planeOffset * size;
                    offset[s_DoPositionHandle_Internal_PrevIndex[i_xy]] = 0;
                    var planarSize = Mathf.Max(planeSize[i_xy], planeSize[s_DoPositionHandle_Internal_NextIndex[i_xy]]);
                    position = DoPlanarHandle(ids[3 + i_xy], i_xy, position, offset, rotation, size * planarSize, cameraLerp, viewVectorDrawSpace, paramXY.planeOrientation);
                }
            }

            //Handles.Slider(ids[i], position, offset, dir, size * param.axisSize[i], DoPositionHandle_ArrowCap, GridSnapping.active ? 0f : EditorSnapSettings.move[i]);

            return position;
        }

        // When axis is looking away from camera, fade it out along 25 -> 15 degrees range
        static readonly float kCameraViewLerpStart1 = Mathf.Cos(Mathf.Deg2Rad * 25.0f);
        static readonly float kCameraViewLerpEnd1 = Mathf.Cos(Mathf.Deg2Rad * 15.0f);
        // When axis is looking towards the camera, fade it out along 170 -> 175 degrees range
        static readonly float kCameraViewLerpStart2 = Mathf.Cos(Mathf.Deg2Rad * 170.0f);
        static readonly float kCameraViewLerpEnd2 = Mathf.Cos(Mathf.Deg2Rad * 175.0f);
        // Hide & disable axis if they have faded out more than 60%
        internal const float kCameraViewThreshold = 0.6f;


        static int[] s_DoPositionHandle_Internal_NextIndex = { 1, 2, 0 };
        static int[] s_DoPositionHandle_Internal_PrevIndex = { 2, 0, 1 };

        static Vector3[] s_AxisVector = { Vector3.right, Vector3.up, Vector3.forward };

        internal static float GetCameraViewLerpForWorldAxis(Vector3 viewVector, Vector3 axis)
        {
            var dot = Vector3.Dot(viewVector, axis);
            var l1 = Mathf.InverseLerp(kCameraViewLerpStart1, kCameraViewLerpEnd1, dot);
            var l2 = Mathf.InverseLerp(kCameraViewLerpStart2, kCameraViewLerpEnd2, dot);
            return Mathf.Max(l1, l2);
        }
        
        internal static Vector3 GetCameraViewFrom(Vector3 position, Matrix4x4 matrix)
        {
            Camera camera = Camera.current;
            return camera.orthographic
                ? matrix.MultiplyVector(camera.transform.forward).normalized
                : matrix.MultiplyVector(position - camera.transform.position).normalized;
        }

        internal static Color GetFadedAxisColor(Color col, float fade, int id)
        {
            // never fade out axes that are being hover-highlighted or currently interacted with
            if (id != 0 && id == GUIUtility.hotControl || id == HandleUtility.nearestControl)
                fade = 0;
            col = Color.Lerp(col, Color.clear, fade);
            return col;
        }

        internal static bool IsHovering(int controlID, Event evt)
        {
            return controlID == HandleUtility.nearestControl && GUIUtility.hotControl == 0 && !Tools.viewToolActive;
        }

        internal static Color ToActiveColorSpace(Color color)
        {
            return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color.linear : color;
        }

        static Vector3 s_PlanarHandlesOctant = Vector3.one;
        static Vector3[] verts = { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };

        static Vector3 DoPlanarHandle(
            int id,
            int planePrimaryAxis,
            Vector3 position,
            Vector3 offset,
            Quaternion rotation,
            float handleSize,
            float cameraLerp,
            Vector3 viewVectorDrawSpace,
            Handles.PositionHandleParam.Orientation orientation)
        {
            var positionOffset = offset;

            var axis1index = planePrimaryAxis;
            var axis2index = (axis1index + 1) % 3;
            var axisNormalIndex = (axis1index + 2) % 3;

            Color prevColor = Handles.color;

            bool isDisabled = !GUI.enabled;
            Color color = isDisabled ? new Color(.5f, .5f, .5f, 0f) : Handles.zAxisColor;
            color = GetFadedAxisColor(color, cameraLerp, id);

            float faceOpacity = 0.8f;
            if (GUIUtility.hotControl == id)
                color = Handles.selectedColor;
            else if (IsHovering(id, Event.current))
                faceOpacity = 0.4f;
            else
                faceOpacity = 0.1f;

            color = ToActiveColorSpace(color);
            Handles.color = color;


            // NOTE: The planar transform handles always face toward the camera so they won't
            // obscure each other (unlike the X, Y, and Z axis handles which always face in the
            // positive axis directions). Whenever the octant that the camera is in (relative to
            // to the transform tool) changes, we need to move the planar transform handle
            // positions to the correct octant.

            // Comments below assume axis1 is X and axis2 is Z to make it easier to visualize things.

            // Shift the planar transform handle in the positive direction by half its
            // handleSize so that it doesn't overlap in the center of the transform gizmo,
            // and also move the handle origin into the octant that the camera is in.
            // Don't update the actant while dragging to avoid too much distraction.
            if (!currentlyDragging)
            {
                switch (orientation)
                {
                    case Handles.PositionHandleParam.Orientation.Camera:
                        // Offset the X position of the handle in negative direction if camera is in the -X octants; otherwise positive.
                        // Test against -0.01 instead of 0 to give a little bias to the positive quadrants. This looks better in axis views.
                        s_PlanarHandlesOctant[axis1index] = (viewVectorDrawSpace[axis1index] > 0.01f ? -1 : 1);
                        // Likewise with the other axis.
                        s_PlanarHandlesOctant[axis2index] = (viewVectorDrawSpace[axis2index] > 0.01f ? -1 : 1);
                        break;
                    case Handles.PositionHandleParam.Orientation.Signed:
                        s_PlanarHandlesOctant[axis1index] = 1;
                        s_PlanarHandlesOctant[axis2index] = 1;
                        break;
                }
            }
            Vector3 handleOffset = s_PlanarHandlesOctant;
            // Zero out the offset along the normal axis.
            handleOffset[axisNormalIndex] = 0;
            positionOffset = rotation * Vector3.Scale(positionOffset, handleOffset);
            // Rotate and scale the offset
            handleOffset = rotation * (handleOffset * handleSize * 0.5f);

            // Calculate 3 axes
            Vector3 axis1 = Vector3.zero;
            Vector3 axis2 = Vector3.zero;
            Vector3 axisNormal = Vector3.zero;
            axis1[axis1index] = 1;
            axis2[axis2index] = 1;
            axisNormal[axisNormalIndex] = 1;
            axis1 = rotation * axis1;
            axis2 = rotation * axis2;
            axisNormal = rotation * axisNormal;

            // Draw the "filler" color for the handle
            verts[0] = position + positionOffset + handleOffset + (axis1 + axis2) * handleSize * 0.5f;
            verts[1] = position + positionOffset + handleOffset + (-axis1 + axis2) * handleSize * 0.5f;
            verts[2] = position + positionOffset + handleOffset + (-axis1 - axis2) * handleSize * 0.5f;
            verts[3] = position + positionOffset + handleOffset + (axis1 - axis2) * handleSize * 0.5f;
            Color faceColor = new Color(color.r, color.g, color.b, color.a * faceOpacity);
            Handles.DrawSolidRectangleWithOutline(verts, faceColor, Color.clear);

            // And then render the handle itself (this is the colored outline)
            position = Handles.Slider2D(id,
                position,
                handleOffset + positionOffset,
                axisNormal,
                axis1, axis2,
                handleSize * 0.5f,
                Handles.RectangleHandleCap,
                GridSnapping.active ? Vector2.zero : new Vector2(EditorSnapSettings.move[axis1index], EditorSnapSettings.move[axis2index]),
                false);

            Handles.color = prevColor;

            return position;
        }

        //static Vector3 DoPivotHandle(Vector3 position, Quaternion rotation)
        //{
        //    Event evt = Event.current;
        //    switch (evt.type)
        //    {
        //        case EventType.KeyDown:
        //            // Holding 'V' turns on the FreeMove transform gizmo and enables vertex snapping.
        //            if (evt.keyCode == KeyCode.V && !currentlyDragging)
        //            {
        //                s_FreeMoveMode = true;
        //            }
        //            break;

        //        case EventType.KeyUp:
        //            // If the user has released the 'V' key, then rendering the transform gizmo
        //            // one last time with Free Move mode off is technically incorrect since it can
        //            // add one additional frame of input with FreeMove enabled, but the
        //            // implementation is a fair bit simpler this way.
        //            // Basic idea: Leave this call above the 'if' statement.
        //            EditorGUI.BeginChangeCheck();
        //            Vector3 res = Handles.DoPositionHandle_Internal(ids, (Vector2)position, Quaternion.identity, paramXY);
        //            Vector3 res2 = Handles.DoPositionHandle_Internal(ids, position, Quaternion.identity, paramZ);
        //            if (EditorGUI.EndChangeCheck())
        //            {
        //                position.x = res.x;
        //                position.y = res.y;
        //                position.z = res2.z;
        //            }
        //            if (evt.keyCode == KeyCode.V && !evt.shift && !currentlyDragging)
        //            {
        //                s_FreeMoveMode = false;
        //            }
        //            return position;

        //        case EventType.Layout:
        //            if (!currentlyDragging && !Tools.vertexDragging)
        //            {
        //                s_FreeMoveMode = evt.shift;
        //            }
        //            break;
        //    }

        //    if (s_FreeMoveMode)
        //        position = Handles.DoPositionHandle_Internal(Handles.PositionHandleIds.@default, position, rotation, Handles.PositionHandleParam.DefaultFreeMoveHandle);
        //    else
        //    {
        //        EditorGUI.BeginChangeCheck();
        //        Vector3 res = Handles.DoPositionHandle_Internal(ids, (Vector2)position, Quaternion.identity, paramXY);
        //        Vector3 res2 = Handles.DoPositionHandle_Internal(ids, position, Quaternion.identity, paramZ);
        //        if (EditorGUI.EndChangeCheck())
        //        {
        //            position.x = res.x;
        //            position.y = res.y;
        //            position.z = res2.z;
        //        }
        //    }
        //    return position;
        //}

        static DisplacableRectHandles m_UVHandles = new DisplacableRectHandles(Color.white);

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawGizmosSelected(DecalProjector decalProjector, GizmoType gizmoType)
        {
            const float k_DotLength = 5f;

            //draw them scale independent
            using (new Handles.DrawingScope(Color.white, Matrix4x4.TRS(decalProjector.transform.position, decalProjector.transform.rotation, Vector3.one)))
            {
                //handle.center = Vector3.zero; //decalProjector.offset;
                handle.center = new Vector3(
                    -decalProjector.offset.x,
                    -decalProjector.offset.y,
                    decalProjector.size.z * .5f - decalProjector.offset.z);
                handle.size = decalProjector.size;
                bool isVolumeEditMode = editMode == k_EditShapePreservingUV || editMode == k_EditShapeWithoutPreservingUV;
                bool isPivotEditMode = editMode == k_EditUVAndPivot;
                handle.DrawHull(isVolumeEditMode);

                //Vector3 projectedPivot = decalProjector.offset + decalProjector.offset.z * Vector3.back;
                //projectedPivot.z = -decalProjector.offset.z;
                Vector3 pivot = Vector3.zero;
                Vector3 projectedPivot = new Vector3(0, 0, -decalProjector.offset.z);

                if (isPivotEditMode)
                {
                    Handles.DrawDottedLines(new[] { projectedPivot, pivot }, k_DotLength);
                }
                else
                {
                    Quaternion arrowRotation = Quaternion.LookRotation(Vector3.down, Vector3.right);
                    float arrowSize = decalProjector.size.z * 0.25f;
                    Handles.ArrowHandleCap(0, projectedPivot, Quaternion.identity, arrowSize, EventType.Repaint);
                }

                //draw UV and bolder edges
                //using (new Handles.DrawingScope(Matrix4x4.TRS(decalProjector.transform.position - decalProjector.transform.rotation * (new Vector3(decalProjector.size.x * 0.5f, decalProjector.size.y * 0.5f, 0) + decalProjector.offset), decalProjector.transform.rotation, Vector3.one)))
                //{
                //    if (isPivotEditMode)
                //    {
                //        Vector2 UVSizeX = new Vector2(
                //            (decalProjector.uvScale.x > 100000 || decalProjector.uvScale.x < -100000 ? 0f : 1f / decalProjector.uvScale.x) * decalProjector.size.x,
                //            0
                //            );
                //        Vector2 UVSizeY = new Vector2(
                //            0,
                //            (decalProjector.uvScale.y > 100000 || decalProjector.uvScale.y < -100000 ? 0f : 1f / decalProjector.uvScale.y) * decalProjector.size.y
                //            );
                //        Vector2 UVStart = -new Vector2(decalProjector.uvBias.x * UVSizeX.x, decalProjector.uvBias.y * UVSizeY.y);
                //        Handles.DrawDottedLines(
                //            new Vector3[]
                //            {
                //                UVStart,                        UVStart + UVSizeX,
                //                UVStart + UVSizeX,              UVStart + UVSizeX + UVSizeY,
                //                UVStart + UVSizeX + UVSizeY,    UVStart + UVSizeY,
                //                UVStart + UVSizeY,              UVStart
                //            },
                //            k_DotLength);
                //    }

                //    Vector2 size = decalProjector.size;
                //    Vector2 halfSize = size * .5f;
                //    Vector2 halfSize2 = new Vector2(halfSize.x, -halfSize.y);
                //    Handles.DrawLine(Vector2.zero,          halfSize - halfSize2, 3f);
                //    Handles.DrawLine(halfSize - halfSize2,  size, 3f);
                //    Handles.DrawLine(size,                  halfSize + halfSize2, 3f);
                //    Handles.DrawLine(halfSize + halfSize2,  Vector2.zero, 3f);
                //}
                using (new Handles.DrawingScope(Matrix4x4.TRS(decalProjector.transform.position - decalProjector.transform.rotation * decalProjector.offset, decalProjector.transform.rotation, Vector3.one)))
                {
                    if (isPivotEditMode)
                    {
                        Vector2 UVSizeX = new Vector2(
                            (decalProjector.uvScale.x > 100000 || decalProjector.uvScale.x < -100000 ? 0f : 1f / decalProjector.uvScale.x) * decalProjector.size.x,
                            0
                            );
                        Vector2 UVSizeY = new Vector2(
                            0,
                            (decalProjector.uvScale.y > 100000 || decalProjector.uvScale.y < -100000 ? 0f : 1f / decalProjector.uvScale.y) * decalProjector.size.y
                            );
                        Vector2 UVStart = -new Vector2(decalProjector.uvBias.x * UVSizeX.x, decalProjector.uvBias.y * UVSizeY.y);
                        Handles.DrawDottedLines(
                            new Vector3[]
                            {
                                UVStart,                        UVStart + UVSizeX,
                                UVStart + UVSizeX,              UVStart + UVSizeX + UVSizeY,
                                UVStart + UVSizeX + UVSizeY,    UVStart + UVSizeY,
                                UVStart + UVSizeY,              UVStart
                            },
                            k_DotLength);
                    }

                    Vector2 size = decalProjector.size;
                    Vector2 halfSize = size * .5f;
                    Vector2 halfSize2 = new Vector2(halfSize.x, -halfSize.y);
                    Handles.DrawLine(Vector2.zero, halfSize - halfSize2, 3f);
                    Handles.DrawLine(halfSize - halfSize2, size, 3f);
                    Handles.DrawLine(size, halfSize + halfSize2, 3f);
                    Handles.DrawLine(halfSize + halfSize2, Vector2.zero, 3f);
                }
            }
        }
        
        static Func<Bounds> GetBoundsGetter(DecalProjector decalProjector)
        {
            return () =>
            {
                var bounds = new Bounds();
                var decalTransform = decalProjector.transform;
                bounds.Encapsulate(decalTransform.position);
                return bounds;
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (m_RequireUpdateMaterialEditor)
            {
                UpdateMaterialEditor();
                m_RequireUpdateMaterialEditor = false;
            }

            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                DoInspectorToolbar(k_EditVolumeModes, editVolumeLabels, GetBoundsGetter(target as DecalProjector), this);
                DoInspectorToolbar(k_EditUVAndPivotModes, editPivotLabels, GetBoundsGetter(target as DecalProjector), this);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                Rect rect = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, k_SizeContent));
                EditorGUI.BeginProperty(rect, k_SizeSubContent[0], m_SizeValues[0]);
                EditorGUI.BeginProperty(rect, k_SizeSubContent[1], m_SizeValues[1]);
                float[] size = new float[2] { m_SizeValues[0].floatValue, m_SizeValues[1].floatValue };
                EditorGUI.BeginChangeCheck();
                EditorGUI.MultiFloatField(rect, k_SizeContent, k_SizeSubContent, size);
                if (EditorGUI.EndChangeCheck())
                {
                    m_SizeValues[0].floatValue = Mathf.Max(0, size[0]);
                    m_SizeValues[1].floatValue = Mathf.Max(0, size[1]);
                }
                EditorGUI.EndProperty();
                EditorGUI.EndProperty();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_SizeValues[2], k_ProjectionDepthContent);
                if (EditorGUI.EndChangeCheck())
                {
                    m_SizeValues[2].floatValue = Mathf.Max(0, m_SizeValues[2].floatValue);
                    m_OffsetZ.floatValue = m_SizeValues[2].floatValue * 0.5f;
                }

                EditorGUILayout.PropertyField(m_MaterialProperty, k_MaterialContent);

                bool decalLayerEnabled = false;
                HDRenderPipelineAsset hdrp = HDRenderPipeline.currentAsset;
                if (hdrp != null)
                {
                    decalLayerEnabled = hdrp.currentPlatformRenderPipelineSettings.supportDecals && hdrp.currentPlatformRenderPipelineSettings.supportDecalLayers;
                    using (new EditorGUI.DisabledScope(!decalLayerEnabled))
                    {
                        EditorGUILayout.PropertyField(m_DecalLayerMask, k_DecalLayerMaskContent);
                    }
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_DrawDistanceProperty, k_DistanceContent);
                if (EditorGUI.EndChangeCheck() && m_DrawDistanceProperty.floatValue < 0f)
                    m_DrawDistanceProperty.floatValue = 0f;

                EditorGUILayout.PropertyField(m_FadeScaleProperty, k_FadeScaleContent);
                using (new EditorGUI.DisabledScope(!decalLayerEnabled))
                {
                    float angleFadeMinValue = m_StartAngleFadeProperty.floatValue;
                    float angleFadeMaxValue = m_EndAngleFadeProperty.floatValue;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.MinMaxSlider(k_AngleFadeContent, ref angleFadeMinValue, ref angleFadeMaxValue, 0.0f, 180.0f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_StartAngleFadeProperty.floatValue = angleFadeMinValue;
                        m_EndAngleFadeProperty.floatValue = angleFadeMaxValue;
                    }
                }

                if (!decalLayerEnabled)
                {
                    EditorGUILayout.HelpBox("Enable 'Decal Layers' in your HDRP Asset if you want to control the Angle Fade. There is a performance cost of enabling this option.",
                        MessageType.Info);
                }

                EditorGUILayout.PropertyField(m_UVScaleProperty, k_UVScaleContent);
                EditorGUILayout.PropertyField(m_UVBiasProperty, k_UVBiasContent);
                EditorGUILayout.PropertyField(m_FadeFactor, k_FadeFactorContent);

                // only display the affects transparent property if material is HDRP/decal
                if (showAffectTransparencyHaveMultipleDifferentValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.LabelField(EditorGUIUtility.TrTextContent("Multiple material type in selection"));
                }
                else if (showAffectTransparency)
                    EditorGUILayout.PropertyField(m_AffectsTransparencyProperty, k_AffectTransparentContent);

                EditorGUI.BeginChangeCheck();
                Vector3 previousOffset = m_Offset.vector3Value;
                EditorGUILayout.PropertyField(m_Offset);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targets.SelectMany(t => new[] { t, (t as DecalProjector).transform }).ToArray(), "Decal Projector Offset Change");
                    foreach (DecalProjector projector in targets)
                        projector.transform.position += projector.transform.rotation * (m_Offset.vector3Value - previousOffset);
                }
            }
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            if (layerMaskHasMultipleValue || layerMask != (target as Component).gameObject.layer)
            {
                foreach (var decalProjector in targets)
                {
                    (decalProjector as DecalProjector).OnValidate();
                }
            }

            if (m_MaterialEditor != null)
            {
                // We need to prevent the user to edit default decal materials
                bool isDefaultMaterial = false;
                bool isValidDecalMaterial = true;
                var hdrp = HDRenderPipeline.currentAsset;
                if (hdrp != null)
                {
                    foreach (var decalProjector in targets)
                    {
                        var mat = (decalProjector as DecalProjector).material;

                        isDefaultMaterial |= mat == hdrp.GetDefaultDecalMaterial();
                        isValidDecalMaterial = isValidDecalMaterial && DecalSystem.IsDecalMaterial(mat);
                    }
                }

                if (isValidDecalMaterial)
                {
                    // Draw the material's foldout and the material shader field
                    // Required to call m_MaterialEditor.OnInspectorGUI ();
                    m_MaterialEditor.DrawHeader();

                    using (new EditorGUI.DisabledGroupScope(isDefaultMaterial))
                    {
                        // Draw the material properties
                        // Works only if the foldout of m_MaterialEditor.DrawHeader () is open
                        m_MaterialEditor.OnInspectorGUI();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Decal only work with Decal Material. Decal Material can be selected in the shader list HDRP/Decal or can be created from a Decal Master Node.",
                        MessageType.Error);
                }
            }
        }
        
        [Shortcut("HDRP/Decal: Handle changing size stretching UV", typeof(SceneView), KeyCode.Keypad1, ShortcutModifiers.Action)]
        static void EnterEditModeWithoutPreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            ChangeEditMode(k_EditShapeWithoutPreservingUV, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("HDRP/Decal: Handle changing size cropping UV", typeof(SceneView), KeyCode.Keypad2, ShortcutModifiers.Action)]
        static void EnterEditModePreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            ChangeEditMode(k_EditShapePreservingUV, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }
        
        [Shortcut("HDRP/Decal: Handle changing pivot position and UVs", typeof(SceneView), KeyCode.Keypad3, ShortcutModifiers.Action)]
        static void EnterEditModePivotPreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            ChangeEditMode(k_EditUVAndPivot, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("HDRP/Decal: Handle swap between cropping and stretching UV", typeof(SceneView), KeyCode.W, ShortcutModifiers.Action)]
        static void SwappingEditUVMode(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            SceneViewEditMode targetMode = SceneViewEditMode.None;
            switch (editMode)
            {
                case k_EditShapePreservingUV:
                    targetMode = k_EditShapeWithoutPreservingUV;
                    break;
                case k_EditShapeWithoutPreservingUV:
                    targetMode = k_EditShapePreservingUV;
                    break;
            }
            if (targetMode != SceneViewEditMode.None)
                ChangeEditMode(targetMode, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("HDRP/Decal: Stop Editing", typeof(SceneView), KeyCode.Keypad0, ShortcutModifiers.Action)]
        static void ExitEditMode(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            QuitEditMode();
        }
    }
}


