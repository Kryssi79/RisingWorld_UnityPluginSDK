using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class AssetBundleBuilder {

    /// <summary> Our build target for the asset bundles (Windows, Linux and Mac) </summary>
    private static readonly BuildTarget[] BUILD_TARGETS = new BuildTarget[] { BuildTarget.StandaloneWindows, BuildTarget.StandaloneLinux64, BuildTarget.StandaloneOSX };

    /// <summary> Header that's always written into the asset bundle file (so we can find out that it's a multi-platform bundle) </summary>
    private static readonly byte[] HEADER = Encoding.UTF8.GetBytes("RisingWorld");

    /// <summary> Current version of our multi-platform bundles </summary>
    private const byte VERSION = 1;


    [MenuItem("PluginSDK/Build Asset Bundles")]
    public static void BuildAssetBundlesMenuItem() {
        BuildAssetBundles();
    }

    /// <summary>
    /// Builds a multi-platform asset bundle (supporting all build targets) based on the content
    /// in the "Assets/AssetBundles" folder
    /// </summary>
    private static void BuildAssetBundles() {
        //Get all folders in Assets/AssetBundles
        string[] directories = Directory.GetDirectories(Application.dataPath + "/AssetBundles");

        //We create a new asset bundle per folder (!)
        AssetBundleBuild[] assetBundles = new AssetBundleBuild[directories.Length];

        //Go through all folders to generate the according asset bundle data
        for (int i = 0; i < directories.Length; i++) {
            assetBundles[i] = new AssetBundleBuild();

            List<string> addresses = new List<string>(512);
            List<string> paths = new List<string>(512);

            //Get all files in the folder recursively (these files will be stored in the asset bundles)
            string[] files = Directory.GetFiles(directories[i], "*.*", SearchOption.AllDirectories);
            //Go through the files, update their paths and names
            foreach (string f in files) {
                string filename = f.Substring(f.IndexOf("Assets/AssetBundles")).Replace('\\', '/');
                addresses.Add(filename.Substring(19 + 1));
                paths.Add(filename);
            }

            //Store all paths and addresses, also determine asset bundle name by folder name
            assetBundles[i].assetNames = paths.ToArray();
            assetBundles[i].addressableNames = addresses.ToArray();
            assetBundles[i].assetBundleName = Path.GetFileName(directories[i]).ToLower() + ".bundle";

            Debug.Log(assetBundles[i].assetBundleName + ": " + assetBundles[i].assetNames.Length + " assets");
        }

        //Check if target directory exists
        string targetFolder = Directory.GetParent(Application.dataPath).FullName + "/Build";
        if (!Directory.Exists(targetFolder)) {
            Directory.CreateDirectory(targetFolder);
        }

        //Go through all build targets
        foreach (BuildTarget buildTarget in BUILD_TARGETS) {
            //Create target folder for each build target
            string targetFolderPlatform = targetFolder + "/" + buildTarget.ToString();
            if (!Directory.Exists(targetFolderPlatform)) {
                Directory.CreateDirectory(targetFolderPlatform);
            }

            //Set target graphics APIs.
            //On Windows, we're supporting DirectX11, DirectX12 and Vulkan
            if (buildTarget == BuildTarget.StandaloneWindows) {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows, new GraphicsDeviceType[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan });
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows, false);
            }
            //On linux, we only support Vulkan
            else if (buildTarget == BuildTarget.StandaloneLinux64) {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new GraphicsDeviceType[] { GraphicsDeviceType.Vulkan });
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            }
            //On Mac, we only support Metal
            else if (buildTarget == BuildTarget.StandaloneOSX) {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new GraphicsDeviceType[] { GraphicsDeviceType.Metal });
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
            }
            //Else we fallback to the default graphics APIs
            else {
                PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, true);
            }

            //Set build options
            BuildAssetBundleOptions buildOptions = BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.AssetBundleStripUnityVersion;

            //Build all asset bundles
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(targetFolderPlatform, assetBundles, buildOptions, buildTarget);

            //If manifest file is null, something went wrong
            if (manifest == null) {
                Debug.LogError("BUILDING ASSET BUNDLES FAILED!");
                return;
            }
        }


        //Go through all asset bundles and merge the data into a single file. We add a specific header at the beginning
        //so we can distinguish default asset bundles and multi-platform bundles
        for (int i = 0; i < assetBundles.Length; i++) {
            AssetBundleBuild assetBundle = assetBundles[i];

            //RisingWorld PluginSDK AssetBundle format:
            //"RisingWorld" (x) + version (1) + position windows (4) + length windows (4) + position linux (4) + length linux (4) + position osx (4) + length osx (4) + data windows (x) + data linux (x) + data osx (x)

            //Get minium required file size (header)
            int fileSize = HEADER.Length + 1 * BUILD_TARGETS.Length * 8;
            byte[][] files = new byte[BUILD_TARGETS.Length][];

            //Go through all build targets, get the according asset bundle and read the file content
            for (int y = 0; y < BUILD_TARGETS.Length; y++) {
                BuildTarget buildTarget = BUILD_TARGETS[y];

                string targetFolderPlatform = targetFolder + "/" + buildTarget.ToString();
                if (!File.Exists(targetFolderPlatform + "/" + assetBundle.assetBundleName)) {
                    Debug.LogError("BUILDING ASSET BUNDLES FAILED!");
                    return;
                }

                //Read asset bundle bytes and increment the file size accordingly
                files[y] = File.ReadAllBytes(targetFolderPlatform + "/" + assetBundle.assetBundleName);
                fileSize += 4;
                fileSize += files[y].Length;
            }

            //Create new byte array for our final asset bundle
            byte[] file = new byte[fileSize];
            int index = 0;

            //Write bundle header
            Array.Copy(HEADER, 0, file, index, HEADER.Length);
            index = HEADER.Length;

            //Write version
            file[index++] = VERSION;

            //Write file positions
            int position = HEADER.Length + 1 + BUILD_TARGETS.Length * 8;
            for (int y = 0; y < BUILD_TARGETS.Length; y++) {
                //Write position
                Array.Copy(BitConverter.GetBytes(position), 0, file, index, 4);
                index += 4;

                //Write size (number of bytes)
                Array.Copy(BitConverter.GetBytes(files[y].Length), 0, file, index, 4);
                index += 4;

                //Copy data
                Array.Copy(files[y], 0, file, position, files[y].Length);
                position += files[y].Length;
            }

            //Write all bytes into our asset bundle file
            File.WriteAllBytes(targetFolder + "/" + assetBundle.assetBundleName, file);
        }

        //Cleanup unused folders
        for (int y = 0; y < BUILD_TARGETS.Length; y++) {
            BuildTarget buildTarget = BUILD_TARGETS[y];
            string targetFolderPlatform = targetFolder + "/" + buildTarget.ToString();
            Directory.Delete(targetFolderPlatform, true);
        }
    }
}
