using UnityEngine;
using UnityEditor;

public class FindAssetByID : EditorWindow
{
    string assetID = "";

    [MenuItem("Tools/MyLunaHelper/Find Asset By ID")]
    public static void ShowWindow()
    {
        GetWindow<FindAssetByID>("Find Asset");
    }

    void OnGUI()
    {
        assetID = EditorGUILayout.TextField("Nhập Asset ID từ Log:", assetID);
        if (GUILayout.Button("Tìm Asset"))
        {
            if (int.TryParse(assetID, out int id))
            {
                // Tìm object trong database
                Object obj = EditorUtility.InstanceIDToObject(id);
                if (obj != null)
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    Debug.Log("Đã tìm thấy Asset: " + obj.name);
                }
                else
                {
                    Debug.LogError("Không tìm thấy Asset với ID này trong phiên làm việc hiện tại.");
                }
            }
        }
    }
}