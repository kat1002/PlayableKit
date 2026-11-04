#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class CleanerTool : EditorWindow
{
    [MenuItem("Tools/MyLunaHelper/Force Remove All AssetBundles")]
    public static void RemoveAllAssetBundles()
    {
        // Lấy danh sách tất cả các tên bundle đang tồn tại trong project
        string[] assetBundleNames = AssetDatabase.GetAllAssetBundleNames();

        if (assetBundleNames.Length == 0)
        {
            EditorUtility.DisplayDialog("Thông báo", "Project sạch sẽ! Không tìm thấy file nào dính AssetBundle tag.", "OK");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Phát hiện AssetBundle!",
            $"Tìm thấy {assetBundleNames.Length} nhóm bundle đang tồn tại.\n\nĐiều này làm Luna bị lỗi build.\nBạn có muốn gỡ tag tất cả không?",
            "Gỡ sạch cho tao",
            "Để xem lại"
        );

        if (!confirm) return;

        int count = 0;
        foreach (var bundleName in assetBundleNames)
        {
            // Lấy danh sách file trong bundle đó
            string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);

            foreach (var assetPath in assetPaths)
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (importer != null)
                {
                    // Gỡ bỏ tag
                    importer.assetBundleName = "";
                    importer.assetBundleVariant = "";
                    count++;
                    Debug.Log($"🧹 Đã gỡ bundle tag khỏi file: {assetPath}");
                }
            }

            // Xóa luôn cái tên bundle khỏi danh sách quản lý của Unity
            AssetDatabase.RemoveAssetBundleName(bundleName, true);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Hoàn tất", $"Đã gỡ tag khỏi {count} files. Giờ project đã sạch!", "Ngon");
    }
}
#endif