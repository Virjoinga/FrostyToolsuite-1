using AssetBankPlugin.Ant;
using Frosty.Core;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using MeshSetPlugin.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;

namespace AssetBankPlugin.Export
{
    public static class AntRigExporter
    {
        public static InternalSkeleton BuildInternalSkeletonFromRig(RigAsset rig, string originalSkelPath = "")
        {
            if (rig == null) return null;

            if (string.IsNullOrEmpty(originalSkelPath))
            {
                var opt = new AnimationOptions();
                opt.Load();
                originalSkelPath = opt.ExportSkeletonAsset;
            }

            var defaultV4 = new Dictionary<uint, Quaternion>();
            if (rig.DefaultVector4Values != null)
            {
                foreach (var v in rig.DefaultVector4Values)
                {
                    if (v.Value != null && v.Value.Length >= 4)
                        defaultV4[v.DofId] = new Quaternion(v.Value[0], v.Value[1], v.Value[2], v.Value[3]);
                }
            }

            var defaultV3 = new Dictionary<uint, Vector3>();
            if (rig.DefaultVector3Values != null)
            {
                foreach (var v in rig.DefaultVector3Values)
                {
                    if (v.Value != null && v.Value.Length >= 3)
                        defaultV3[v.DofId] = new Vector3(v.Value[0], v.Value[1], v.Value[2]);
                }
            }

            var dofRotations = new Dictionary<string, Quaternion>(StringComparer.OrdinalIgnoreCase);
            var dofTranslations = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var dofScales = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);

            if (rig.DofIds != null && rig.RigDofSets != null)
            {
                int dofIdx = 0;
                for (int i = 0; i < rig.RigDofSets.Length; i++)
                {
                    var dsa = AntRefTable.Get(rig.RigDofSets[i]);
                    if (dsa != null && dsa.AssetType == "DofSetAsset")
                    {
                        var slotsObj = dsa.GetProperty<object>("Slots");
                        if (slotsObj is IEnumerable slotsList)
                        {
                            uint startDofIdx = (rig.DofSetIdIndices != null && i < rig.DofSetIdIndices.Length)
                                ? rig.DofSetIdIndices[i]
                                : (uint)dofIdx;

                            int slotCount = 0;
                            foreach (var slotItem in slotsList)
                            {
                                string slotName = "";
                                if (slotItem is Dictionary<string, object> sDict)
                                    slotName = sDict.TryGetValue("Name", out object sn) ? sn.ToString() : "";
                                else if (slotItem is AntAsset sAnt)
                                    slotName = sAnt.Name;

                                uint currentDofIdx = startDofIdx + (uint)slotCount;
                                if (currentDofIdx < rig.DofIds.Length)
                                {
                                    uint dofId = rig.DofIds[currentDofIdx];
                                    if (!string.IsNullOrEmpty(slotName))
                                    {
                                        int dot = slotName.LastIndexOf('.');
                                        if (dot > 0)
                                        {
                                            string bName = slotName.Substring(0, dot);
                                            char comp = slotName[dot + 1];

                                            if (comp == 'q' && defaultV4.TryGetValue(dofId, out var rot))
                                                dofRotations[bName] = rot;
                                            else if (comp == 't' && defaultV3.TryGetValue(dofId, out var trans))
                                                dofTranslations[bName] = trans;
                                            else if (comp == 's' && defaultV3.TryGetValue(dofId, out var scl))
                                                dofScales[bName] = scl;
                                        }
                                    }
                                }
                                slotCount++;
                            }
                            dofIdx += slotCount;
                        }
                    }
                }
            }

            var boneNames = new List<string>();
            var boneParents = new List<int>();
            var localTransforms = new List<Transform>();
            var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            EbxAssetEntry skelEntry = !string.IsNullOrEmpty(originalSkelPath) ? App.AssetManager.GetEbxEntry(originalSkelPath) : null;
            if (skelEntry != null)
            {
                var skelEbx = App.AssetManager.GetEbx(skelEntry);
                dynamic skelObj = skelEbx.RootObject;

                for (int i = 0; i < skelObj.BoneNames.Count; i++)
                {
                    string bName = skelObj.BoneNames[i].ToString();
                    boneNames.Add(bName);
                    boneParents.Add((int)skelObj.Hierarchy[i]);

                    dynamic lt = skelObj.LocalPose[i];
                    Vector3 defaultTrans = new Vector3((float)lt.trans.x, (float)lt.trans.y, (float)lt.trans.z);

                    Matrix4x4 m = new Matrix4x4(
                        (float)lt.right.x, (float)lt.right.y, (float)lt.right.z, 0,
                        (float)lt.up.x, (float)lt.up.y, (float)lt.up.z, 0,
                        (float)lt.forward.x, (float)lt.forward.y, (float)lt.forward.z, 0,
                        0, 0, 0, 1
                    );
                    Quaternion defaultRot = Quaternion.CreateFromRotationMatrix(m);
                    localTransforms.Add(new Transform(defaultTrans, defaultRot, Vector3.One));

                    nameToIndex[bName] = i;
                }
            }

            var antBoneNames = new List<string>();
            var antParents = new List<int>();
            var antLocalTransforms = new List<Transform>();

            AntAsset skelAsset = AntRefTable.Get(rig.Skeleton);
            if (skelAsset != null)
            {
                var jointsObj = skelAsset.GetProperty<object>("Joints");
                if (jointsObj is IEnumerable rawJoints)
                {
                    foreach (var jItem in rawJoints)
                    {
                        string jName = "unknown";
                        int jParent = -1;

                        if (jItem is Dictionary<string, object> dict)
                        {
                            if (dict.TryGetValue("JointName", out object n)) jName = n.ToString();
                            if (dict.TryGetValue("ParentIndex", out object p)) jParent = Convert.ToInt32(p);
                        }
                        else if (jItem is AntAsset antItem)
                        {
                            jName = antItem.Name;
                            jParent = antItem.GetProperty<int>("ParentIndex", -1);
                        }

                        antBoneNames.Add(jName);
                        antParents.Add(jParent);

                        Quaternion rot = dofRotations.TryGetValue(jName, out var qVal) ? qVal : Quaternion.Identity;
                        Vector3 trans = dofTranslations.TryGetValue(jName, out var tVal) ? tVal : Vector3.Zero;
                        Vector3 scale = dofScales.TryGetValue(jName, out var sVal) ? sVal : Vector3.One;

                        antLocalTransforms.Add(new Transform(trans, rot, scale));
                    }
                }
            }

            for (int i = 0; i < boneNames.Count; i++)
            {
                string bName = boneNames[i];
                Transform currentXform = localTransforms[i];

                Quaternion rot = dofRotations.TryGetValue(bName, out var qVal) ? qVal : currentXform.Rotation;
                Vector3 trans = dofTranslations.TryGetValue(bName, out var tVal) ? tVal : currentXform.Position;
                Vector3 scale = dofScales.TryGetValue(bName, out var sVal) ? sVal : currentXform.Scale;

                localTransforms[i] = new Transform(trans, rot, scale);
            }

            for (int i = 0; i < antBoneNames.Count; i++)
            {
                string antName = antBoneNames[i];

                if (!nameToIndex.ContainsKey(antName))
                {
                    int originalAntParentIdx = antParents[i];
                    int newParentIdx = -1;

                    if (originalAntParentIdx >= 0 && originalAntParentIdx < antBoneNames.Count)
                    {
                        string parentName = antBoneNames[originalAntParentIdx];
                        if (nameToIndex.TryGetValue(parentName, out int resolvedParentIdx))
                        {
                            newParentIdx = resolvedParentIdx;
                        }
                    }

                    int newBoneIdx = boneNames.Count;
                    boneNames.Add(antName);
                    boneParents.Add(newParentIdx);
                    localTransforms.Add(antLocalTransforms[i]);

                    nameToIndex[antName] = newBoneIdx;
                }
            }

            int numJoints = boneNames.Count;
            if (numJoints == 0) return null;

            var modelTransforms = new List<Transform>(numJoints);
            var worldMatrices = new Matrix4x4[numJoints];

            for (int i = 0; i < numJoints; i++)
            {
                int pIdx = boneParents[i];
                Transform xform = localTransforms[i];

                Matrix4x4 localMat = Matrix4x4.CreateScale(xform.Scale) *
                                     Matrix4x4.CreateFromQuaternion(xform.Rotation) *
                                     Matrix4x4.CreateTranslation(xform.Position);

                Matrix4x4 worldMat;
                if (pIdx >= 0 && pIdx < i)
                    worldMat = localMat * worldMatrices[pIdx];
                else
                    worldMat = localMat;

                worldMatrices[i] = worldMat;
                modelTransforms.Add(new Transform(worldMat));
            }

            return new InternalSkeleton(rig.Name, boneNames, boneParents, modelTransforms, localTransforms);
        }

        public static dynamic CreateEbxSkeletonFromInternal(InternalSkeleton internalSkel)
        {
            dynamic skelObj = TypeLibrary.CreateObject("SkeletonAsset");
            skelObj.Name = internalSkel.Name;

            skelObj.BoneNames.Clear();
            skelObj.Hierarchy.Clear();
            skelObj.LocalPose.Clear();
            skelObj.ModelPose.Clear();

            Type hierarchyElemType = skelObj.Hierarchy.GetType().GetGenericArguments()[0];

            for (int i = 0; i < internalSkel.BoneNames.Count; i++)
            {
                skelObj.BoneNames.Add(new CString(internalSkel.BoneNames[i]));

                int parentIdx = internalSkel.BoneParents[i];
                if (hierarchyElemType == typeof(short))
                    skelObj.Hierarchy.Add((short)parentIdx);
                else
                    skelObj.Hierarchy.Add(parentIdx);

                Transform localXform = internalSkel.LocalTransforms[i];
                skelObj.LocalPose.Add(ToLinearTransform(localXform));

                Transform modelXform = internalSkel.BoneTransforms[i];
                skelObj.ModelPose.Add(ToLinearTransform(modelXform));
            }

            return skelObj;
        }

        private static dynamic ToLinearTransform(Transform xform)
        {
            dynamic lt = TypeLibrary.CreateObject("LinearTransform");
            Matrix4x4 m = xform.Matrix;

            lt.right = TypeLibrary.CreateObject("Vec3");
            lt.right.x = m.M11; lt.right.y = m.M12; lt.right.z = m.M13;

            lt.up = TypeLibrary.CreateObject("Vec3");
            lt.up.x = m.M21; lt.up.y = m.M22; lt.up.z = m.M23;

            lt.forward = TypeLibrary.CreateObject("Vec3");
            lt.forward.x = m.M31; lt.forward.y = m.M32; lt.forward.z = m.M33;

            lt.trans = TypeLibrary.CreateObject("Vec3");
            lt.trans.x = xform.Position.X;
            lt.trans.y = xform.Position.Y;
            lt.trans.z = xform.Position.Z;

            return lt;
        }
    }

    public static class VirtualSkeletonManager
    {
        public static string RegisterVirtualSkeleton(dynamic dynamicSkelObj)
        {
            string virtualName = "ant_virtual_skeleton_" + Guid.NewGuid().ToString("N");
            Guid virtualGuid = Guid.NewGuid();

            EbxAsset virtualAsset = (EbxAsset)Activator.CreateInstance(typeof(EbxAsset), true);

            FieldInfo objectsField = typeof(EbxAsset).GetField("objects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                                  ?? typeof(EbxAsset).GetField("m_objects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);

            if (objectsField != null)
            {
                var list = objectsField.GetValue(virtualAsset) as IList;
                if (list == null)
                {
                    list = new List<object>();
                    objectsField.SetValue(virtualAsset, list);
                }
                list.Clear();
                list.Add(dynamicSkelObj);
            }

            SetMember(virtualAsset, "RootObject", dynamicSkelObj);

            EbxAssetEntry entry = (EbxAssetEntry)Activator.CreateInstance(typeof(EbxAssetEntry), true);
            SetMember(entry, "Name", virtualName);
            SetMember(entry, "Filename", virtualName);
            SetMember(entry, "Guid", virtualGuid);
            SetMember(entry, "Type", "SkeletonAsset");

            var modEntry = new ModifiedAssetEntry();
            modEntry.DataObject = virtualAsset;
            entry.ModifiedEntry = modEntry;

            var am = App.AssetManager;
            Type amType = am.GetType();

            FieldInfo ebxListField = amType.GetField("m_ebxList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (ebxListField != null && ebxListField.GetValue(am) is IDictionary ebxDict)
            {
                ebxDict[virtualName.ToLower()] = entry;
            }

            FieldInfo ebxGuidListField = amType.GetField("m_ebxGuidList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (ebxGuidListField != null && ebxGuidListField.GetValue(am) is IDictionary guidDict)
            {
                guidDict[virtualGuid] = entry;
            }

            return virtualName;
        }

        public static void UnregisterVirtualSkeleton(string virtualName)
        {
            var am = App.AssetManager;
            Type amType = am.GetType();

            FieldInfo ebxListField = amType.GetField("m_ebxList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (ebxListField != null && ebxListField.GetValue(am) is IDictionary ebxDict)
            {
                if (ebxDict.Contains(virtualName.ToLower()))
                {
                    ebxDict.Remove(virtualName.ToLower());
                }
            }
        }

        private static void SetMember(object obj, string name, object val)
        {
            Type t = obj.GetType();
            PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (p != null && p.CanWrite)
            {
                p.SetValue(obj, val);
                return;
            }
            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (f != null)
            {
                f.SetValue(obj, val);
            }
        }
    }
}