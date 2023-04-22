using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LayerManager {

    /// <summary>
    /// Array containing all built-in layers used by the game
    /// </summary>
    private static readonly string[] RISING_WORLD_LAYERS = new string[] {
        "Default",
        "TransparentFX",
        "Ignore Raycast",
        "Trigger",
        "Water",
        "UI",
        "Area",
        "",
        "Terrain",
        "Grass",
        "World",
        "Vegetation",
        "VegetationInteraction",
        "Construction",
        "TransparentConstruction",
        "Object",
        "ObjectInteraction",
        "",
        "",
        "LocalPlayer",
        "RemotePlayer",
        "Npc",
        "",
        "Item",
        "Vehicle",
        "Corpse",
        "Debris",
        "Decal",
        "Ladder",
        "Misc",
        "Selector",
        "Mask"
    };

    static LayerManager() {
        if (!SessionState.GetBool("ValidateLayersSessionState", false)) {
            SessionState.SetBool("ValidateLayersSessionState", true);
            EditorApplication.delayCall += ValidateLayers;
        }
    }

    [MenuItem("PluginSDK/Validate Layers")]
    public static void ValidateLayersMenuItem() {
        ValidateLayers();
    }

    /// <summary>
    /// Goes through all layers defined in this project and check if they match the Rising World layers
    /// </summary>
    private static void ValidateLayers() {
        //Load tag manager asset from project settings
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAssetAtPath("ProjectSettings/TagManager.asset", typeof(UnityEngine.Object)));

        //Find the "layers" property and check if it's an array
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray) {
            Debug.LogWarning("Layer validation failed!");
            return;
        }

        //Go through all layers and check if their names match our layers
        bool needFix = false;
        StringBuilder sb = new StringBuilder(256);
        for (int i = 0; i < layers.arraySize; i++) {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!layer.stringValue.Equals(RISING_WORLD_LAYERS[i])) {
                needFix = true;
            }

            if (!string.IsNullOrEmpty(layer.stringValue) || !string.IsNullOrEmpty(RISING_WORLD_LAYERS[i])) {
                sb.Append('[').Append(i).Append("] ").Append(layer.stringValue).Append(" --> ").Append(RISING_WORLD_LAYERS[i]).Append('\n');
            }
        }

        //If there is a mismatch, show prompt to user and ask if the layers should be fixed now
        if (needFix && EditorUtility.DisplayDialog("Fix Layers", "Some layers are not correct! \n" + sb.ToString(), "Fix now", "Do not fix")) {
            for (int i = 0; i < layers.arraySize; i++) {
                SerializedProperty layerSP = layers.GetArrayElementAtIndex(i);
                Debug.LogWarning("Rename layer " + i + " from " + layerSP.stringValue + " to " + RISING_WORLD_LAYERS[i]);
                layerSP.stringValue = RISING_WORLD_LAYERS[i];
            }
            tagManager.ApplyModifiedProperties();
        }
        else {
            Debug.Log("All layers validated!");
        }

    }
}
