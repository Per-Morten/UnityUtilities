using UnityEditor;
using UnityEngine;

public class RefreshAndPlayButton 
    : EditorWindow
{
    [MenuItem("Open Refresh and Play Button Window", priority = 1)]
    public static void Init()
    {
        var window = EditorWindow.GetWindow<RefreshAndPlayButton>();
        window.titleContent.text = "Refresh and Play";
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh and Play"))
            {
                AssetDatabase.Refresh(ImportAssetOptions.Default);
                EditorApplication.EnterPlaymode();
            }
        }
    }
}
