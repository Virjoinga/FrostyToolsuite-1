using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Xml.Linq;

namespace SkeletonImportFromXmlPlugin
{
    public class SkeletonImportFromXmlContextMenuItem : DataExplorerContextMenuExtension
    {
        public override string ContextItemName => "Import Skeleton from XML...";
        public override ImageSource Icon => null;

        public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
        {
            EbxAssetEntry entry = App.SelectedAsset as EbxAssetEntry;
            if (entry == null || entry.Type != "SkeletonAsset")
            {
                App.Logger.LogWarning("You must select a SkeletonAsset to import into.");
                return;
            }

            FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Select Skeleton XML", "*.xml (XML Files)|*.xml", "Skeleton");
            if (!ofd.ShowDialog())
                return;

            string xmlPath = ofd.FileName;

            FrostyTaskWindow.Show("Importing Skeleton from XML", "", (task) =>
            {
                try
                {
                    XDocument doc = XDocument.Load(xmlPath);
                    XElement skeletonElem = doc.Descendants("SkeletonAsset").FirstOrDefault();
                    if (skeletonElem == null)
                    {
                        App.Logger.LogError("Invalid XML: no <SkeletonAsset> element found.");
                        return;
                    }

                    EbxAsset asset = App.AssetManager.GetEbx(entry);
                    dynamic skeletonAsset = asset.RootObject;
                    
                    float ParseFloat(string s)
                    {
                        if (string.IsNullOrWhiteSpace(s))
                            throw new FormatException("Empty string");
                        return float.Parse(s, CultureInfo.InvariantCulture);
                    }
                    
                    void ParseVec3(XElement vecElem, dynamic targetVec)
                    {
                        if (vecElem == null) return;
                        XElement xElem = vecElem.Element("x");
                        XElement yElem = vecElem.Element("y");
                        XElement zElem = vecElem.Element("z");
                        if (xElem != null) targetVec.x = ParseFloat(xElem.Value.Trim());
                        if (yElem != null) targetVec.y = ParseFloat(yElem.Value.Trim());
                        if (zElem != null) targetVec.z = ParseFloat(zElem.Value.Trim());
                    }
                    
                    void ParseLinearTransform(XElement ltElem, dynamic lt)
                    {
                        XElement right = ltElem.Element("right")?.Element("Vec3");
                        XElement up = ltElem.Element("up")?.Element("Vec3");
                        XElement forward = ltElem.Element("forward")?.Element("Vec3");
                        XElement trans = ltElem.Element("trans")?.Element("Vec3");

                        if (right != null) ParseVec3(right, lt.right);
                        if (up != null) ParseVec3(up, lt.up);
                        if (forward != null) ParseVec3(forward, lt.forward);
                        if (trans != null) ParseVec3(trans, lt.trans);
                    }

                    dynamic CreateTransformList(string propertyName)
                    {
                        
                        dynamic currentList = ((object)skeletonAsset).GetType().GetProperty(propertyName).GetValue(skeletonAsset, null);
                        Type listType = currentList?.GetType();
                        if (listType == null || !listType.IsGenericType)
                        {
                            Type fallbackType = TypeLibrary.CreateObject("LinearTransform").GetType();
                            Type genericListType = typeof(List<>).MakeGenericType(fallbackType);
                            return Activator.CreateInstance(genericListType);
                        }
                        else
                        {
                            Type elementType = listType.GetGenericArguments()[0];
                            Type genericListType = typeof(List<>).MakeGenericType(elementType);
                            return Activator.CreateInstance(genericListType);
                        }
                    }
                    
                    XElement boneNamesElem = skeletonElem.Element("BoneNames");
                    if (boneNamesElem != null)
                    {
                        List<CString> boneNames = new List<CString>();
                        foreach (XElement member in boneNamesElem.Elements("member"))
                        {
                            string value = member.Value.Trim();
                            boneNames.Add(new CString(value));
                        }
                        skeletonAsset.BoneNames = boneNames;
                        App.Logger.Log($"[Import] BoneNames: {boneNames.Count} entries");
                    }
                    
                    XElement hashesElem = skeletonElem.Element("BoneNameHashes");
                    if (hashesElem != null)
                    {
                        List<uint> hashes = new List<uint>();
                        foreach (XElement member in hashesElem.Elements("member"))
                        {
                            string val = member.Value.Trim();
                            if (val.StartsWith("0x")) val = val.Substring(2);
                            hashes.Add(uint.Parse(val, NumberStyles.HexNumber));
                        }
                        skeletonAsset.BoneNameHashes = hashes;
                        App.Logger.Log($"[Import] BoneNameHashes: {hashes.Count} entries");
                    }
                    
                    XElement hierarchyElem = skeletonElem.Element("Hierarchy");
                    if (hierarchyElem != null)
                    {
                        List<int> hierarchy = new List<int>();
                        foreach (XElement member in hierarchyElem.Elements("member"))
                        {
                            string val = member.Value.Trim();
                            if (val.StartsWith("0x")) val = val.Substring(2);
                            hierarchy.Add(int.Parse(val, NumberStyles.HexNumber));
                        }
                        skeletonAsset.Hierarchy = hierarchy;
                        App.Logger.Log($"[Import] Hierarchy: {hierarchy.Count} entries");
                    }
                    
                    XElement localPoseElem = skeletonElem.Element("LocalPose");
                    if (localPoseElem != null)
                    {
                        dynamic localPoseList = CreateTransformList("LocalPose");
                        Type ltType = localPoseList.GetType().GetGenericArguments()[0];
                        foreach (XElement member in localPoseElem.Elements("member"))
                        {
                            XElement ltElem = member.Element("LinearTransform");
                            if (ltElem != null)
                            {
                                dynamic lt = Activator.CreateInstance(ltType);
                                ParseLinearTransform(ltElem, lt);
                                localPoseList.Add(lt);
                            }
                        }

                        skeletonAsset.LocalPose = localPoseList;
                        App.Logger.Log($"[Import] LocalPose: {localPoseList.Count} entries");
                    }
                    
                    XElement modelPoseElem = skeletonElem.Element("ModelPose");
                    if (modelPoseElem != null)
                    {
                        dynamic modelPoseList = CreateTransformList("ModelPose");
                        Type ltType = modelPoseList.GetType().GetGenericArguments()[0];
                        foreach (XElement member in modelPoseElem.Elements("member"))
                        {
                            XElement ltElem = member.Element("LinearTransform");
                            if (ltElem != null)
                            {
                                dynamic lt = Activator.CreateInstance(ltType);
                                ParseLinearTransform(ltElem, lt);
                                modelPoseList.Add(lt);
                            }
                        }

                        skeletonAsset.ModelPose = modelPoseList;
                        App.Logger.Log($"[Import] ModelPose: {modelPoseList.Count} entries");
                    }
                    
                    void ParseIntList(XElement parent, string elemName, Action<List<int>> setter)
                    {
                        XElement elem = parent.Element(elemName);
                        if (elem == null) return;
                        List<int> list = new List<int>();
                        foreach (XElement member in elem.Elements("member"))
                        {
                            string val = member.Value.Trim();
                            if (val.StartsWith("0x")) val = val.Substring(2);
                            list.Add(int.Parse(val, NumberStyles.HexNumber));
                        }
                        setter(list);
                        App.Logger.Log($"[Import] {elemName}: {list.Count} entries");
                    }

                    ParseIntList(skeletonElem, "ServerSkeletonToSkeletonMap", v => skeletonAsset.ServerSkeletonToSkeletonMap = v);
                    ParseIntList(skeletonElem, "SkeletonToServerSkeletonMap", v => skeletonAsset.SkeletonToServerSkeletonMap = v);
                    ParseIntList(skeletonElem, "ServerHierarchy", v => skeletonAsset.ServerHierarchy = v);
                    ParseIntList(skeletonElem, "GameplayBonesToSkeleton", v => skeletonAsset.GameplayBonesToSkeleton = v);
                    ParseIntList(skeletonElem, "GameplayBonesToServerSkeleton", v => skeletonAsset.GameplayBonesToServerSkeleton = v);
                    
                    XElement nameElem = skeletonElem.Element("Name");
                    if (nameElem != null)
                    {
                        skeletonAsset.Name = nameElem.Value.Trim();
                        App.Logger.Log($"[Import] Name: {skeletonAsset.Name}");
                    }

                    App.AssetManager.ModifyEbx(entry.Name, asset);
                    App.Logger.Log($"Successfully imported skeleton from {xmlPath} into {entry.Name}");
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Error importing skeleton: {ex.Message}");
                    App.Logger.LogError($"Stack trace: {ex.StackTrace}");
                }
            });
        });
    }
}