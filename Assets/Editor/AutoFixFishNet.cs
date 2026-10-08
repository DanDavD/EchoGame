using UnityEditor;
using System.Reflection;
using System.Linq;
using UnityEngine;

public class AutoFixFishNet
{
    [MenuItem("EchoProtocol/Fix FishNet IDs")]
    public static void Fix()
    {
        var assembly = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FishNet.Editor");
        if (assembly != null)
        {
            var type = assembly.GetType("FishNet.Configuring.ReserializeNetworkObjectsEditor");
            if (type != null)
            {
                var method = type.GetMethod("ReserializeNetworkObjects", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (method != null)
                {
                    method.Invoke(null, null);
                    Debug.Log("[AutoFix] FishNet NetworkObjects Reserialized.");
                }
                else Debug.Log("[AutoFix] ReserializeNetworkObjects method not found.");
            }
            else Debug.Log("[AutoFix] ReserializeNetworkObjectsEditor type not found.");
        }
        else Debug.Log("[AutoFix] FishNet.Editor assembly not found.");

        // Force save assets
        AssetDatabase.SaveAssets();
    }
}
