using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using Frosty.Hash;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using FrostySdk.Resources;
using MeshSetPlugin.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CustomizationCreatorPlugin.Windows
{
    public partial class AddCustomizationWindow : FrostyDockableWindow
    {
        private string mBlueprintDirectory = "";
        private string mCustomizationName = "";
        private string mTextureBaseName = "";
        private string mBaseCustomizationPath = "";

        public AddCustomizationWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void varBPDirTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            mBlueprintDirectory = varBPDirTextBox.Text.TrimEnd('/');
        }

        private void varWepNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            mCustomizationName = varWepNameTextBox.Text;
        }

        private void varTexNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            mTextureBaseName = varTexNameTextBox.Text;
        }

        private void varBaseCustomizationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private string VerifyFileName(string inFilename)
        {
            string filename = inFilename;
            if (filename.Contains("//")) filename = filename.Replace("//", "/");
            if (filename.Contains("\\")) filename = filename.Replace("\\", "/");
            return filename;
        }

        private void BrowseBlueprintDir_Click(object sender, RoutedEventArgs e)
        {
            var selector = new DirectorySelectorWindow { Owner = this };
            if (selector.ShowDialog() == true && !string.IsNullOrWhiteSpace(selector.SelectedPath))
            {
                varBPDirTextBox.Text = selector.SelectedPath;
                mBlueprintDirectory = selector.SelectedPath;
            }
        }

        private void BrowseBaseCustomization_Click(object sender, RoutedEventArgs e)
        {
            var selector = new CustomizationSelectorWindow { Owner = this };
            if (selector.ShowDialog() == true && !string.IsNullOrWhiteSpace(selector.SelectedAssetPath))
            {
                mBaseCustomizationPath = selector.SelectedAssetPath;
                varBaseCustomizationTextBox.Text = selector.SelectedAssetPath;
            }
        }

        private void createButton_Click(object sender, RoutedEventArgs e)
        {
            // Early Exit: Prevent generating over existing assets and crashing
            string objectBPName = $"{mBlueprintDirectory}/{mCustomizationName}";
            VerifyFileName(objectBPName);

            if (App.AssetManager.GetEbxEntry(objectBPName) != null)
            {
                App.Logger.LogError($"Asset '{objectBPName}' already exists! Please choose a different name or directory.");
                return;
            }

            // 1. Base unlock
            EbxAssetEntry baseUnlock = null;
            if (!string.IsNullOrWhiteSpace(mBaseCustomizationPath))
            {
                baseUnlock = App.AssetManager.GetEbxEntry(mBaseCustomizationPath);
                if (baseUnlock == null)
                    App.Logger.LogWarning($"Base customization '{mBaseCustomizationPath}' not found. Falling back to BrownCoat default.");
            }

            if (baseUnlock == null)
            {
                if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville))
                    baseUnlock = App.AssetManager.GetEbxEntry("Characters/Zombie/Horde/BrownCoat/Body/Default/BrownCoat_Body_Default_VisualAsset");
                else if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2))
                    baseUnlock = App.AssetManager.GetEbxEntry("Characters/Zombie/Horde/BrownCoat/Costume/BrownCoat_Costume_Default_UnlockAsset");
            }

            if (baseUnlock == null)
            {
                App.Logger.LogError("No base customization found. Aborting.");
                return;
            }

            dynamic baseUnlockRoot = App.AssetManager.GetEbx(baseUnlock).RootObject;

            // 2. Base BlueprintBundle
            string baseBpbRef = baseUnlockRoot.BlueprintBundleReference?.Name;
            EbxAssetEntry baseBpb = null;
            if (!string.IsNullOrWhiteSpace(baseBpbRef))
                baseBpb = App.AssetManager.GetEbxEntry(baseBpbRef);
            if (baseBpb == null)
            {
                if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville))
                    baseBpb = App.AssetManager.GetEbxEntry("Characters/Zombie/Horde/BrownCoat/Body/Default/browncoat_body_default_visualasset_bpb");
                else if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2))
                    baseBpb = App.AssetManager.GetEbxEntry("Characters/Zombie/Horde/BrownCoat/Costume/browncoat_costume_default_unlockasset_bpb");
            }

            if (baseBpb == null)
            {
                App.Logger.LogError("Could not find a BlueprintBundle template.");
                return;
            }

            // 3. Base Assets Extraction (Object BP, NetReg, Mesh DB, Mesh)
            EbxAssetEntry baseObjectBP = null;
            EbxAssetEntry baseNetReg = App.AssetManager.GetEbxEntry(baseBpb.Name + "_networkregistry_Win32");
            EbxAssetEntry baseMeshDb = App.AssetManager.GetEbxEntry(baseBpb.Name + "/MeshVariationDb_Win32");
            EbxAssetEntry baseMesh = null;

            try
            {
                var visuals = baseUnlockRoot.Visuals;
                if (visuals != null && visuals.Count > 0)
                {
                    var outObjRef = visuals[0].OutObjectBlueprint;
                    if (outObjRef != null && outObjRef.External != null && outObjRef.External.FileGuid != Guid.Empty)
                        baseObjectBP = App.AssetManager.GetEbxEntry(outObjRef.External.FileGuid);
                }

                if (baseObjectBP == null)
                {
                    EbxAsset bpbAsset = App.AssetManager.GetEbx(baseBpb);
                    foreach (dynamic obj in bpbAsset.Objects)
                    {
                        string typeName = obj.GetType().Name;
                        if (typeName == "VisualCustomizationAsset" || typeName == "PVZVisualUnlockAsset")
                        {
                            var embeddedVisuals = obj.Visuals;
                            if (embeddedVisuals != null && embeddedVisuals.Count > 0)
                            {
                                var outObjRef = embeddedVisuals[0].OutObjectBlueprint;
                                if (outObjRef != null && outObjRef.External != null && outObjRef.External.FileGuid != Guid.Empty)
                                {
                                    baseObjectBP = App.AssetManager.GetEbxEntry(outObjRef.External.FileGuid);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (baseMeshDb != null)
                {
                    dynamic meshDbRoot = App.AssetManager.GetEbx(baseMeshDb).RootObject;
                    if (meshDbRoot.Entries != null && meshDbRoot.Entries.Count > 0)
                    {
                        var meshRef = meshDbRoot.Entries[0].Mesh;
                        if (meshRef != null && meshRef.External != null && meshRef.External.FileGuid != Guid.Empty)
                            baseMesh = App.AssetManager.GetEbxEntry(meshRef.External.FileGuid);
                    }
                }
            }
            catch { }

            if (baseObjectBP == null)
            {
                App.Logger.LogWarning($"Could not extract ObjectBlueprint from '{baseUnlock.Name}' or its BPB. Falling back to BrownCoat body ObjectBlueprint.");
                if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville))
                    baseObjectBP = App.AssetManager.GetEbxEntry("Characters/Zombie/Horde/BrownCoat/Body/Default/BrownCoat_Body_Default");
                else if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2))
                    baseObjectBP = App.AssetManager.GetEbxEntry("Characters/Zombie/Horde/BrownCoat/Body/Default/BrownCoat_Body_Default");
            }

            if (baseObjectBP == null)
            {
                App.Logger.LogError("No usable ObjectBlueprint found. Aborting.");
                return;
            }

            // 4. Bundle Creation
            string newBpbName = $"{mBlueprintDirectory}/{mCustomizationName}_visualasset_bpb";
            VerifyFileName(newBpbName);
            int sourceBundleId = baseBpb.Bundles.Count > 0
                ? baseBpb.Bundles[0]
                : baseBpb.AddedBundles[0];
            string bundleName = ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville)
                ? "Win32/" + newBpbName.ToLower().Replace("Win32/", "", StringComparison.OrdinalIgnoreCase)
                : "win32/" + newBpbName.Replace("win32/", "", StringComparison.OrdinalIgnoreCase);

            BundleEntry newBundleEntry = App.AssetManager.AddBundle(
                bundleName,
                BundleType.BlueprintBundle,
                App.AssetManager.GetBundleEntry(sourceBundleId).SuperBundleId);
            int bundleId = App.AssetManager.GetBundleId(newBundleEntry);

            List<ResAssetEntry> newResEntries = new List<ResAssetEntry>();
            List<ChunkAssetEntry> newChunkEntries = new List<ChunkAssetEntry>();

            // 5. Duplicate the core assets
            EbxAssetEntry newObjectBP = DuplicateAsset(baseObjectBP, objectBPName, false);
            newObjectBP.AddedBundles.Clear(); newObjectBP.AddedBundles.Add(bundleId);

            string unlockName = $"{mBlueprintDirectory}/{mCustomizationName}_VisualAsset";
            EbxAssetEntry newUnlock = DuplicateAsset(baseUnlock, unlockName, false);

            EbxAssetEntry newBpb = DuplicateAsset(baseBpb, newBpbName, false);
            newBpb.AddedBundles.Clear(); newBpb.AddedBundles.Add(bundleId);

            EbxAssetEntry newNetReg = null;
            string newNetRegName = $"{mBlueprintDirectory}/{mCustomizationName}_visualasset_bpb_networkregistry_Win32";
            if (baseNetReg != null)
            {
                VerifyFileName(newNetRegName);
                newNetReg = DuplicateAsset(baseNetReg, newNetRegName, false);
                if (newNetReg != null)
                {
                    newNetReg.AddedBundles.Clear(); newNetReg.AddedBundles.Add(bundleId);
                }
            }

            // 6. Deep Mesh Duplication (EBX + RES + Chunks)
            EbxAssetEntry newMesh = null;
            EbxAssetEntry newMeshDb = null;
            if (baseMesh != null && baseMeshDb != null)
            {
                string meshName = $"{mBlueprintDirectory}/{mCustomizationName}_Mesh".ToLower();
                VerifyFileName(meshName);
                newMesh = DuplicateAsset(baseMesh, meshName, false);

                if (newMesh != null)
                {
                    newMesh.AddedBundles.Clear(); newMesh.AddedBundles.Add(bundleId);

                    EbxAsset meshEbx = App.AssetManager.GetEbx(newMesh);
                    dynamic meshRoot = meshEbx.RootObject;

                    ResAssetEntry oldMeshRes = App.AssetManager.GetResEntry(meshRoot.MeshSetResource);
                    if (oldMeshRes != null)
                    {
                        ResAssetEntry newMeshRes = DuplicateRes(oldMeshRes, meshName, ResourceType.MeshSet);

                        if (newMeshRes != null)
                        {
                            newResEntries.Add(newMeshRes);

                            MeshSet newMeshSet = App.AssetManager.GetResAs<MeshSet>(newMeshRes);
                            newMeshSet.FullName = newMeshRes.Name;

                            foreach (var lod in newMeshSet.Lods)
                            {
                                lod.Name = newMeshRes.Name;
                                if (lod.ChunkId != Guid.Empty)
                                {
                                    ChunkAssetEntry lodChunk = App.AssetManager.GetChunkEntry(lod.ChunkId);
                                    if (lodChunk != null)
                                    {
                                        ChunkAssetEntry newChunk = DuplicateChunk(lodChunk);
                                        lod.ChunkId = newChunk.Id;
                                        newMeshRes.LinkAsset(newChunk);
                                        newChunkEntries.Add(newChunk);
                                    }
                                }
                            }

                            meshRoot.MeshSetResource = newMeshRes.ResRid;
                            meshRoot.NameHash = (uint)Utils.HashString(meshName);
                            newMesh.LinkAsset(newMeshRes);

                            // Force SaveBytes to recalculate the string offsets, then extract the updated ResMeta
                            newMeshSet.SaveBytes();
                            var fi = typeof(Resource).GetField("resMeta", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (fi != null)
                            {
                                newMeshRes.ResMeta = (byte[])fi.GetValue(newMeshSet);
                            }

                            App.AssetManager.ModifyRes(newMeshRes.Name, newMeshSet);
                        }
                    }
                    // Strip proc bones to prevent physics bone ownership collision when
                    // multiple same source duplicates are loaded simultaneously
                    if (stripPhysicsBonesCheckBox.IsChecked == true)
                    {
                        dynamic spa = meshRoot.SkinnedProceduralAnimation;
                        if (spa != null)
                        {
                            spa.Expressions.Clear();
                            spa.Bones.Clear();
                            spa.RootPoses.Clear();
                        }
                    }

                    App.AssetManager.ModifyEbx(newMesh.Name, meshEbx);
                }

                string meshDbName = $"{newBpbName}/MeshVariationDb_Win32";
                VerifyFileName(meshDbName);
                newMeshDb = DuplicateAsset(baseMeshDb, meshDbName, false);
                if (newMeshDb != null)
                {
                    newMeshDb.AddedBundles.Clear(); newMeshDb.AddedBundles.Add(bundleId);
                }
            }

            // 7. Update references
            EbxAsset newUnlockAsset = App.AssetManager.GetEbx(newUnlock);
            dynamic newUnlockRoot = newUnlockAsset.RootObject;
            newUnlockRoot.BlueprintBundleReference.Name = bundleName.Replace("win32/", "", StringComparison.OrdinalIgnoreCase);

            if (newUnlockRoot.Visuals != null && newUnlockRoot.Visuals.Count > 0)
            {
                dynamic visualData = newUnlockRoot.Visuals[0];
                visualData.OutObjectBlueprint = new PointerRef(new EbxImportReference
                {
                    FileGuid = newObjectBP.Guid,
                    ClassGuid = App.AssetManager.GetEbx(newObjectBP).RootInstanceGuid
                });
            }

            if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2))
            {
                newUnlockRoot.Identifier = (uint)Utils.HashString($"{newUnlock.Name}{newUnlock.Guid}", true);
                newUnlockRoot.DebugUnlockId = newUnlock.Name.Split('/').Last();
            }
            App.AssetManager.ModifyEbx(newUnlock.Name, newUnlockAsset);

            EbxAsset newBpbAsset = App.AssetManager.GetEbx(newBpb);
            foreach (dynamic obj in newBpbAsset.Objects)
            {
                string typeName = obj.GetType().Name;
                if (typeName == "VisualCustomizationAsset" || typeName == "PVZVisualUnlockAsset")
                {
                    if (obj.Visuals != null && obj.Visuals.Count > 0)
                    {
                        dynamic bpbVisualData = obj.Visuals[0];
                        bpbVisualData.OutObjectBlueprint = new PointerRef(new EbxImportReference
                        {
                            FileGuid = newObjectBP.Guid,
                            ClassGuid = App.AssetManager.GetEbx(newObjectBP).RootInstanceGuid
                        });
                    }
                }
            }
            App.AssetManager.ModifyEbx(newBpb.Name, newBpbAsset);

            if (newNetReg != null)
            {
                EbxAsset netRegAsset = App.AssetManager.GetEbx(newNetReg);
                dynamic netRegRoot = netRegAsset.RootObject;

                if (netRegRoot.Objects != null)
                {
                    netRegRoot.Objects.Clear();
                    netRegRoot.Objects.Add(new PointerRef(new EbxImportReference
                    {
                        FileGuid = newBpb.Guid,
                        ClassGuid = newBpbAsset.RootInstanceGuid
                    }));
                }
                App.AssetManager.ModifyEbx(newNetReg.Name, netRegAsset);
            }

            if (newMesh != null)
            {
                EbxAsset objBpAsset = App.AssetManager.GetEbx(newObjectBP);
                foreach (dynamic obj in objBpAsset.Objects)
                {
                    var propInfo = obj.GetType().GetProperty("Mesh");
                    if (propInfo != null)
                    {
                        propInfo.SetValue(obj, new PointerRef(new EbxImportReference
                        {
                            FileGuid = newMesh.Guid,
                            ClassGuid = App.AssetManager.GetEbx(newMesh).RootInstanceGuid
                        }));
                    }
                }
                App.AssetManager.ModifyEbx(newObjectBP.Name, objBpAsset);
            }

            // 8. Deep Texture Duplication (EBX + RES + Chunks)
            if (duplicateTexturesCheckBox.IsChecked == true && newMesh != null)
            {
                EbxAsset meshAsset = App.AssetManager.GetEbx(newMesh);
                dynamic meshRoot = meshAsset.RootObject;
                bool meshModified = false;

                string baseTexName = string.IsNullOrWhiteSpace(mTextureBaseName) ? mCustomizationName : mTextureBaseName;

                // Cache to prevent duplicating the same texture multiple times when materials share it
                Dictionary<string, EbxAssetEntry> duplicatedTextures = new Dictionary<string, EbxAssetEntry>();

                if (meshRoot.Materials != null)
                {
                    foreach (dynamic mat in meshRoot.Materials)
                    {
                        dynamic internalMat = mat.Internal;
                        if (internalMat != null && internalMat.Shader != null && internalMat.Shader.TextureParameters != null)
                        {
                            foreach (dynamic texParam in internalMat.Shader.TextureParameters)
                            {
                                if (texParam.Value != null && texParam.Value.External != null)
                                {
                                    EbxAssetEntry texEntry = App.AssetManager.GetEbxEntry(texParam.Value.External.FileGuid);
                                    if (texEntry != null)
                                    {
                                        string origName = texEntry.Name.Split('/').Last();
                                        string suffix = "";

                                        int mIndex = origName.IndexOf("_M_", StringComparison.OrdinalIgnoreCase);
                                        if (mIndex >= 0)
                                        {
                                            suffix = origName.Substring(mIndex);
                                        }
                                        else
                                        {
                                            int lastUnderscore = origName.LastIndexOf('_');
                                            if (lastUnderscore >= 0)
                                                suffix = origName.Substring(lastUnderscore);
                                        }

                                        string texName = $"{mBlueprintDirectory}/{baseTexName}{suffix}";
                                        VerifyFileName(texName);

                                        // Prevent duplicate crashes if multiple materials share a texture
                                        if (duplicatedTextures.ContainsKey(texName))
                                        {
                                            EbxAssetEntry existingTexEntry = duplicatedTextures[texName];
                                            texParam.Value = new PointerRef(new EbxImportReference
                                            {
                                                FileGuid = existingTexEntry.Guid,
                                                ClassGuid = App.AssetManager.GetEbx(existingTexEntry).RootInstanceGuid
                                            });
                                            meshModified = true;
                                            continue;
                                        }

                                        // Duplicate Texture EBX
                                        EbxAssetEntry newTexEntry = DuplicateAsset(texEntry, texName, false);
                                        if (newTexEntry != null)
                                        {
                                            duplicatedTextures.Add(texName, newTexEntry);

                                            newTexEntry.AddedBundles.Clear(); newTexEntry.AddedBundles.Add(bundleId);

                                            EbxAsset newTexAsset = App.AssetManager.GetEbx(newTexEntry);
                                            dynamic newTexRoot = newTexAsset.RootObject;

                                            // Duplicate Texture RES & Chunk
                                            ResAssetEntry oldTexRes = App.AssetManager.GetResEntry(newTexRoot.Resource);
                                            if (oldTexRes != null)
                                            {
                                                ResAssetEntry newTexRes = DuplicateRes(oldTexRes, texName, ResourceType.Texture);

                                                if (newTexRes != null)
                                                {
                                                    newResEntries.Add(newTexRes);

                                                    Texture oldTex = App.AssetManager.GetResAs<Texture>(oldTexRes);
                                                    Texture newTex = App.AssetManager.GetResAs<Texture>(newTexRes);

                                                    newTexRoot.Resource = newTexRes.ResRid;

                                                    ChunkAssetEntry oldTexChunk = App.AssetManager.GetChunkEntry(oldTex.ChunkId);
                                                    if (oldTexChunk != null)
                                                    {
                                                        ChunkAssetEntry newTexChunk = DuplicateChunk(oldTexChunk, (oldTex.Flags.HasFlag(TextureFlags.OnDemandLoaded) || oldTex.Type != TextureType.TT_2d) ? null : oldTex);
                                                        newTex.ChunkId = newTexChunk.Id;
                                                        newTexRes.LinkAsset(newTexChunk);
                                                        newChunkEntries.Add(newTexChunk);
                                                    }

                                                    newTexEntry.LinkAsset(newTexRes);
                                                    App.AssetManager.ModifyRes(newTexRes.Name, newTex);
                                                }
                                            }

                                            App.AssetManager.ModifyEbx(newTexEntry.Name, newTexAsset);

                                            // Wire new texture to mesh material
                                            texParam.Value = new PointerRef(new EbxImportReference
                                            {
                                                FileGuid = newTexEntry.Guid,
                                                ClassGuid = newTexAsset.RootInstanceGuid
                                            });
                                            meshModified = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (meshModified)
                {
                    App.AssetManager.ModifyEbx(newMesh.Name, meshAsset);
                }
            }

            // 9. Process Mesh Variation Db Pointers
            if (newMeshDb != null && newMesh != null)
            {
                EbxAsset meshDbAsset = App.AssetManager.GetEbx(newMeshDb);
                dynamic meshDbRoot = meshDbAsset.RootObject;

                EbxAsset meshAsset = App.AssetManager.GetEbx(newMesh);
                dynamic meshRoot = meshAsset.RootObject;

                if (meshDbRoot.Entries != null)
                {
                    foreach (dynamic entry in meshDbRoot.Entries)
                    {
                        entry.Mesh = new PointerRef(new EbxImportReference
                        {
                            FileGuid = newMesh.Guid,
                            ClassGuid = meshAsset.RootInstanceGuid
                        });

                        if (entry.Materials != null && meshRoot.Materials != null)
                        {
                            for (int i = 0; i < entry.Materials.Count; i++)
                            {
                                if (i < meshRoot.Materials.Count)
                                {
                                    dynamic matInternal = meshRoot.Materials[i].Internal;
                                    if (matInternal != null)
                                    {
                                        Guid matGuid = matInternal.GetInstanceGuid().ExportedGuid;
                                        entry.Materials[i].Material = new PointerRef(new EbxImportReference
                                        {
                                            FileGuid = newMesh.Guid,
                                            ClassGuid = matGuid
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                App.AssetManager.ModifyEbx(newMeshDb.Name, meshDbAsset);
            }

            // Bind tracked Res and Chunks to the bundle natively
            foreach (var res in newResEntries)
            {
                res.AddedBundles.Clear();
                res.AddedBundles.Add(bundleId);
            }
            foreach (var chk in newChunkEntries)
            {
                chk.AddedBundles.Clear();
                chk.AddedBundles.Add(bundleId);
            }

            // 10. Place unlock in characters shared bundle
            BundleEntry charBundle = null;
            foreach (BundleEntry bundle in App.AssetManager.EnumerateBundles())
            {
                if (bundle.Name.Equals("Win32/gameplay/kits/bundling/characterssharedbundleasset", StringComparison.OrdinalIgnoreCase))
                {
                    charBundle = bundle;
                    break;
                }
            }
            if (charBundle != null)
            {
                int unlockBundleId = App.AssetManager.GetBundleId(charBundle);
                newUnlock.AddedBundles.Clear();
                newUnlock.AddedBundles.Add(unlockBundleId);
            }

            // 11. Add to Characters Shared Network Registry
            if (addToCharactersSharedNetworkRegistryCheckBox.IsChecked == true)
            {
                try
                {
                    EbxAssetEntry netRegEntry = App.AssetManager.GetEbxEntry("gameplay/kits/bundling/characterssharedbundleasset_networkregistry_Win32");
                    if (netRegEntry != null)
                    {
                        EbxAsset netRegAsset = App.AssetManager.GetEbx(netRegEntry);
                        dynamic netRegRoot = netRegAsset.RootObject;
                        bool alreadyAdded = false;

                        foreach (dynamic member in netRegRoot.Objects)
                        {
                            if (member.External.FileGuid == newUnlock.Guid)
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }
                        if (!alreadyAdded)
                        {
                            netRegRoot.Objects.Add(new PointerRef(new EbxImportReference
                            {
                                FileGuid = newUnlock.Guid,
                                ClassGuid = newUnlockAsset.RootInstanceGuid
                            }));
                            netRegAsset.AddDependency(newUnlock.Guid);
                            App.AssetManager.ModifyEbx(netRegEntry.Name, netRegAsset);
                            App.Logger.Log("Added {0} to Characters Shared Network Registry", newUnlock.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Failed to add to Characters Shared Network Registry: {ex.Message}");
                }
            }

            RefreshEditorDataExplorer();
            App.Logger.Log("Created: ObjectBP={0}, Unlock={1}, BPB={2}, Bundle={3}",
                            newObjectBP.Name, newUnlock.Name, newBpbName, bundleName);
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private EbxAssetEntry CreateAsset(string newName, Type newType)
        {
            EbxAsset newAsset = new EbxAsset(TypeLibrary.CreateObject(newType.Name));
            newAsset.SetFileGuid(Guid.NewGuid());
            dynamic obj = newAsset.RootObject;
            obj.Name = newName;
            AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newAsset.Objects, (Type)obj.GetType(), newAsset.FileGuid), -1);
            obj.SetInstanceGuid(guid);
            EbxAssetEntry newEntry = App.AssetManager.AddEbx(newName, newAsset);
            newEntry.ModifiedEntry.DependentAssets.AddRange(newAsset.Dependencies);
            return newEntry;
        }

        private EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType = null)
        {
            EbxAsset asset = App.AssetManager.GetEbx(entry);
            EbxAsset newAsset;
            if (createNew)
            {
                newAsset = new EbxAsset(TypeLibrary.CreateObject(newType.Name));
            }
            else
            {
                using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream(), EbxWriteFlags.DoNotSort | EbxWriteFlags.IncludeTransient))
                {
                    writer.WriteAsset(asset);
                    byte[] buf = writer.ToByteArray();
                    using (EbxReader reader = EbxReader.CreateReader(new MemoryStream(buf)))
                        newAsset = reader.ReadAsset<EbxAsset>();
                }
            }
            newAsset.SetFileGuid(Guid.NewGuid());

            foreach (dynamic obj in newAsset.Objects)
            {
                AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newAsset.Objects, (Type)obj.GetType(), newAsset.FileGuid), -1);
                obj.SetInstanceGuid(guid);
            }

            dynamic rootObj = newAsset.RootObject;
            rootObj.Name = newName;

            EbxAssetEntry newEntry = App.AssetManager.AddEbx(newName, newAsset);
            newEntry.AddedBundles.AddRange(entry.EnumerateBundles());
            newEntry.ModifiedEntry.DependentAssets.AddRange(newAsset.Dependencies);
            return newEntry;
        }

        private ResAssetEntry DuplicateRes(ResAssetEntry entry, string name, ResourceType resType)
        {
            if (App.AssetManager.GetResEntry(name) == null)
            {
                using (NativeReader reader = new NativeReader(App.AssetManager.GetRes(entry)))
                    return App.AssetManager.AddRes(name, resType, entry.ResMeta, reader.ReadToEnd(), entry.EnumerateBundles().ToArray());
            }
            return null;
        }

        private ChunkAssetEntry DuplicateChunk(ChunkAssetEntry entry, Texture texture = null)
        {
            byte[] random = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                while (true)
                {
                    rng.GetBytes(random);
                    random[15] |= 1;
                    if (App.AssetManager.GetChunkEntry(new Guid(random)) == null)
                        break;
                }
            }
            Guid newGuid;
            using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(entry)))
            {
                newGuid = App.AssetManager.AddChunk(reader.ReadToEnd(), new Guid(random), texture, entry.EnumerateBundles().ToArray());
            }
            return App.AssetManager.GetChunkEntry(newGuid);
        }

        private static void RefreshEditorDataExplorer()
        {
            try
            {
                var editorWindow = Frosty.Core.App.EditorWindow;
                if (editorWindow != null)
                {
                    editorWindow.DataExplorer.ItemsSource = App.AssetManager.EnumerateEbx();
                    editorWindow.DataExplorer.RefreshItems();
                }
            }
            catch { }
        }
    }

    public class FolderNode : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public ObservableCollection<FolderNode> Children { get; set; } = new ObservableCollection<FolderNode>();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class DirectorySelectorWindow : FrostyWindow
    {
        private TreeView _treeView;
        private TextBox _searchBox;
        private ObservableCollection<FolderNode> _rootNodes = new ObservableCollection<FolderNode>();
        private List<string> _allDirs;
        public string SelectedPath { get; private set; }

        public DirectorySelectorWindow()
        {
            Title = "  Select Blueprint Directory";
            Width = 500; Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _allDirs = App.AssetManager.EnumerateEbx()
                .Select(entry => entry.Path.TrimStart('/'))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });

            _searchBox = new TextBox
            {
                Margin = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["ListBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"],
                BorderThickness = new Thickness(1)
            };
            _searchBox.TextChanged += (s, e) => BuildTree(_searchBox.Text);
            Grid.SetRow(_searchBox, 0); grid.Children.Add(_searchBox);

            _treeView = new TreeView
            {
                Margin = new Thickness(5),
                Background = (Brush)Application.Current.Resources["ListBackground"],
                BorderThickness = new Thickness(0),
                ItemsSource = _rootNodes
            };
            var template = new HierarchicalDataTemplate(typeof(FolderNode)) { ItemsSource = new Binding("Children") };
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            factory.SetValue(StackPanel.MarginProperty, new Thickness(0, 1, 0, 1));
            var imgFactory = new FrameworkElementFactory(typeof(Image));
            imgFactory.SetValue(Image.SourceProperty, new BitmapImage(new Uri("pack://application:,,,/FrostyEditor;component/Images/CloseFolder.png")));
            imgFactory.SetValue(Image.WidthProperty, 16.0);
            imgFactory.SetValue(Image.HeightProperty, 16.0);
            imgFactory.SetValue(Image.MarginProperty, new Thickness(0, 0, 4, 0));
            var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
            txtFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            txtFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            txtFactory.SetValue(TextBlock.ForegroundProperty, (Brush)Application.Current.Resources["FontColor"]);
            txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(imgFactory); factory.AppendChild(txtFactory);
            template.VisualTree = factory;
            _treeView.ItemTemplate = template;
            var style = new Style(typeof(TreeViewItem), (Style)Application.Current.FindResource(typeof(TreeViewItem)));
            style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }));
            _treeView.ItemContainerStyle = style;
            _treeView.SelectedItemChanged += (s, e) =>
            {
                if (e.NewValue is FolderNode node) node.IsSelected = true;
                if (e.OldValue is FolderNode oldNode) oldNode.IsSelected = false;
            };
            Grid.SetRow(_treeView, 1); grid.Children.Add(_treeView);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var selectBtn = new Button
            {
                Content = "Select",
                Width = 80,
                Height = 26,
                Margin = new Thickness(0, 0, 15, 0),
                IsEnabled = false,
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
            };
            selectBtn.Click += (s, e) => { SelectedPath = ((FolderNode)_treeView.SelectedItem)?.FullPath; DialogResult = true; Close(); };
            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 26,
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            _treeView.SelectedItemChanged += (s, e) => { selectBtn.IsEnabled = _treeView.SelectedItem is FolderNode; };
            btnPanel.Children.Add(selectBtn); btnPanel.Children.Add(cancelBtn);
            Grid.SetRow(btnPanel, 2); grid.Children.Add(btnPanel);

            Content = grid;
            BuildTree("");
        }

        private void BuildTree(string query)
        {
            _rootNodes.Clear();
            query = query?.ToLower() ?? "";
            var rootDict = new Dictionary<string, FolderNode>();
            foreach (var dir in _allDirs)
            {
                if (!string.IsNullOrWhiteSpace(query) && !dir.ToLower().Contains(query)) continue;
                var parts = dir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string cumulativePath = "";
                FolderNode parent = null;
                foreach (var part in parts)
                {
                    cumulativePath += part + "/";
                    if (!rootDict.TryGetValue(cumulativePath, out FolderNode node))
                    {
                        node = new FolderNode { Name = part, FullPath = cumulativePath.TrimEnd('/') };
                        rootDict[cumulativePath] = node;
                        if (parent == null) _rootNodes.Add(node); else parent.Children.Add(node);
                    }
                    parent = node;
                }
            }
        }
    }

    public class CustomizationSelectorWindow : FrostyWindow
    {
        private ListView listView;
        private TextBox _searchBox;
        private EbxAssetEntry[] _allEntries;

        private int _searchVersion = 0;

        public string SelectedAssetPath { get; private set; }

        public CustomizationSelectorWindow()
        {
            Title = "  Select Base Customization";
            Width = 600; Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            string assetType = ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville)
                ? "VisualCustomizationAsset"
                : ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2)
                    ? "PVZVisualUnlockAsset"
                    : "";

            if (string.IsNullOrEmpty(assetType))
            {
                App.Logger.LogError("Unsupported game profile for customization selector.");
                DialogResult = false;
                Close();
                return;
            }

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });

            _searchBox = new TextBox
            {
                Margin = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["ListBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"],
                BorderThickness = new Thickness(1),
                IsEnabled = false
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            Grid.SetRow(_searchBox, 0); grid.Children.Add(_searchBox);

            listView = new ListView
            {
                Margin = new Thickness(5),
                Background = (Brush)Application.Current.Resources["ListBackground"],
                BorderThickness = new Thickness(0)
            };

            VirtualizingPanel.SetIsVirtualizing(listView, true);
            VirtualizingPanel.SetVirtualizationMode(listView, VirtualizationMode.Recycling);
            ScrollViewer.SetIsDeferredScrollingEnabled(listView, true);

            var view = new GridView();
            view.Columns.Add(new GridViewColumn { Header = "Customization", Width = 580, DisplayMemberBinding = new Binding("Name") });
            listView.View = view;
            Grid.SetRow(listView, 1); grid.Children.Add(listView);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var selectBtn = new Button
            {
                Content = "Select",
                Width = 80,
                Height = 26,
                Margin = new Thickness(0, 0, 15, 0),
                IsEnabled = false,
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
            };
            selectBtn.Click += (s, e) => ConfirmSelection();

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 26,
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

            listView.SelectionChanged += (s, e) => { selectBtn.IsEnabled = listView.SelectedItem != null; };
            listView.MouseDoubleClick += (s, e) => ConfirmSelection();

            btnPanel.Children.Add(selectBtn); btnPanel.Children.Add(cancelBtn);
            Grid.SetRow(btnPanel, 2); grid.Children.Add(btnPanel);

            Content = grid;

            Loaded += async (s, e) =>
            {
                await Task.Run(() =>
                {
                    _allEntries = App.AssetManager.EnumerateEbx(assetType).OrderBy(x => x.Name).ToArray();
                });

                listView.ItemsSource = _allEntries;
                _searchBox.IsEnabled = true;
                _searchBox.Focus();
            };
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int currentVersion = Interlocked.Increment(ref _searchVersion);
            string query = _searchBox.Text;

            await Task.Delay(40);

            if (currentVersion != Volatile.Read(ref _searchVersion)) return;

            if (string.IsNullOrWhiteSpace(query))
            {
                if (listView.ItemsSource != _allEntries)
                {
                    listView.ItemsSource = _allEntries;
                }
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var entries = _allEntries;
                int total = entries.Length;

                var tempResults = new EbxAssetEntry[total];
                int matchCount = 0;

                for (int i = 0; i < total; i++)
                {
                    if (Volatile.Read(ref _searchVersion) != currentVersion) return;

                    if (entries[i].Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        tempResults[matchCount++] = entries[i];
                    }
                }

                if (Volatile.Read(ref _searchVersion) != currentVersion) return;

                var finalResults = new EbxAssetEntry[matchCount];
                Array.Copy(tempResults, finalResults, matchCount);

                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (currentVersion == Volatile.Read(ref _searchVersion))
                    {
                        listView.ItemsSource = finalResults;
                    }
                }, DispatcherPriority.DataBind);
            });
        }

        private void ConfirmSelection()
        {
            if (listView.SelectedItem is EbxAssetEntry selected)
            {
                SelectedAssetPath = selected.Name;
                DialogResult = true;
                Close();
            }
        }
    }
}