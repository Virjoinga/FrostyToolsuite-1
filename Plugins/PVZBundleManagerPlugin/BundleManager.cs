using Frosty.Controls;
using Frosty.Core;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using Microsoft.CSharp.RuntimeBinder;
using PvZBundleManagerPlugin.Ports.Classes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PvZBundleManagerPlugin
{
    internal class BundleManager : ILoggable
    {
        private const uint CacheMagic = 0xDEADFED4;

        private const uint CacheVersion = 2u;

        private string NetworkRegistryTypesPath = "Caches/" + "networkregistrytypes.txt";

        private bool IsBfn;

        internal ILogger m_logger;

        internal AssetManager m_assetManager;

        internal FileSystemManager m_fs;

        internal static Dictionary<string, BaseAssetHandler> s_handlers;

        internal Dictionary<string, BaseAssetHandler> m_handlers
        {
            get
            {
                if (!m_handlersInitialized)
                {
                    s_handlers = AssetHandlerDB.GetAssetHandlers();
                    m_handlersInitialized = true;
                }
                return s_handlers;
            }
        }

        internal bool m_highbundleWarning = false;
        internal bool m_handlersInitialized = false;

        /// <summary>
        /// Maps a bundle to its load order data.
        /// PrevBundles is a list of bundles that are loaded before this bundle, into the same compartment as this bundle.
        /// ParentBundles is a list of bundles in the parent compartment.
        /// </summary>
        internal Dictionary<int, (List<int> PrevBundles, List<int> ParentBundles)> m_bundleAndParents = new Dictionary<int, (List<int> PrevBundles, List<int> ParentBundles)>();

        internal List<int> m_staticBundleIds = new List<int>();
        internal List<int> m_gameBundleIds = new List<int>();

        // Using a hashset for faster lookups
        internal HashSet<string> m_networkRegistryTypes = new HashSet<string>();

        // Types found from searching all network registries
        internal HashSet<string> m_foundRegistryTypes = new HashSet<string>();

        public void ClearLogger()
        {
            m_logger = null;
        }

        public void SetLogger(ILogger inLogger)
        {
            m_logger = inLogger;
        }

        internal void WriteToLog(string text, params object[] vars)
        {
            m_logger?.Log(text, vars);
        }

        public BundleManager(AssetManager inAm, FileSystemManager inFs)
        {
            m_assetManager = inAm;
            m_fs = inFs;
        }

        public void Initialize()
        {
            if (!ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2, ProfileVersion.PlantsVsZombiesGardenWarfare, ProfileVersion.PlantsVsZombiesBattleforNeighborville))
            {
                return;
            }

             IsBfn = ProfilesLibrary.DataVersion == (int)ProfileVersion.PlantsVsZombiesBattleforNeighborville;

            if (IsBfn)
            {
                NetworkRegistryTypesPath = "Caches/networkregistrytypes_bfn.txt";
            }

            if (!ReadFromCache())
            {

                try { File.Delete(m_fs.CacheName + ".bundlecache"); } catch { }
                if (File.Exists(NetworkRegistryTypesPath))
                {
                    m_networkRegistryTypes = File.ReadAllLines(NetworkRegistryTypesPath).ToHashSet();
                }

                CacheStaticBundles();
                EnumerateSharedBundles();
                EnumerateLevels();
                EnumerateBlueprintBundles();
                FindNetworkRegistryTypes();
                WriteCache();
            }
        }

        #region Bundle Management Methods

        public void BundleManageAsset(EbxAssetEntry entry)
        {
            if (!ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare, ProfileVersion.PlantsVsZombiesGardenWarfare2, ProfileVersion.PlantsVsZombiesBattleforNeighborville))
            {
                FrostyMessageBox.Show($"The bundle manager currently doesn't support {ProfilesLibrary.DisplayName}", "Frosty Editor", System.Windows.MessageBoxButton.OK);
            }
            WriteToLog("Managing asset {0}", entry.Name);
            CheckForModifiedDescriptions();
            List<int> bundleIds = entry.Bundles.Concat(entry.AddedBundles).ToList();
            foreach (int bundleId in bundleIds)
            {
                BundleEntry bundleEntry = m_assetManager.GetBundleEntry(bundleId);
                if (bundleEntry.Added)
                {
                    if (bundleEntry.Type == BundleType.SubLevel)
                    {
                        if (!FindParentsOfNewSublevelBundle(bundleEntry))
                        {
                            App.Logger.LogError("Unable to find parents of sublevel bundle {0}. The operation has been cancelled. Ensure that your new SubWorldData is referenced before trying to manage assets in the bundle.", bundleEntry.Name);
                            return;
                        }
                    }
                    if (bundleEntry.Type == BundleType.BlueprintBundle)
                    {
                        if (!FindParentsOfNewBPB(bundleEntry))
                        {
                            App.Logger.LogError("Unable to find parents of blueprint bundle {0}. The operation has been cancelled. Ensure that the UnlockAsset has been added to bundles.", bundleEntry.Name);
                            return;
                        }
                    }
                }
            }
            foreach (EbxAssetEntry dependencyEntry in entry.GetAllDependencies())
            {
                CheckAssetBundles(dependencyEntry, bundleIds);
            }
            foreach (int bundleId in bundleIds)
            {
                CheckIfRegistered(entry, bundleId);
            }

            // Automatically add BuffData to the AllBuffs AssetGatherer
            if (entry.Type == "BuffData" || TypeLibrary.IsSubClassOf(entry.Type, "BuffData"))
            {
                AddBuffToAllBuffs(entry);
            }

            // Automatically add PerkAsset to the AllPerks AssetGatherer
            if (entry.Type == "PerkAsset" || TypeLibrary.IsSubClassOf(entry.Type, "PerkAsset"))
            {
                AddPerkToAllPerks(entry);
            }
        }

        private void AddPerkToAllPerks(EbxAssetEntry entry)
        {
            EbxAssetEntry allPerksEntry = m_assetManager.GetEbxEntry("Gameplay/Perks/Bundling/AllPerks");
            if (allPerksEntry == null)
            {
                return;
            }

            EbxAsset allPerksAsset = m_assetManager.GetEbx(allPerksEntry);
            dynamic root = allPerksAsset.RootObject;

            try
            {
                var assetsList = root.Assets;
                if (assetsList != null)
                {
                    EbxAsset perkAsset = m_assetManager.GetEbx(entry);
                    dynamic perkRoot = perkAsset.RootObject;
                    AssetClassGuid objGuid = perkRoot.GetInstanceGuid();

                    PointerRef pr = new PointerRef(new EbxImportReference()
                    {
                        FileGuid = entry.Guid,
                        ClassGuid = objGuid.ExportedGuid
                    });

                    bool alreadyContains = false;
                    foreach (dynamic existingPr in assetsList)
                    {
                        if (existingPr.External.FileGuid == pr.External.FileGuid &&
                            existingPr.External.ClassGuid == pr.External.ClassGuid)
                        {
                            alreadyContains = true;
                            break;
                        }
                    }

                    if (!alreadyContains)
                    {
                        assetsList.Add(pr);
                        allPerksAsset.AddDependency(entry.Guid);
                        m_assetManager.ModifyEbx(allPerksEntry.Name, allPerksAsset);
                        WriteToLog("Added perk {0} to Gameplay/Perks/Bundling/AllPerks.", entry.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog("Failed to add perk to AllPerks: {0}", ex.Message);
            }
        }

        private void AddBuffToAllBuffs(EbxAssetEntry entry)
        {
            EbxAssetEntry allBuffsEntry = m_assetManager.GetEbxEntry("Gameplay/Weapons/Bundling/AllBuffs");
            if (allBuffsEntry == null)
            {
                return;
            }

            EbxAsset allBuffsAsset = m_assetManager.GetEbx(allBuffsEntry);
            dynamic root = allBuffsAsset.RootObject;

            try
            {
                var assetsList = root.Assets;
                if (assetsList != null)
                {
                    EbxAsset buffAsset = m_assetManager.GetEbx(entry);
                    dynamic buffRoot = buffAsset.RootObject;
                    AssetClassGuid objGuid = buffRoot.GetInstanceGuid();

                    PointerRef pr = new PointerRef(new EbxImportReference()
                    {
                        FileGuid = entry.Guid,
                        ClassGuid = objGuid.ExportedGuid
                    });

                    bool alreadyContains = false;
                    foreach (dynamic existingPr in assetsList)
                    {
                        if (existingPr.External.FileGuid == pr.External.FileGuid &&
                            existingPr.External.ClassGuid == pr.External.ClassGuid)
                        {
                            alreadyContains = true;
                            break;
                        }
                    }

                    if (!alreadyContains)
                    {
                        assetsList.Add(pr);
                        allBuffsAsset.AddDependency(entry.Guid);
                        m_assetManager.ModifyEbx(allBuffsEntry.Name, allBuffsAsset);
                        WriteToLog("Added buff {0} to Gameplay/Weapons/Bundling/AllBuffs.", entry.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog("Failed to add buff to AllBuffs: {0}", ex.Message);
            }
        }

        private void CheckForModifiedDescriptions()
        {
            foreach (EbxAssetEntry refEntry in m_assetManager.EnumerateEbx(type: "LevelDescriptionAsset", true))
            {
                if (refEntry.IsAdded || refEntry.Name.Contains("Level_AssetScreenshots"))
                {
                    continue;
                }

                dynamic refRoot = m_assetManager.GetEbx(refEntry).RootObject;
                string levelName = refRoot.LevelName.ToString();
                List<int> prevLoadIds = new List<int>();

                foreach (dynamic bundle in refRoot.Bundles)
                {
                    string bundleName = $"{(IsBfn ? "Win32" : "win32")}/{bundle.Name.ToString().ToLower()}";
                    int bundleId = m_assetManager.GetBundleId(bundleName);
                    if (bundleId == -1 || bundle.Name.ToString() == levelName)
                    {
                        continue;
                    }
                    prevLoadIds.Add(bundleId);
                }

                string levelBundleName = $"{(IsBfn ? "Win32" : "win32")}/{levelName.ToLower()}";

                int levelBundleId = m_assetManager.GetBundleId(levelBundleName);

                foreach (int prevLoadId in prevLoadIds)
                {
                    if (!m_bundleAndParents[levelBundleId].PrevBundles.Contains(prevLoadId))
                    {
                        m_bundleAndParents[levelBundleId].PrevBundles.Add(prevLoadId);
                    }
                }
            }
        }

        private bool FindParentsOfNewSublevelBundle(BundleEntry bundleEntry)
        {
            int bundleId = m_assetManager.GetBundleId(bundleEntry);
            string newBundleName = bundleEntry.Name.Replace("win32/", string.Empty, StringComparison.OrdinalIgnoreCase);
            bool foundParent = false;
            if (!m_bundleAndParents.ContainsKey(bundleId))
            {
                WriteToLog("Finding parents of new bundle {0}", bundleEntry.Name);
                foreach (EbxAssetEntry entry in m_assetManager.EnumerateEbx().Where(entry => entry.IsModified && (entry.Type == "LevelData" || entry.Type == "SubWorldData" || entry.Type == "DetachedSubWorldData")))
                {
                    foreach (dynamic obj in m_assetManager.GetEbx(entry).ExportedObjects)
                    {
                        if (obj.GetType().Name == "SubWorldReferenceObjectData")
                        {
                            string bundleName = obj.BundleName.ToString().ToLower();
                            if (bundleName == newBundleName)
                            {
                                foundParent = true;
                                // Combining the bundles and addedbundles just in case this new subworld is a child of an added subworld
                                m_bundleAndParents.Add(bundleId, (new List<int>(), entry.Bundles.Concat(entry.AddedBundles).ToList()));
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                return true;
            }
            return foundParent;
        }

        private bool FindParentsOfNewBPB(BundleEntry bundleEntry)
        {
            bool foundParent = false;
            int bundleId = m_assetManager.GetBundleId(bundleEntry);
            if (!m_bundleAndParents.ContainsKey(bundleId))
            {
                WriteToLog("Finding parents of new bundle {0}", bundleEntry.Name);
                /*
                foreach (EbxAssetEntry entry in m_assetManager.EnumerateEbx("PVZCharacterWeaponUnlockAsset", true))
                {
                    dynamic unlockAsset = m_assetManager.GetEbx(entry).RootObject;
                    string bpbName = "win32/" + unlockAsset.WeaponBlueprintBundleReference.Name;
                    if (bpbName == bundleEntry.Name)
                    {
                        m_bundleAndParents.Add(bundleId, (new List<int>(), entry.AddedBundles));
                        foundParent = true;
                        break;
                    }
                }
                // It's not a weapon, so it must be a visual
                if (!foundParent)
                {
                    foreach (EbxAssetEntry entry in m_assetManager.EnumerateEbx("PVZVisualUnlockAsset", true))
                    {
                        dynamic unlockAsset = m_assetManager.GetEbx(entry).RootObject;
                        string bpbName = "win32/" + unlockAsset.BlueprintBundleReference.Name;
                        if (bpbName == bundleEntry.Name)
                        {
                            int bpbBundleId = m_assetManager.GetBundleId(bpbName);
                            m_bundleAndParents.Add(bpbBundleId, (new List<int>(), entry.AddedBundles));
                            foundParent = true;
                            break;
                        }
                    }
                }
                */
                m_bundleAndParents.Add(bundleId, (new List<int>(), m_gameBundleIds));
            }
            else
            {
                return true;
            }
            return foundParent;
        }

        private void AddAssetToBundle(EbxAssetEntry entry, int bundleId)
        {
            WriteToLog("Adding asset {0} to bundle {1}", entry.Name, m_assetManager.GetBundleEntry(bundleId).Name);
            string assetKey = "null";
            // If there isn't a handler for the exact asset type, check if there's a handler for the base type
            if (!m_handlers.ContainsKey(entry.Type))
            {
                foreach (string handlerType in m_handlers.Keys)
                {
                    if (TypeLibrary.IsSubClassOf(entry.Type, handlerType))
                    {
                        assetKey = handlerType;
                        break;
                    }
                }
            }
            else
            {
                assetKey = entry.Type;
            }
            m_handlers[assetKey].AddToBundle(entry, m_assetManager.GetBundleEntry(bundleId));

            // This has been removed in favor of the handling within MeshAssetHandler
            //if (assetKey == "MeshAsset")
            //{
            //    // Make sure mesh is in all required MeshDBs
            //    Tuple<dynamic, List<EbxAssetEntry>> meshDbData = GetMeshDbEntryForMesh(entry);
            //    if (meshDbData.Item1 != null)
            //    {
            //        UpdateMeshVariationDB(bundleId, meshDbData);
            //        foreach (EbxAssetEntry refEntry in meshDbData.Item2)
            //        {
            //            CheckAssetBundles(refEntry, new List<int> { bundleId });
            //        }
            //    }
            //}

            if (entry.Type == "ObjectVariation")
            {
                Tuple<dynamic, List<EbxAssetEntry>> meshDbData = GetMeshDbEntryForObjectVariation(entry);
                if (meshDbData.Item1 != null)
                {
                    UpdateMeshVariationDB(bundleId, meshDbData, entry);
                    foreach (EbxAssetEntry refEntry in meshDbData.Item2)
                    {
                        CheckAssetBundles(refEntry, new List<int> { bundleId });
                    }
                }
            }

            // Update registries
            CheckIfRegistered(entry, bundleId);

            if (!m_highbundleWarning)
            {
                if (m_assetManager.GetBundleEntry(bundleId).Name.Contains("fe_hub/streaming_highbundle"))
                {
                    m_highbundleWarning = true;
                    App.Logger.LogWarning("A hub highbundle has been modified. The game will softlock if these bundles aren't disabled.");
                }
            }
        }

        /// <summary>
        /// Checks if the given asset can be loaded by any of the given bundle ids.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="bundleIds"></param>
        private void CheckAssetBundles(EbxAssetEntry asset, List<int> bundleIds)
        {
            //WriteToLog("Checking asset {0}", asset.Name);
            foreach (int bundleId in bundleIds)
            {
                if (asset.Bundles.Contains(bundleId))
                {
                    return;
                }
                if (asset.AddedBundles.Contains(bundleId))
                {
                    return;
                }
                if (!CanBeLoadedByBundle(asset, bundleId))
                {
                    AddAssetToBundle(asset, bundleId);
                }
            }
        }

        /// <summary>
        /// Checks if the given entry is registered in the bundle's NetworkRegistry. If not, it will be registered.
        /// If no registry is found in the bundle, one will be created.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="bundleId"></param>
        private void CheckIfRegistered(EbxAssetEntry entry, int bundleId)
        {
            List<EbxImportReference> networkedObjects = GetNetworkedObjectsInAsset(entry);
            if (networkedObjects.Count > 0)
            {
                BundleEntry bundleEntry = m_assetManager.GetBundleEntry(bundleId);
                List<EbxAssetEntry> registry = m_assetManager.EnumerateEbx(bundleEntry).Where(e => e.Type == "NetworkRegistryAsset").ToList();
                if (registry.Count == 0)
                {
                    //WriteToLog("Bundle {0} needs a NetworkRegistry.", bundleEntry.Name);
                    registry.Add(CreateRegistry(bundleId));
                }
                if (registry.Count > 1)
                {
                    WriteToLog("Bundle {0} has more than one NetworkRegistry. This should never happen.", bundleEntry.Name);
                    return;
                }
                EbxAssetEntry networkRegistryEntry = registry[0];
                EbxAsset networkRegistry = m_assetManager.GetEbx(networkRegistryEntry);

                int registeredCount = 0;

                dynamic registryRoot = networkRegistry.RootObject;
                foreach (EbxImportReference objToRegister in networkedObjects)
                {
                    PointerRef pr = new PointerRef(objToRegister);
                    if (!registryRoot.Objects.Contains(pr))
                    {
                        networkRegistry.AddDependency(objToRegister.FileGuid);
                        registryRoot.Objects.Add(pr);
                        registeredCount++;
                        m_assetManager.ModifyEbx(networkRegistryEntry.Name, networkRegistry);
                    }
                }
                if (registeredCount > 0)
                {
                    WriteToLog("Added {0} objects to registry {1}.", registeredCount, networkRegistryEntry.Name);
                }
            }
        }

        /// <summary>
        /// Gets all networked objects in the given asset.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private List<EbxImportReference> GetNetworkedObjectsInAsset(EbxAssetEntry entry)
        {
            EbxAsset asset = m_assetManager.GetEbx(entry);
            List<EbxImportReference> networkedObjects = new List<EbxImportReference>();
            if (asset.ExportedObjects.Count() == 0) return networkedObjects;

            // If this asset has DataBusData flags
            /*try
            {
                if (((dynamic)asset.RootObject).Flags != null)
                {
                    // Check if the NeedsNetworkId flag has been set
                    // If not, return nothing
                    ushort flags = ((dynamic)asset.RootObject).Flags;
                    bool needsNetworking = (flags & (ushort)DataBusFlags.NeedNetworkIdFlag) != 0;
                    if (!needsNetworking)
                    {
                        return networkedObjects;
                    }
                }
            }
            catch (RuntimeBinderException) { }*/

            foreach (dynamic obj in asset.ExportedObjects)
            {
                //Does this object really need to be registered?
                Type objType = obj.GetType();
                if (m_networkRegistryTypes.Contains(objType.Name))
                {
                    try
                    {
                        if (obj.Realm != null)
                        {
                            // Client-sided, doesn't need to be networked
                            if ((int)obj.Realm == 0) // This only matters for modified assets, since client-sided entities generated by the frostbite pipeline will never be exportable
                            {
                                continue;
                            }
                        }
                    }
                    catch (RuntimeBinderException) { }

                    AssetClassGuid objGuid = obj.GetInstanceGuid();
                    networkedObjects.Add(new EbxImportReference() { FileGuid = asset.FileGuid, ClassGuid = objGuid.ExportedGuid });
                }
            }
            return networkedObjects;
        }

        /// <summary>
        /// Creates a new network registry.
        /// </summary>
        /// <param name="bundleId">Bundle to create the registry in.</param>
        private EbxAssetEntry CreateRegistry(int bundleId)
        {
            BundleEntry bentry = m_assetManager.GetBundleEntry(bundleId);
            string registryName;
            if (bentry.Type == BundleType.BlueprintBundle)
            {
                // Registry goes in the Win32 folder
                registryName = $"{bentry.Name}_networkregistry_Win32";
            }
            else
            {
                registryName = $"{bentry.Name.Replace("win32/", string.Empty)}_networkregistry_Win32";
            }
            App.Logger.Log("No registry exists for bundle {0}, creating new registry {1}", bentry.Name, registryName);
            // Create new ebx asset
            EbxAsset newRegistry = new EbxAsset(TypeLibrary.CreateObject("NetworkRegistryAsset"));
            newRegistry.SetFileGuid(Guid.NewGuid());

            dynamic obj = newRegistry.RootObject;
            obj.Name = registryName;

            AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newRegistry.Objects, (Type)obj.GetType(), newRegistry.FileGuid), -1);
            obj.SetInstanceGuid(guid);

            EbxAssetEntry newEntry = App.AssetManager.AddEbx(registryName, newRegistry);
            newEntry.AddedBundles.Add(bundleId);
            newEntry.ModifiedEntry.DependentAssets.AddRange(newRegistry.Dependencies);
            m_assetManager.ModifyEbx(registryName, m_assetManager.GetEbx(newEntry));

            return newEntry;
        }

        private void UpdateMeshVariationDB(int bundleId, Tuple<dynamic, List<EbxAssetEntry>> meshDbEntry)
        {
            bool updated = false;
            foreach (EbxAssetEntry dbAssetEntry in m_assetManager.EnumerateEbx(type: "MeshVariationDatabase"))
            {
                if (dbAssetEntry.IsInBundle(bundleId))
                {
                    EbxAsset dbAsset = m_assetManager.GetEbx(dbAssetEntry);
                    dynamic dbRoot = dbAsset.RootObject;
                    if (dbAsset.Dependencies.ToList().Contains(meshDbEntry.Item1.Mesh.External.FileGuid)) return;
                    dbRoot.Entries.Add(meshDbEntry.Item1);
                    foreach (EbxAssetEntry refEntry in meshDbEntry.Item2)
                    {
                        dbAsset.AddDependency(refEntry.Guid);
                    }
                    m_assetManager.ModifyEbx(dbAssetEntry.Name, dbAsset);
                    updated = true;
                }
            }
            if (!updated)
            {
                CreateMeshVariationDB(bundleId, meshDbEntry);
            }
        }

        private void UpdateMeshVariationDB(int bundleId, Tuple<dynamic, List<EbxAssetEntry>> variationDbEntry, EbxAssetEntry variationEntry)
        {
            bool updated = false;
            uint nameHash = ((dynamic)App.AssetManager.GetEbx(variationEntry).RootObject).NameHash;
            foreach (EbxAssetEntry dbAssetEntry in m_assetManager.EnumerateEbx(type: "MeshVariationDatabase"))
            {
                if (dbAssetEntry.IsInBundle(bundleId))
                {
                    EbxAsset dbAsset = m_assetManager.GetEbx(dbAssetEntry);
                    dynamic dbRoot = dbAsset.RootObject;
                    foreach (dynamic dbEntry in dbRoot.Entries)
                    {
                        if (dbEntry.VariationAssetNameHash == nameHash)
                        {
                            return;
                        }
                    }
                    dbRoot.Entries.Add(variationDbEntry.Item1);
                    foreach (EbxAssetEntry refEntry in variationDbEntry.Item2)
                    {
                        dbAsset.AddDependency(refEntry.Guid);
                    }
                    dbAsset.AddDependency(variationEntry.Guid);
                    m_assetManager.ModifyEbx(dbAssetEntry.Name, dbAsset);
                    updated = true;
                }
            }
            if (!updated)
            {
                CreateMeshVariationDB(bundleId, variationDbEntry, variationEntry);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private Tuple<dynamic, List<EbxAssetEntry>> GetMeshDbEntryForMesh(EbxAssetEntry entry)
        {
            bool foundMeshDBEntry = false;
            List<EbxAssetEntry> meshVariReferences = new List<EbxAssetEntry>();
            dynamic meshDbEntry = null;
            foreach (EbxAssetEntry meshvariEntry in m_assetManager.EnumerateEbx(type: "MeshVariationDatabase"))
            {
                if (meshvariEntry.DependentAssets.Contains(entry.Guid))
                {
                    dynamic meshvariRoot = m_assetManager.GetEbx(meshvariEntry).RootObject;
                    foreach (dynamic dbEntry in meshvariRoot.Entries)
                    {
                        if (dbEntry.Mesh.External.FileGuid == entry.Guid && dbEntry.VariationAssetNameHash == 0)
                        {
                            foundMeshDBEntry = true;
                            if (!IsBfn)
                            {
                                foreach (dynamic Material in dbEntry.Materials)
                                {
                                    foreach (dynamic texParam in Material.TextureParameters)
                                    {
                                        EbxAssetEntry texEntry = m_assetManager.GetEbxEntry(texParam.Value.External.FileGuid);
                                        if (texEntry != null)
                                        {
                                            if (!meshVariReferences.Contains(texEntry))
                                            {
                                                meshVariReferences.Add(texEntry);
                                            }
                                        }
                                    }
                                }
                            }
                            meshDbEntry = dbEntry;
                            break;
                        }
                    }
                }
                // Stop searching through DBs when an entry for the mesh is found
                if (foundMeshDBEntry == true)
                {
                    break;
                }
            }
            return new Tuple<dynamic, List<EbxAssetEntry>>(meshDbEntry, meshVariReferences);
        }

        private Tuple<dynamic, List<EbxAssetEntry>> GetMeshDbEntryForObjectVariation(EbxAssetEntry entry)
        {
            bool foundMeshDBEntry = false;
            List<EbxAssetEntry> meshVariReferences = new List<EbxAssetEntry>();
            dynamic meshDbEntry = null;
            uint nameHash = ((dynamic)App.AssetManager.GetEbx(entry).RootObject).NameHash;
            foreach (EbxAssetEntry meshvariEntry in m_assetManager.EnumerateEbx(type: "MeshVariationDatabase"))
            {
                if (meshvariEntry.DependentAssets.Contains(entry.Guid))
                {
                    dynamic meshvariRoot = m_assetManager.GetEbx(meshvariEntry).RootObject;
                    foreach (dynamic dbEntry in meshvariRoot.Entries)
                    {
                        if (dbEntry.VariationAssetNameHash == nameHash)
                        {
                            foundMeshDBEntry = true;

                            if (!IsBfn)
                            {
                                foreach (dynamic Material in dbEntry.Materials)
                                {

                                    foreach (dynamic texParam in Material.TextureParameters)
                                    {
                                        EbxAssetEntry texEntry = m_assetManager.GetEbxEntry(texParam.Value.External.FileGuid);
                                        if (texEntry != null)
                                        {
                                            if (!meshVariReferences.Contains(texEntry))
                                            {
                                                meshVariReferences.Add(texEntry);
                                            }
                                        }
                                    }
                                }
                            }

                            meshDbEntry = dbEntry;
                            break;
                        }
                    }
                }
                // Stop searching through DBs when an entry for the mesh is found
                if (foundMeshDBEntry == true)
                {
                    break;
                }
            }
            return new Tuple<dynamic, List<EbxAssetEntry>>(meshDbEntry, meshVariReferences);
        }

        public void CreateMeshVariationDB(int bundleId, Tuple<dynamic, List<EbxAssetEntry>> meshDbEntry)
        {
            BundleEntry bentry = m_assetManager.GetBundleEntry(bundleId);
            string dbName;
            if (bentry.Type == BundleType.BlueprintBundle)
            {
                dbName = $"{bentry.Name.Replace("win32", "Win32")}/MeshVariationDb_Win32";
            }
            else
            {
                dbName = $"{bentry.Name.Replace("win32/", string.Empty)}/MeshVariationDb_Win32";
            }
            App.Logger.Log("No MeshVariationDB exists for bundle {0}, creating new db {1}", bentry.Name, dbName);
            EbxAsset newDb = new EbxAsset(TypeLibrary.CreateObject("MeshVariationDatabase"));
            newDb.SetFileGuid(Guid.NewGuid());

            dynamic newDbRoot = newDb.RootObject;
            newDbRoot.Name = dbName;

            newDbRoot.Entries.Add(meshDbEntry.Item1);
            foreach (EbxAssetEntry refEntry in meshDbEntry.Item2)
            {
                newDb.AddDependency(refEntry.Guid);
            }

            AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newDb.Objects, (Type)newDbRoot.GetType(), newDb.FileGuid), -1);
            newDbRoot.SetInstanceGuid(guid);

            EbxAssetEntry newEntry = App.AssetManager.AddEbx(dbName, newDb);
            newEntry.AddedBundles.Add(bundleId);
            newEntry.ModifiedEntry.DependentAssets.AddRange(newDb.Dependencies);
            m_assetManager.ModifyEbx(dbName, m_assetManager.GetEbx(newEntry));
        }

        public void CreateMeshVariationDB(int bundleId, Tuple<dynamic, List<EbxAssetEntry>> meshDbEntry, EbxAssetEntry variationEntry)
        {
            BundleEntry bentry = m_assetManager.GetBundleEntry(bundleId);
            string dbName;
            if (bentry.Type == BundleType.BlueprintBundle)
            {
                dbName = $"{bentry.Name.Replace("win32", "Win32")}/MeshVariationDb_Win32";
            }
            else
            {
                dbName = $"{bentry.Name.Replace("win32/", string.Empty)}/MeshVariationDb_Win32";
            }
            App.Logger.Log("No MeshVariationDB exists for bundle {0}, creating new db {1}", bentry.Name, dbName);
            EbxAsset newDb = new EbxAsset(TypeLibrary.CreateObject("MeshVariationDatabase"));
            newDb.SetFileGuid(Guid.NewGuid());

            dynamic newDbRoot = newDb.RootObject;
            newDbRoot.Name = dbName;

            newDbRoot.Entries.Add(meshDbEntry.Item1);
            foreach (EbxAssetEntry refEntry in meshDbEntry.Item2)
            {
                newDb.AddDependency(refEntry.Guid);
            }
            newDb.AddDependency(variationEntry.Guid);

            AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newDb.Objects, (Type)newDbRoot.GetType(), newDb.FileGuid), -1);
            newDbRoot.SetInstanceGuid(guid);

            EbxAssetEntry newEntry = App.AssetManager.AddEbx(dbName, newDb);
            newEntry.AddedBundles.Add(bundleId);
            newEntry.ModifiedEntry.DependentAssets.AddRange(newDb.Dependencies);
            m_assetManager.ModifyEbx(dbName, m_assetManager.GetEbx(newEntry));
        }

        /// <summary>
        /// Returns true if the given asset can be loaded by the given bundle id.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="bundleId"></param>
        /// <returns></returns>
        public bool CanBeLoadedByBundle(EbxAssetEntry entry, int bundleId)
        {
            List<int> assetBundles = entry.Bundles.Concat(entry.AddedBundles).ToList();
            if (assetBundles.Contains(bundleId))
            {
                return true;
            }
            if(m_bundleAndParents.ContainsKey(bundleId))
            {
                var bundleLoadData = m_bundleAndParents[bundleId];
                foreach(int prevLoadId in bundleLoadData.PrevBundles)
                {
                    if(assetBundles.Contains(prevLoadId))
                    {
                        return true;
                    }
                }
            }
            foreach (int parentId in FindAllParentsOfBundle(bundleId))
            {
                if (assetBundles.Contains(parentId))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the given asset can be loaded by any of the given bundle ids.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="bundleIds"></param>
        /// <returns></returns>
        public bool CanBeLoadedByBundles(EbxAssetEntry entry, List<int> bundleIds)
        {
            foreach (int bundleId in bundleIds)
            {
                if (!CanBeLoadedByBundle(entry, bundleId))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Finds all parents of the given bundle id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public HashSet<int> FindAllParentsOfBundle(int id)
        {
            HashSet<int> allParentIds = new HashSet<int>();
            Stack<int> stack = new Stack<int>();

            // Add the given ID to the stack to start the search
            stack.Push(id);

            while (stack.Count > 0)
            {
                int currentId = stack.Pop();
                if (m_bundleAndParents.ContainsKey(currentId))
                {
                    var bundleLoadData = m_bundleAndParents[currentId];
                    foreach (int parentId in bundleLoadData.ParentBundles)
                    {
                        if (allParentIds.Contains(parentId)) continue;

                        allParentIds.Add(parentId);
                        stack.Push(parentId);
                    }
                    foreach(int prevId in bundleLoadData.PrevBundles)
                    {
                        if (allParentIds.Contains(prevId)) continue;

                        allParentIds.Add(prevId);
                        stack.Push(prevId);
                    }
                }
            }

            return allParentIds;
        }
        #endregion

        #region Cache Creation Methods

        private void CacheStaticBundles()
        {
            int settingsBundleId = m_assetManager.GetBundleId(IsBfn ? "Win32/default_settings" : "win32/default_settings_win32");        
            m_staticBundleIds.Add(settingsBundleId);
            m_bundleAndParents.Add(settingsBundleId, (new List<int>(), new List<int>()));
            int startupBundleId = m_assetManager.GetBundleId((IsBfn ? "Win32" : "win32") + "/systems/frostbitestartupdata");
            m_staticBundleIds.Add(startupBundleId);
            m_bundleAndParents.Add(startupBundleId, (new List<int>(), new List<int>() { settingsBundleId }));
            int configBundleId = m_assetManager.GetBundleId((IsBfn ? "Win32" : "win32") + "/gameplay/gameconfigurations/pvz_game");
            m_staticBundleIds.Add(configBundleId);
            m_bundleAndParents.Add(configBundleId, (new List<int>(), new List<int>() { settingsBundleId, startupBundleId }));

            if (!IsBfn)
            {
                int uiStaticBundleId = m_assetManager.GetBundleId("win32/_pvz/ui/flow/bundle/uidefaultstaticbundle");
                m_staticBundleIds.Add(uiStaticBundleId);
                m_bundleAndParents.Add(uiStaticBundleId, (new List<int>(), new List<int>() { settingsBundleId, startupBundleId, configBundleId }));
            }
            else
            {
                int installedGlobalSharedBId = m_assetManager.GetBundleId("Win32/gameplay/bundling/installed_globalsharedbundleasset");
                m_staticBundleIds.Add(installedGlobalSharedBId);
                m_bundleAndParents.Add(installedGlobalSharedBId, (new List<int>(), new List<int>() { settingsBundleId, startupBundleId, configBundleId }));

                int installedCharSharedBId = m_assetManager.GetBundleId("Win32/installchunks/installed_characterssharedbundleasset");
                m_staticBundleIds.Add(installedCharSharedBId);
                m_bundleAndParents.Add(installedCharSharedBId, (new List<int>(), new List<int>() { settingsBundleId, startupBundleId, configBundleId, installedGlobalSharedBId }));
            }
        }

        private void EnumerateSharedBundles()
        {
            WriteToLog("Enumerating Shared Bundles");
            foreach (EbxAssetEntry refEntry in m_assetManager.EnumerateEbx(type: "LevelDescriptionAsset"))
            {
                if (refEntry.IsAdded || refEntry.Name.Contains("Level_AssetScreenshots"))
                {
                    continue;
                }

                dynamic refRoot = m_assetManager.GetEbx(refEntry).RootObject;
                string levelName = refRoot.LevelName.ToString();
                List<int> prevLoadBundleIds = new List<int>();

                foreach (dynamic bundle in refRoot.Bundles)
                {
                    string bundleName = (IsBfn ? "Win32/" : "win32/") + bundle.Name.ToString().ToLower();
                    int bundleId = m_assetManager.GetBundleId(bundleName);
                    if(m_bundleAndParents.ContainsKey(bundleId))
                    {
                        prevLoadBundleIds.Add(bundleId);
                        continue;
                    }
                    if (bundleId == -1 || bundle.Name.ToString() == levelName)
                    {
                        continue;
                    }
                    m_bundleAndParents.Add(bundleId, (prevLoadBundleIds, m_staticBundleIds));
                    m_gameBundleIds.Add(bundleId);
                }
            }
            foreach (BundleEntry bundleEntry in m_assetManager.EnumerateBundles(BundleType.SharedBundle))
            {
                int bundleId = m_assetManager.GetBundleId(bundleEntry);
                if(m_bundleAndParents.ContainsKey(bundleId))
                {
                    continue;
                }
                m_bundleAndParents.Add(bundleId, (new List<int>(), m_staticBundleIds));
            }
        }

        private void EnumerateBlueprintBundles()
        {
            WriteToLog("Enumerating Blueprint Bundles");
            foreach(var bpbBundleEntry in m_assetManager.EnumerateBundles(BundleType.BlueprintBundle))
            {
                int bundleId = m_assetManager.GetBundleId(bpbBundleEntry);
                m_bundleAndParents.Add(bundleId, (new List<int>(), m_gameBundleIds));
            }
        }

        private void EnumerateLevels()
        {
            WriteToLog("Enumerating Levels");
            foreach (EbxAssetEntry refEntry in m_assetManager.EnumerateEbx(type: "LevelDescriptionAsset"))
            {
                if (refEntry.IsAdded || refEntry.Name.Contains("Level_AssetScreenshots"))
                {
                    continue;
                }

                dynamic refRoot = m_assetManager.GetEbx(refEntry).RootObject;
                string levelName = refRoot.LevelName.ToString();

                List<int> parents = new List<int>();
                if (IsBfn && levelName.Contains("DSub"))
                {
                    parents.Add(m_assetManager.GetBundleId("Win32/levels/level_picnic_root/level_picnic_root"));
                }
                List<int> prevLoads = new List<int>();

                WriteToLog($"Searching Level {levelName}");

                foreach (dynamic bundle in refRoot.Bundles)
                {
                    string bundleName = (IsBfn ? "Win32/" : "win32/") + bundle.Name.ToString().ToLower();
                    int bundleId = m_assetManager.GetBundleId(bundleName);
                    if (bundleId == -1 || bundle.Name.ToString() == levelName)
                    {
                        continue;
                    }
                    if(m_bundleAndParents.ContainsKey(bundleId))
                    {
                        prevLoads.Add(bundleId);
                    }
                }
                SearchLevelData(levelName, prevLoads, parents, false);
            }
        }

        private void SearchLevelData(string levelName, List<int> prevLoads, List<int> parents, bool subworldSearch)
        {
            WriteToLog(string.Format("Searching {0} {1}", subworldSearch == true ? "SubworldData" : "LevelData", levelName));
            string levelBundleName = (IsBfn ? "Win32/" : "win32/") + levelName.ToLower();

            int levelBundleId = m_assetManager.GetBundleId(levelBundleName);
            // Level_Hub_Tacobandits has 2 invalid subworlds
            if ((levelBundleId == -1 && subworldSearch) || m_bundleAndParents.ContainsKey(levelBundleId))
            {
                //App.Logger.Log("[Warning] Subworld doesn't exist, skipping");
                return;
            }

            m_bundleAndParents.Add(levelBundleId, (prevLoads, parents));
            List<int> newParents = new List<int> { levelBundleId };
            EbxAssetEntry refEntry = m_assetManager.GetEbxEntry(levelName);
            if (refEntry == null)
            {
                //App.Logger.Log(string.Format("Could not find {0} {1}", subworldSearch == true ? "SubworldData" : "LevelData", levelName));
                return;
            }
            EbxAsset refAsset = m_assetManager.GetEbx(refEntry);
            foreach (dynamic obj in refAsset.ExportedObjects)
            {
                if (obj.GetType().Name == "SubWorldReferenceObjectData")
                {
                    SearchLevelData(obj.BundleName.ToString().ToLower(), new List<int>(), newParents, true);
                }
            }
        }

        private void FindNetworkRegistryTypes()
        {
            WriteToLog("Searching for networked types");
            List<EbxAssetEntry> networkRegistries = m_assetManager.EnumerateEbx(type: "NetworkRegistryAsset").ToList();
            HashSet<Guid> processedRegistryGuids = new HashSet<Guid>();
            ConcurrentDictionary<Guid, EbxAsset> alreadyOpenedAssets = new ConcurrentDictionary<Guid, EbxAsset>();

            foreach (EbxAssetEntry networkRegistry in networkRegistries.Where(r => !processedRegistryGuids.Contains(r.Guid)))
            {
                WriteToLog("Searching {0}", networkRegistry.DisplayName);
                processedRegistryGuids.Add(networkRegistry.Guid);

                dynamic registryRoot = m_assetManager.GetEbx(networkRegistry).RootObject;
                ConcurrentDictionary<Guid, Type> processedTypes = new ConcurrentDictionary<Guid, Type>();
                ConcurrentBag<string> foundRegistryTypes = new ConcurrentBag<string>();

                var pointerRefs = (IEnumerable<PointerRef>)registryRoot.Objects;

                Parallel.ForEach(pointerRefs, pr =>
                {
                    if (pr.Type != PointerRefType.Null)
                    {
                        Guid classGuid = pr.External.ClassGuid;
                        Guid fileGuid = pr.External.FileGuid;

                        if (!processedTypes.TryGetValue(classGuid, out Type objectType))
                        {
                            EbxAsset dependency = null;
                            if (!alreadyOpenedAssets.ContainsKey(fileGuid))
                            {
                                dependency = m_assetManager.GetEbx(m_assetManager.GetEbxEntry(fileGuid));
                                alreadyOpenedAssets[fileGuid] = dependency;
                            }
                            else
                            {
                                dependency = alreadyOpenedAssets[fileGuid];
                            }

                            dynamic prObj = dependency.GetObject(classGuid);
                            objectType = prObj.GetType();
                            processedTypes[classGuid] = objectType;
                        }

                        string[] objType = objectType.ToString().Split('.');

                        if (!m_networkRegistryTypes.Contains(objType.Last()))
                        {
                            foundRegistryTypes.Add(objType.Last());
                        }
                    }
                });

                foreach (var registryType in foundRegistryTypes)
                {
                    m_foundRegistryTypes.Add(registryType);
                }
            }
        }

        #endregion

        #region Cache Methods

        private bool ReadFromCache()
        {
            if (!File.Exists(m_fs.CacheName + ".bundlecache"))
            {
                return false;
            }

            using (NativeReader reader = new NativeReader(new FileStream(m_fs.CacheName + ".bundlecache", FileMode.Open, FileAccess.Read)))
            {
                if (reader.ReadUInt() < CacheVersion)
                {
                    return false;
                }
                if (reader.ReadUInt() != CacheMagic)
                {
                    return false;
                }

                int staticBundleIdCount = reader.ReadInt();
                for (int i = 0; i < staticBundleIdCount; i++)
                {
                    m_staticBundleIds.Add(reader.ReadInt());
                }

                int gameBundleIdCount = reader.ReadInt();
                for (int i = 0; i < gameBundleIdCount; i++)
                {
                    m_gameBundleIds.Add(reader.ReadInt());
                }

                int bnpCount = reader.ReadInt();
                for (int i = 0; i < bnpCount; i++)
                {
                    int bundleId = reader.ReadInt();
                    int prevLoadsCount = reader.ReadInt();
                    List<int> prevLoads = new List<int>();
                    for (int j = 0; j < prevLoadsCount; j++)
                    {
                        prevLoads.Add(reader.ReadInt());
                    }
                    int parentCount = reader.ReadInt();
                    List<int> parents = new List<int>();
                    for (int k = 0; k < parentCount; k++)
                    {
                        parents.Add(reader.ReadInt());
                    }
                    m_bundleAndParents.Add(bundleId, (prevLoads, parents));
                }
            }

            if (File.Exists(NetworkRegistryTypesPath))
            {
                m_networkRegistryTypes = File.ReadAllLines(NetworkRegistryTypesPath).ToHashSet();
            }

            return true;
        }

        private void WriteCache()
        {
            string cacheName = m_fs.CacheName + ".bundlecache";
            using (NativeWriter writer = new NativeWriter(new FileStream(cacheName, FileMode.Create)))
            {
                writer.Write(CacheVersion);
                writer.Write(CacheMagic);

                writer.Write(m_staticBundleIds.Count);
                for (int i = 0; i < m_staticBundleIds.Count; i++)
                {
                    writer.Write(m_staticBundleIds[i]);
                }

                writer.Write(m_gameBundleIds.Count);
                for (int i = 0; i < m_gameBundleIds.Count; i++)
                {
                    writer.Write(m_gameBundleIds[i]);
                }

                writer.Write(m_bundleAndParents.Count);
                foreach (KeyValuePair<int, (List<int>, List<int>)> bundleAndLoadData in m_bundleAndParents)
                {
                    writer.Write(bundleAndLoadData.Key);
                    writer.Write(bundleAndLoadData.Value.Item1.Count);
                    foreach (int prevLoadId in bundleAndLoadData.Value.Item1)
                    {
                        writer.Write(prevLoadId);
                    }
                    writer.Write(bundleAndLoadData.Value.Item2.Count);
                    foreach (int parentId in bundleAndLoadData.Value.Item2)
                    {
                        writer.Write(parentId);
                    }
                }
            }

            using (NativeWriter typeWriter = new NativeWriter(new FileStream(NetworkRegistryTypesPath, FileMode.Append)))
            {
                foreach (string networkedType in m_foundRegistryTypes)
                {
                    typeWriter.WriteLine(networkedType);
                }
            }

            m_networkRegistryTypes.Concat(m_foundRegistryTypes);
            m_foundRegistryTypes.Clear();
        }

        #endregion
    }
}
