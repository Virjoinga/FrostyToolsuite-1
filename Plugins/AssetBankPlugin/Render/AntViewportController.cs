using AssetBankPlugin.Ant;
using AssetBankPlugin.Export;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Render;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using MeshSetPlugin.Render;
using MeshSetPlugin.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AssetBankPlugin
{
    public partial class AntStateAssetEditor
    {
        private async Task LoadPreviewAsync(AnimationAsset animAsset, string displayName = null)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (!string.IsNullOrEmpty(displayName))
                        animAsset.Name = displayName;

                    animAsset.Channels = animAsset.GetChannels(animAsset.ChannelToDofAsset);
                    var internalAnim = animAsset.ConvertToInternal();
                    if (internalAnim == null)
                    {
                        App.Logger.LogError($"[AntStateEditor] ConvertToInternal returned null for '{animAsset.Name}'. Check that reference banks are loaded.");
                        return;
                    }

                    InternalSkeleton internalSkel = null;

                    if (!_skeletonOverrideActive)
                    {
                        try
                        {
                            EbxAssetEntry rigEntry = null;
                            var dof = animAsset.ChannelToDofAsset;

                            if (dof is Guid g && g != Guid.Empty)
                                rigEntry = App.AssetManager.GetEbxEntry(g);
                            if (rigEntry == null)
                                rigEntry = TryResolveEbxRef(dof);

                            if (rigEntry != null)
                            {
                                App.Logger.Log($"[AntStateEditor] Rig asset: {rigEntry.Name}");
                                var rigEbx = App.AssetManager.GetEbx(rigEntry);
                                dynamic rig = rigEbx.RootObject;

                                try
                                {
                                    internalSkel = SkeletonAssetExport.ConvertToInternal(rig);
                                    if (internalSkel?.BoneNames?.Count == 0) internalSkel = null;
                                }
                                catch { internalSkel = null; }

                                if (internalSkel == null)
                                {
                                    try
                                    {
                                        var skelE = TryResolveEbxRef((object)rig.Skeleton);
                                        if (skelE != null)
                                            internalSkel = SkeletonAssetExport.ConvertToInternal(App.AssetManager.GetEbx(skelE).RootObject);
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch (Exception ex) { App.Logger.LogWarning($"[AntStateEditor] Rig skeleton failed: {ex.Message}"); }
                    }

                    if (internalSkel == null)
                    {
                        string skelPath = _skeletonOverrideActive ? _currentSkeletonPath?.Trim() : "";
                        if (string.IsNullOrEmpty(skelPath))
                        {
                            var opt = new AnimationOptions();
                            opt.Load();
                            skelPath = opt.ExportSkeletonAsset?.Trim() ?? "";
                        }
                        App.Logger.Log($"[AntStateEditor] Fallback skeleton: '{skelPath}' (override={_skeletonOverrideActive})");
                        if (string.IsNullOrEmpty(skelPath)) { App.Logger.LogWarning("[AntStateEditor] No skeleton available."); return; }
                        var skelEntry = App.AssetManager.GetEbxEntry(skelPath);
                        if (skelEntry == null) { App.Logger.LogError($"[AntStateEditor] Skeleton not found: {skelPath}"); return; }
                        internalSkel = SkeletonAssetExport.ConvertToInternal(App.AssetManager.GetEbx(skelEntry).RootObject);
                    }

                    var renderSkel = AntSkeletonConverter.Convert(internalSkel);

                    int lastKeyframe = internalAnim.Frames.Count > 0
                        ? internalAnim.Frames[internalAnim.Frames.Count - 1].FrameIndex + 1
                        : 1;
                    int endFrame = Math.Max(animAsset.EndFrame + 1, lastKeyframe);

                    var renderAnim = AntAnimationConverter.Convert(internalAnim, endFrame);

                    Dispatcher.Invoke(() =>
                    {
                        m_screen.LoadSkeleton(renderSkel);
                        m_screen.LoadAnimation(renderAnim, internalAnim, endFrame);

                        UpdateMeshSkeletons();

                        if (m_playPauseBtn != null) m_playPauseBtn.IsChecked = true;
                        if (m_timelineSlider != null) { m_timelineSlider.Maximum = endFrame; m_timelineSlider.Value = 0; }
                        if (m_totalFramesLabel != null) m_totalFramesLabel.Text = endFrame.ToString();
                        if (m_frameLabel != null) m_frameLabel.Text = "0";
                    });
                });

            }
            catch (Exception ex)
            {
                App.Logger.LogError($"[AntStateEditor] Preview failed for '{animAsset.Name}': {ex.Message}");
            }
        }

        private CancellationTokenSource _searchCancellationTokenSource;
        private async Task LoadMeshAsync(string meshEbxPath)
        {
            if (string.IsNullOrEmpty(meshEbxPath)) return;

            // Initialize the variation database

            if (ProfilesLibrary.DataVersion != (int)ProfileVersion.PlantsVsZombiesBattleforNeighborville)
            {
                if (!MeshVariationDb.IsLoaded)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        FrostyTaskWindow.Show("Loading Variations", "", MeshVariationDb.LoadVariations);
                    });
                }
                MeshVariationDb.LoadModifiedVariations();
            }

            var entry = App.AssetManager.GetEbxEntry(meshEbxPath);
            if (entry == null)
            {
                App.Logger.LogError($"[AntStateEditor] Asset not found: {meshEbxPath}");
                return;
            }

            try
            {
                var ebx = App.AssetManager.GetEbx(entry);
                string type = ebx.RootObject.GetType().Name;

                if (type == "VisualCustomizationSetAsset")
                    await LoadSkinAsync(ebx);
                else if (type == "VisualCustomizationAsset")
                    await LoadVisualAssetAsync(ebx, clearFirst: true, replaceIndex: -1);
                else
                    await LoadSingleMeshAsync(ebx, entry, clearFirst: true);
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"[AntStateEditor] Load failed: {ex.Message}");
            }
        }

        private static Guid GetFileGuid(object refObj)
        {
            if (refObj == null) return Guid.Empty;
            try
            {
                dynamic d = refObj;
                try { return (Guid)d.External.FileGuid; } catch { }
                try { return (Guid)d.External.ExternalGuid; } catch { }
                try { return (Guid)d.ExternalGuid; } catch { }
            }
            catch { }
            return Guid.Empty;
        }

        private static Guid GetClassGuid(object refObj)
        {
            if (refObj == null) return Guid.Empty;
            try
            {
                dynamic d = refObj;
                try { return (Guid)d.External.ClassGuid; } catch { }
                try { return (Guid)d.Internal.GetInstanceGuid().ExportedGuid; } catch { }
            }
            catch { }
            return Guid.Empty;
        }

        private async Task LoadSingleMeshAsync(EbxAsset ebx, EbxAssetEntry entry, bool clearFirst, PointerRef variationRef = default(PointerRef), string bpbName = null, int replaceIndex = -1)
        {
            await Task.Run(() =>
            {
                dynamic root = ebx.RootObject;
                ulong resRid = root.MeshSetResource;
                var resEntry = App.AssetManager.GetResEntry(resRid);
                var meshSet = App.AssetManager.GetResAs<MeshSet>(resEntry);

                var materials = new MeshMaterialCollection(ebx, (bpbName != null && variationRef.Type != PointerRefType.Null) ? new PointerRef() : variationRef);

                if (!string.IsNullOrEmpty(bpbName) && variationRef.Type != PointerRefType.Null)
                {
                    try
                    {
                        string dbName = bpbName + "/MeshVariationDb_Win32";
                        var dbEntry = App.AssetManager.GetEbxEntry(dbName);
                        if (dbEntry != null)
                        {
                            var dbEbx = App.AssetManager.GetEbx(dbEntry);
                            dynamic dbRoot = dbEbx.RootObject;
                            foreach (dynamic dbEntryObj in dbRoot.Entries)
                            {
                                object meshRefObj = dbEntryObj.Mesh;
                                if (GetFileGuid(meshRefObj) == entry.Guid)
                                {
                                    foreach (dynamic dbMat in dbEntryObj.Materials)
                                    {
                                        object materialRefObj = dbMat.Material;
                                        object matVarRefObj = dbMat.MaterialVariation;

                                        if (matVarRefObj != null)
                                        {
                                            Guid matVarFileGuid = GetFileGuid(matVarRefObj);
                                            Guid matVarClassGuid = GetClassGuid(matVarRefObj);
                                            Guid matClassGuid = GetClassGuid(materialRefObj);

                                            if (matVarFileGuid != Guid.Empty && matVarClassGuid != Guid.Empty && matClassGuid != Guid.Empty)
                                            {
                                                MeshMaterial targetMat = null;
                                                for (int m = 0; m < materials.Count; m++)
                                                {
                                                    if (materials[m].Guid == matClassGuid)
                                                    {
                                                        targetMat = materials[m];
                                                        break;
                                                    }
                                                }

                                                if (targetMat != null)
                                                {
                                                    var varAssetEntry = App.AssetManager.GetEbxEntry(matVarFileGuid);
                                                    if (varAssetEntry != null)
                                                    {
                                                        var varAsset = App.AssetManager.GetEbx(varAssetEntry);
                                                        dynamic matchingVariationObj = null;
                                                        foreach (dynamic obj in varAsset.Objects)
                                                        {
                                                            AssetClassGuid objGuid = obj.GetInstanceGuid();
                                                            if (objGuid.ExportedGuid == matVarClassGuid)
                                                            {
                                                                matchingVariationObj = obj;
                                                                break;
                                                            }
                                                        }

                                                        if (matchingVariationObj != null)
                                                        {
                                                            object shaderRefObj = matchingVariationObj.Shader.Shader;
                                                            if (shaderRefObj != null && GetFileGuid(shaderRefObj) != Guid.Empty)
                                                            {
                                                                targetMat.Shader = (dynamic)shaderRefObj;
                                                                targetMat.TextureParameters.Clear();
                                                                targetMat.VectorParameters.Clear();
                                                                targetMat.BoolParameters.Clear();
                                                                targetMat.ConditionalParameters.Clear();
                                                            }

                                                            foreach (dynamic param in matchingVariationObj.Shader.BoolParameters)
                                                            {
                                                                int idx = targetMat.BoolParameters.FindIndex((dynamic a) => a.ParameterName.Equals(param.ParameterName, StringComparison.OrdinalIgnoreCase));
                                                                if (idx != -1) targetMat.BoolParameters[idx] = param;
                                                                else targetMat.BoolParameters.Add(param);
                                                            }
                                                            foreach (dynamic param in matchingVariationObj.Shader.VectorParameters)
                                                            {
                                                                int idx = targetMat.VectorParameters.FindIndex((dynamic a) => a.ParameterName.Equals(param.ParameterName, StringComparison.OrdinalIgnoreCase));
                                                                if (idx != -1) targetMat.VectorParameters[idx] = param;
                                                                else targetMat.VectorParameters.Add(param);
                                                            }
                                                            foreach (dynamic param in matchingVariationObj.Shader.TextureParameters)
                                                            {
                                                                int idx = targetMat.TextureParameters.FindIndex((dynamic a) => a.ParameterName.Equals(param.ParameterName, StringComparison.OrdinalIgnoreCase));
                                                                if (idx != -1) targetMat.TextureParameters[idx] = param;
                                                                else targetMat.TextureParameters.Add(param);
                                                            }
                                                            try
                                                            {
                                                                foreach (dynamic param in matchingVariationObj.Shader.ConditionalParameters)
                                                                {
                                                                    int idx = targetMat.ConditionalParameters.FindIndex((dynamic a) => a.ConditionalAsset.Equals(param.ConditionalAsset));
                                                                    if (idx != -1) targetMat.ConditionalParameters[idx] = param;
                                                                    else targetMat.ConditionalParameters.Add(param);
                                                                }
                                                            }
                                                            catch { }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.LogWarning($"[AntStateEditor] Companion MeshVariationDatabase manual mapping failed: {ex.Message}");
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    var perMeshSkel = BuildPerMeshSkeleton(root);
                    var newEntry = new LoadedMeshEntry
                    {
                        DisplayName = entry.Filename,
                        MeshSet = meshSet,
                        Materials = materials,
                        PerMeshSkeleton = perMeshSkel,
                        SourceEntry = entry,
                        VariationRef = variationRef,
                        BpbName = bpbName
                    };

                    if (replaceIndex >= 0 && replaceIndex < _loadedMeshData.Count)
                    {
                        _loadedMeshData[replaceIndex] = newEntry;
                        LoadedMeshes[replaceIndex].Name = entry.Filename;
                        OnMeshVisibilityChanged();
                    }
                    else
                    {
                        if (clearFirst && _meshLoaded)
                        {
                            m_screen.ClearMeshes(clearAll: true);
                            _loadedMeshData.Clear();
                            LoadedMeshes.Clear();
                        }
                        int idx = _loadedMeshData.Count;
                        _loadedMeshData.Add(newEntry);
                        LoadedMeshes.Add(new LoadedMeshViewModel(this) { InternalIndex = idx, Name = entry.Filename });
                        m_screen.AddMesh(meshSet, materials, Matrix4x4.Identity, perMeshSkel);
                        _meshLoaded = true;
                        m_screen.RefreshPose();
                    }
                });
            });
        }

        private MeshRenderSkeleton BuildPerMeshSkeleton(dynamic meshRoot)
        {
            if (m_screen.CurrentSkeleton == null) return null;

            bool hasSpa = false;
            try
            {
                int count = 0;
                foreach (var _ in (System.Collections.IEnumerable)meshRoot.SkinnedProceduralAnimation.Bones)
                    count++;
                hasSpa = count > 0;
            }
            catch { }

            if (!hasSpa) return m_screen.CurrentSkeleton;

            var clone = new MeshRenderSkeleton();
            foreach (var b in m_screen.CurrentSkeleton.Bones)
                clone.AddBone(new MeshRenderSkeleton.Bone
                {
                    NameHash = b.NameHash,
                    ParentBoneId = b.ParentBoneId,
                    ModelPose = b.ModelPose,
                    LocalPose = b.LocalPose,
                    IsProcedural = b.IsProcedural
                });

            AppendSpaBones(clone, meshRoot);
            return clone;
        }

        private static void AppendSpaBones(MeshRenderSkeleton skeleton, dynamic meshRoot)
        {
            try
            {
                int procIndex = 0;
                foreach (dynamic bone in (System.Collections.IEnumerable)meshRoot.SkinnedProceduralAnimation.Bones)
                {
                    try
                    {
                        int parentIdx = (int)bone.ParentIndex;

                        Matrix4x4 localPose = new Matrix4x4(
                            bone.Pose.right.x, bone.Pose.right.y, bone.Pose.right.z, 0f,
                            bone.Pose.up.x, bone.Pose.up.y, bone.Pose.up.z, 0f,
                            bone.Pose.forward.x, bone.Pose.forward.y, bone.Pose.forward.z, 0f,
                            bone.Pose.trans.x, bone.Pose.trans.y, bone.Pose.trans.z, 1f
                        );

                        Matrix4x4.Invert(localPose, out Matrix4x4 invLocal);
                        Matrix4x4 modelPose = skeleton.GetBone(parentIdx).ModelPose * invLocal;

                        skeleton.AddBone(new MeshRenderSkeleton.Bone
                        {
                            NameHash = (int)(0x80000000u + (uint)procIndex),
                            ParentBoneId = parentIdx,
                            ModelPose = modelPose,
                            LocalPose = localPose,
                            IsProcedural = true
                        });
                        procIndex++;
                    }
                    catch { }
                }
            }
            catch { }
        }

        private async Task LoadSkinAsync(EbxAsset skinEbx)
        {
            Dispatcher.Invoke(() => { if (_meshLoaded) m_screen.ClearMeshes(clearAll: true); _meshLoaded = false; _loadedMeshData.Clear(); LoadedMeshes.Clear(); });

            dynamic root = skinEbx.RootObject;
            int loaded = 0;

            try
            {
                var visualCustomizations = root.VisualCustomizations;

                foreach (dynamic group in (System.Collections.IEnumerable)visualCustomizations)
                {
                    try
                    {
                        var selectables = group.Selectables;
                        foreach (dynamic sel in (System.Collections.IEnumerable)selectables)
                        {
                            try
                            {
                                object rawRef = sel.Selectable;

                                var visualEntry = TryResolveEbxRef(rawRef);
                                if (visualEntry == null) continue;

                                var visualEbx = App.AssetManager.GetEbx(visualEntry);
                                var meshData = FindMeshOnVisualAsset(visualEbx);
                                if (meshData == null) continue;

                                var meshEntry = meshData.Item1;
                                var variationRef = meshData.Item2;
                                var bpbName = meshData.Item3;

                                var meshEbx = App.AssetManager.GetEbx(meshEntry);
                                await LoadSingleMeshAsync(meshEbx, meshEntry, clearFirst: false, variationRef: variationRef, bpbName: bpbName);
                                loaded++;
                            }
                            catch (Exception ex)
                            {
                                App.Logger.LogWarning($"[AntStateEditor][Skin] Part failed: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.LogWarning($"[AntStateEditor][Skin] Group failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"[AntStateEditor][Skin] Iteration failed: {ex.Message}");
            }

            App.Logger.Log($"[AntStateEditor] Skin loaded: {loaded} mesh part(s).");
        }

        private static EbxAssetEntry TryResolveEbxRef(object refObj)
        {
            if (refObj == null) return null;
            try
            {
                dynamic d = refObj;
                try { return App.AssetManager.GetEbxEntry((Guid)d.External.FileGuid); } catch { }
                try { return App.AssetManager.GetEbxEntry((Guid)d.External.ExternalGuid); } catch { }
                try { return App.AssetManager.GetEbxEntry((Guid)d.ExternalGuid); } catch { }
            }
            catch { }
            return null;
        }

        private static Tuple<EbxAssetEntry, PointerRef, string> FindMeshOnVisualAsset(EbxAsset visualEbx)
        {
            dynamic root = visualEbx.RootObject;

            string bpbName = null;
            try { bpbName = (string)root.BlueprintBundleReference.Name; } catch { }
            if (string.IsNullOrEmpty(bpbName))
                try { bpbName = (string)root.BlueprintBundleReference.BlueprintBundleReference.Name; } catch { }

            if (string.IsNullOrEmpty(bpbName)) return null;

            var bpbEntry = App.AssetManager.GetEbxEntry(bpbName);
            if (bpbEntry == null)
            {
                App.Logger.Log("[AntStateEditor][Skin] BPB not found");
                return null;
            }

            var bpbEbx = App.AssetManager.GetEbx(bpbEntry);

            try
            {
                foreach (dynamic obj in bpbEbx.Objects)
                {
                    if (obj.GetType().Name == "VisualCustomizationAsset")
                    {
                        foreach (dynamic visual in (System.Collections.IEnumerable)obj.Visuals)
                        {
                            try
                            {
                                dynamic outObjRoot = null;
                                object outObjRef = visual.OutObjectBlueprint;

                                if (outObjRef is PointerRef)
                                {
                                    PointerRef pRef = (PointerRef)outObjRef;
                                    if (pRef.Type == PointerRefType.Internal)
                                    {
                                        outObjRoot = pRef.Internal;
                                    }
                                    else if (pRef.Type == PointerRefType.External)
                                    {
                                        var outObjEntry = TryResolveEbxRef(outObjRef);
                                        if (outObjEntry != null)
                                        {
                                            outObjRoot = App.AssetManager.GetEbx(outObjEntry).RootObject;
                                        }
                                    }
                                }

                                if (outObjRoot != null)
                                {
                                    PointerRef variationRef = new PointerRef();
                                    try
                                    {
                                        if (outObjRoot.Variation != null && outObjRoot.Variation is PointerRef)
                                        {
                                            variationRef = (PointerRef)outObjRoot.Variation;
                                        }
                                    }
                                    catch { }

                                    object meshAssetObj = null;
                                    try { meshAssetObj = outObjRoot.MeshAsset; } catch { }

                                    if (meshAssetObj != null)
                                    {
                                        var meshEntry = TryResolveEbxRef(meshAssetObj);
                                        if (meshEntry != null)
                                        {
                                            return Tuple.Create(meshEntry, variationRef, bpbName);
                                        }
                                    }

                                    object rawEntity = null;
                                    try { rawEntity = outObjRoot.Object; } catch { }

                                    if (rawEntity != null)
                                    {
                                        dynamic entityData = rawEntity;
                                        if (rawEntity is PointerRef)
                                        {
                                            PointerRef erRef = (PointerRef)rawEntity;
                                            if (erRef.Type == PointerRefType.Internal)
                                            {
                                                entityData = erRef.Internal;
                                            }
                                        }

                                        if (entityData != null)
                                        {
                                            var meshEntry = TryResolveEbxRef((object)entityData.Mesh);
                                            if (meshEntry != null)
                                            {
                                                return Tuple.Create(meshEntry, variationRef, bpbName);
                                            }
                                        }
                                    }

                                    var fallbackMeshEntry = TryResolveEbxRef((object)outObjRoot.Mesh);
                                    if (fallbackMeshEntry != null)
                                    {
                                        return Tuple.Create(fallbackMeshEntry, variationRef, bpbName);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Logger.LogWarning($"[AntStateEditor][Skin] Visual parsing failed: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogWarning($"[AntStateEditor][Skin] BPB object scan failed: {ex.Message}");
            }

            try
            {
                foreach (Guid depGuid in bpbEbx.Dependencies)
                {
                    var dep = App.AssetManager.GetEbxEntry(depGuid);
                    if (dep == null) continue;

                    try
                    {
                        var depEbx = App.AssetManager.GetEbx(dep);
                        dynamic dRoot = depEbx.RootObject;

                        PointerRef variationRef = new PointerRef();
                        try
                        {
                            if (dRoot.Variation != null && dRoot.Variation is PointerRef)
                            {
                                variationRef = (PointerRef)dRoot.Variation;
                            }
                        }
                        catch { }

                        object meshAssetObj = null;
                        try { meshAssetObj = dRoot.MeshAsset; } catch { }

                        if (meshAssetObj != null)
                        {
                            var meshEntry = TryResolveEbxRef(meshAssetObj);
                            if (meshEntry != null) return Tuple.Create(meshEntry, variationRef, bpbName);
                        }

                        object rawEntity = null;
                        try { rawEntity = dRoot.Object; } catch { }

                        if (rawEntity != null)
                        {
                            dynamic entityData = rawEntity;
                            if (rawEntity is PointerRef)
                            {
                                PointerRef pRef = (PointerRef)rawEntity;
                                if (pRef.Type == PointerRefType.Internal)
                                {
                                    entityData = pRef.Internal;
                                }
                            }

                            if (entityData != null)
                            {
                                var meshEntry = TryResolveEbxRef((object)entityData.Mesh);
                                if (meshEntry != null) return Tuple.Create(meshEntry, variationRef, bpbName);
                            }
                        }

                        var fallbackEntry = TryResolveEbxRef((object)dRoot.Mesh);
                        if (fallbackEntry != null) return Tuple.Create(fallbackEntry, variationRef, bpbName);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogWarning($"[AntStateEditor][Skin] Dependency scan failed: {ex.Message}");
            }

            return null;
        }
        private static Dictionary<Guid, ulong> GetOriginalHashes(ResAssetEntry resEntry, ChunkAssetEntry chunkEntry, int bundleId)
        {
            byte[] origBytes = null;

            if (resEntry != null)
            {
                var mod = resEntry.ModifiedEntry;
                resEntry.ModifiedEntry = null;
                using (var origStream = App.AssetManager.GetRes(resEntry))
                {
                    if (origStream != null)
                    {
                        using (var ms = new MemoryStream()) { origStream.CopyTo(ms); origBytes = ms.ToArray(); }
                    }
                }
                resEntry.ModifiedEntry = mod;
            }
            else if (chunkEntry != null)
            {
                var mod = chunkEntry.ModifiedEntry;
                chunkEntry.ModifiedEntry = null;
                using (var origStream = App.AssetManager.GetChunk(chunkEntry))
                {
                    if (origStream != null)
                    {
                        using (var ms = new MemoryStream()) { origStream.CopyTo(ms); origBytes = ms.ToArray(); }
                    }
                }
                chunkEntry.ModifiedEntry = mod;
            }

            if (origBytes == null) return null;

            Dictionary<Guid, ulong> hashes;
            using (var reader = new NativeReader(new MemoryStream(origBytes)))
            {
                var origBank = new Bank(reader, bundleId, isHashMode: true);
                hashes = origBank.OriginalHashes;
            }

            return hashes;
        }
        private async Task LoadAsync()
        {
            try
            {
                SetLoadingState(true, "Locating data stream...");

                dynamic antStateAsset = RootObject;
                Stream s = null;
                int bundleId = 0;

                ResAssetEntry resEntry = null;
                ChunkAssetEntry chunkEntry = null;

                if (antStateAsset.StreamingGuid == Guid.Empty)
                {
                    resEntry = App.AssetManager.GetResEntry(AssetEntry.Name);
                    if (resEntry != null) { bundleId = resEntry.Bundles[0]; s = App.AssetManager.GetRes(resEntry); }
                }
                else
                {
                    chunkEntry = App.AssetManager.GetChunkEntry(antStateAsset.StreamingGuid);
                    if (chunkEntry != null) { bundleId = chunkEntry.Bundles[0]; s = App.AssetManager.GetChunk(chunkEntry); }
                }

                if (s == null)
                {
                    SetLoadingState(false, "");
                    logger.LogError("Failed to locate AntState stream.");
                    return;
                }

                SetLoadingState(true, $"Parsing {s.Length} bytes...");

                byte[] rawBytes;
                using (var ms = new MemoryStream())
                { s.CopyTo(ms); rawBytes = ms.ToArray(); }
                _bankBytes = rawBytes;
                _bankResEntry = resEntry;
                _bankChunkEntry = chunkEntry;
                _bankIsBigEndian = DetectBigEndian(rawBytes);

                Dictionary<Guid, ulong> originalHashes = null;

                await Task.Run(() =>
                {
                    bool isModified = (_bankResEntry?.HasModifiedData ?? false) || (_bankChunkEntry?.HasModifiedData ?? false);
                    if (isModified)
                    {
                        SetLoadingState(true, "Comparing against original bank...");

                        originalHashes = GetOriginalHashes(_bankResEntry, _bankChunkEntry, bundleId);

                        GC.Collect(2, GCCollectionMode.Forced, true, true);
                        GC.WaitForPendingFinalizers();
                    }

                    SetLoadingState(true, isModified ? "Parsing modified bank..." : "Parsing bank data...");
                    using (var reader = new NativeReader(new MemoryStream(_bankBytes)))
                    {
                        _bank = new Bank(reader, bundleId);
                    }
                });

                SetLoadingState(true, "Building asset hierarchy...");

                _masterGroups = await Task.Run(() =>
                {
                    var animToController = BuildAnimToControllerMap();
                    var tempGroups = new Dictionary<string, List<AntAssetViewModel>>();

                    foreach (var kvp in _bank.DataNames)
                    {
                        var antAsset = AntRefTable.Get(kvp.Value);
                        string typeName = antAsset != null ? antAsset.AssetType : "Unknown";

                        if (!tempGroups.TryGetValue(typeName, out var list))
                        {
                            list = new List<AntAssetViewModel>();
                            tempGroups[typeName] = list;
                        }

                        string displayName = kvp.Key;
                        if (antAsset is AnimationAsset && animToController.TryGetValue(kvp.Value, out var controller))
                            displayName = $"{controller.Name} ({kvp.Key})";

                        var vm = new AntAssetViewModel
                        {
                            ParentEditor = this,
                            Name = displayName,
                            Id = kvp.Value,
                            AssetInstance = antAsset
                        };

                        // Check modifications
                        if (originalHashes != null && antAsset != null)
                        {
                            if (originalHashes.TryGetValue(antAsset.ID, out ulong origHash))
                            {
                                ulong currentHash = Bank.ComputeAssetHash(antAsset);
                                if (currentHash != origHash)
                                {
                                    vm.IsModified = true;
                                    vm.IsUnsaved = false;
                                }
                            }
                            else
                            {
                                vm.IsNew = true;
                                vm.IsModified = true;
                                vm.IsUnsaved = false;
                            }
                        }

                        list.Add(vm);
                    }

                    return tempGroups
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => new AntTypeGroup
                        {
                            TypeName = kvp.Key,
                            Assets = new ObservableCollection<AntAssetViewModel>(kvp.Value.OrderBy(a => a.Name))
                        })
                        .ToList();
                });

                Dispatcher.Invoke(() => ApplySearchFilter(""));
                SetLoadingState(false, "");
            }
            catch (Exception ex)
            {
                SetLoadingState(false, "");
                logger.LogError($"[AntStateEditor] {ex.Message}");
            }
        }


        private void SetLoadingState(bool isVisible, string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (m_loadingOverlay != null) m_loadingOverlay.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                if (m_loadingText != null) m_loadingText.Text = text;
            });
        }

        private async void ApplySearchFilter(string searchText)
        {
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource.Token;

            searchText = searchText?.Trim() ?? "";
            bool showModified = m_filterMenu?.ShowModifiedOnly ?? false;
            bool showAnimsOnly = m_filterMenu?.ShowAnimationsOnly ?? false;
            bool sortAsc = m_filterMenu?.SortAscending ?? true;

            var masterGroupsSnapshot = _masterGroups.ToList();

            try
            {
                List<AntTypeGroup> newFilteredGroups = await Task.Run(() =>
                {
                    return ProcessFilterOnBackgroundThread(
                        masterGroupsSnapshot,
                        searchText,
                        showModified,
                        showAnimsOnly,
                        sortAsc,
                        cancellationToken);
                }, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;

                _filteredGroups.Clear();
                foreach (var group in newFilteredGroups)
                {
                    _filteredGroups.Add(group);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"[AntStateEditor] Search filter error: {ex.Message}");
            }
        }

        // Helper method running on the background thread
        private List<AntTypeGroup> ProcessFilterOnBackgroundThread(
            List<AntTypeGroup> masterGroups,
            string searchText,
            bool showModified,
            bool showAnimsOnly,
            bool sortAsc,
            CancellationToken token)
        {
            string typeQuery = null;
            string textQuery = searchText;

            if (searchText.Contains("@"))
            {
                var parts = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var typeToken = parts.FirstOrDefault(p => p.StartsWith("@"));
                if (typeToken != null)
                {
                    typeQuery = typeToken.Substring(1);
                    textQuery = string.Join(" ", parts.Where(p => p != typeToken));
                }
            }

            IEnumerable<AntTypeGroup> sortedGroups = sortAsc
                ? masterGroups.OrderBy(g => g.TypeName)
                : masterGroups.OrderByDescending(g => g.TypeName);

            if (!string.IsNullOrEmpty(typeQuery))
            {
                sortedGroups = sortedGroups.Where(g => g.TypeName.IndexOf(typeQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var resultGroups = new List<AntTypeGroup>();

            foreach (var group in sortedGroups)
            {
                // Check for cancellation sometimes during heavy loops
                if (token.IsCancellationRequested)
                    return new List<AntTypeGroup>();

                var matchedAssets = group.Assets.Where(vm =>
                {
                    if (showAnimsOnly && !(vm.AssetInstance is AnimationAsset))
                        return false;

                    if (showModified && !vm.IsModified && !vm.IsNew)
                        return false;

                    if (!string.IsNullOrEmpty(textQuery))
                    {
                        bool nameMatches = vm.Name.IndexOf(textQuery, StringComparison.OrdinalIgnoreCase) >= 0;
                        bool idMatches = vm.Id.ToString().IndexOf(textQuery, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!nameMatches && !idMatches) return false;
                    }

                    return true;
                });

                var sortedList = sortAsc
                    ? matchedAssets.OrderBy(a => a.Name).ToList()
                    : matchedAssets.OrderByDescending(a => a.Name).ToList();

                if (sortedList.Count > 0)
                {
                    resultGroups.Add(new AntTypeGroup
                    {
                        TypeName = group.TypeName,
                        Assets = new ObservableCollection<AntAssetViewModel>(sortedList)
                    });
                }
            }

            return resultGroups;
        }

        private void ToggleBulkMode(bool isBulk)
        {
            if (!isBulk) _shiftAnchor = null;

            foreach (var group in _masterGroups)
                foreach (var asset in group.Assets)
                    asset.SetBulkMode(isBulk);
        }

        private void OnTreeViewPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isBulkMode) return;

            var clicked = GetViewModelFromSource(e.OriginalSource as DependencyObject);
            if (clicked == null) return;

            bool shiftHeld = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (shiftHeld && _shiftAnchor != null)
            {
                SelectRange(_shiftAnchor, clicked);
                e.Handled = true;
            }
            else
            {
                _shiftAnchor = clicked;
            }
        }

        private AntAssetViewModel GetViewModelFromSource(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return (source as TreeViewItem)?.DataContext as AntAssetViewModel;
        }

        private void SelectRange(AntAssetViewModel anchor, AntAssetViewModel target)
        {
            var flat = _filteredGroups.SelectMany(g => g.Assets).ToList();

            int anchorIdx = flat.IndexOf(anchor);
            int targetIdx = flat.IndexOf(target);

            if (anchorIdx < 0 || targetIdx < 0) return;

            int start = Math.Min(anchorIdx, targetIdx);
            int end = Math.Max(anchorIdx, targetIdx);

            for (int i = start; i <= end; i++)
                flat[i].IsSelectedForExport = true;
        }

        private void ExportSelected()
        {
            List<AntAssetViewModel> assetsToExport = new List<AntAssetViewModel>();
            if (_isBulkMode)
            {
                assetsToExport = _masterGroups.SelectMany(g => g.Assets)
                                              .Where(a => a.IsSelectedForExport && a.AssetInstance is AnimationAsset)
                                              .ToList();
            }
            else if (_selectedAsset != null && _selectedAsset.AssetInstance is AnimationAsset)
            {
                assetsToExport.Add(_selectedAsset);
            }

            if (assetsToExport.Count == 0) return;

            var opt = new AnimationOptions();
            opt.Load();
            if (string.IsNullOrEmpty(opt.ExportSkeletonAsset))
            {
                FrostyMessageBox.Show("Please set an Export Skeleton in Options first.", "Missing Skeleton");
                return;
            }

            FrostySaveFileDialog sfd = new FrostySaveFileDialog("Select Export Directory", "*.cast (Cast)|*.cast|*.seanim (SEAnim)|*.seanim", "Cast", "FolderSelection");
            if (!sfd.ShowDialog()) return;
            string exportDirectory = Path.GetDirectoryName(sfd.FileName);
            string extension = Path.GetExtension(sfd.FileName).ToLower();

            FrostyTaskWindow.Show($"Exporting {assetsToExport.Count} Animations", "", (task) =>
            {
                InternalSkeleton skeleton = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var skelEntry = App.AssetManager.GetEbxEntry(opt.ExportSkeletonAsset);
                    var skelEbx = App.AssetManager.GetEbx(skelEntry);
                    skeleton = SkeletonAssetExport.ConvertToInternal(skelEbx.RootObject);
                });

                var animToController = BuildAnimToControllerMap();

                int progress = 0;
                foreach (var asset in assetsToExport)
                {
                    var anim = asset.AssetInstance as AnimationAsset;

                    animToController.TryGetValue(asset.Id, out var controller);
                    string finalFileName = (controller != null) ? controller.Name : asset.Name;

                    task.Update($"Exporting {finalFileName}...", ((double)progress / assetsToExport.Count) * 100.0);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            anim.Name = finalFileName;
                            anim.Channels = anim.GetChannels(anim.ChannelToDofAsset);
                            var intern = anim.ConvertToInternal();
                            if (intern != null)
                            {
                                if (extension == ".cast")
                                {
                                    new AnimationExporterCAST().Export(intern, skeleton, exportDirectory);
                                }
                                else
                                {
                                    new AnimationExporterSEANIM().Export(intern, skeleton, exportDirectory);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.LogError($"[AntStateEditor] Failed {finalFileName}: {ex.Message}");
                        }
                    });

                    progress++;
                }
                App.Logger.Log($"[AntStateEditor] Exported {assetsToExport.Count} assets.");
            });
        }

        public override List<ToolbarItem> RegisterToolbarItems()
        {
            return new List<ToolbarItem>
            {
                new ToolbarItem("Refresh", "Reload data", null, new RelayCommand((state) => _ = LoadAsync()))
            };
        }

        internal void OnMeshVisibilityChanged()
        {
            Dispatcher.Invoke(() =>
            {
                m_screen.ClearMeshes(clearAll: true);
                for (int i = 0; i < _loadedMeshData.Count; i++)
                {
                    if (i < LoadedMeshes.Count && LoadedMeshes[i].IsVisible)
                    {
                        var e = _loadedMeshData[i];
                        m_screen.AddMesh(e.MeshSet, e.Materials, Matrix4x4.Identity, e.PerMeshSkeleton);
                    }
                }
                _meshLoaded = _loadedMeshData.Count > 0;
                m_screen.RefreshPose();
            });
        }

        internal async void ReplaceMeshFromExplorer(int index)
        {
            try
            {
                dynamic mainWin = App.EditorWindow;
                EbxAssetEntry entry = mainWin?.DataExplorer?.SelectedAsset as EbxAssetEntry;
                if (entry == null) { App.Logger.LogWarning("[AntStateEditor] No asset selected in data explorer."); return; }

                var ebx = App.AssetManager.GetEbx(entry);
                string type = ebx.RootObject.GetType().Name;

                if (type == "VisualCustomizationSetAsset")
                    App.Logger.LogWarning("[AntStateEditor] Use 'Load Mesh' to swap a full skin - the arrow replaces a single slot.");
                else if (type == "VisualCustomizationAsset")
                    await LoadVisualAssetAsync(ebx, clearFirst: false, replaceIndex: index);
                else
                    await LoadSingleMeshAsync(ebx, entry, clearFirst: false, replaceIndex: index);
            }
            catch (Exception ex) { App.Logger.LogError($"[AntStateEditor] Replace mesh failed: {ex.Message}"); }
        }

        public ICommand AddMeshFromExplorerCommand => new RelayCommand(_ => AddMeshFromExplorer());

        internal async void AddMeshFromExplorer()
        {
            try
            {
                dynamic mainWin = App.EditorWindow;
                EbxAssetEntry entry = mainWin?.DataExplorer?.SelectedAsset as EbxAssetEntry;
                if (entry == null) { App.Logger.LogWarning("[AntStateEditor] No asset selected in data explorer."); return; }

                var ebx = App.AssetManager.GetEbx(entry);
                string type = ebx.RootObject.GetType().Name;

                if (type == "VisualCustomizationSetAsset")
                    App.Logger.LogWarning("[AntStateEditor] Use 'Load Mesh' to load a full skin - add appends a single mesh.");
                else if (type == "VisualCustomizationAsset")
                    await LoadVisualAssetAsync(ebx, clearFirst: false, replaceIndex: -1);
                else
                    await LoadSingleMeshAsync(ebx, entry, clearFirst: false, replaceIndex: -1);
            }
            catch (Exception ex) { App.Logger.LogError($"[AntStateEditor] Add mesh failed: {ex.Message}"); }
        }

        private async Task LoadVisualAssetAsync(EbxAsset visualEbx, bool clearFirst, int replaceIndex)
        {
            var meshData = FindMeshOnVisualAsset(visualEbx);
            if (meshData == null)
            {
                App.Logger.LogError("[AntStateEditor] Could not resolve mesh from VisualCustomizationAsset.");
                return;
            }
            var meshEntry = meshData.Item1;
            var variationRef = meshData.Item2;
            var bpbName = meshData.Item3;
            var meshEbx = App.AssetManager.GetEbx(meshEntry);
            await LoadSingleMeshAsync(meshEbx, meshEntry, clearFirst, variationRef: variationRef, bpbName: bpbName, replaceIndex: replaceIndex);
        }

        internal void RemoveMeshAt(int index)
        {
            if (index < 0 || index >= _loadedMeshData.Count) return;
            _loadedMeshData.RemoveAt(index);
            LoadedMeshes.RemoveAt(index);
            for (int i = index; i < LoadedMeshes.Count; i++)
                LoadedMeshes[i].InternalIndex = i;
            OnMeshVisibilityChanged();
        }
    }
}
