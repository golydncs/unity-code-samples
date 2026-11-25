#if UNITY_EDITOR

using UnityEditor;

using System.IO;
using System.Collections.Generic;
using UnityEngine;

using Spine.Unity;

/// <summary>
/// 프리팹의 SkeletonAnimation에서 SkeletonDataAsset 참조 누락을 찾습니다.
/// 결과를 에디터 창에 표시해 확인할 자산을 모아 볼 수 있게 합니다.
/// </summary>
public class MissingSkeletonDataScanner : EditorWindow 
{
    public List<GameObject> missingDataPrefabs = new List<GameObject>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Find Missing Skeleton Data")]
    public static void OpenWindow()
    {
        GetWindow(typeof(MissingSkeletonDataScanner));
    }

    public void OnGUI()
    {
        GUILayout.Space(10);

        GUILayout.Label("Find prefabs with missing SkeletonDataAsset references.", GUILayout.Width(300.0f));
               
        if (GUILayout.Button("Find", GUILayout.Width(100.0f)))
        {
            // 재검사 시 이전 결과가 누적되지 않도록 초기화합니다.
            missingDataPrefabs.Clear();
            FindMissingSkeletonData(missingDataPrefabs);
        }

        GUILayout.Space(15);

        GUILayout.Label(string.Format("Result : {0}", missingDataPrefabs.Count));
        GUILayout.Label("-------------------");

        GUILayout.Space(5);

        // 별도 복사 목록 없이 검사 결과를 오브젝트 필드로 직접 표시합니다.
        // 버튼과 결과 개수는 고정하고 목록만 스크롤합니다.
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        for (int idx = 0; idx < missingDataPrefabs.Count; ++idx)
        {
            EditorGUILayout.ObjectField(missingDataPrefabs[idx], typeof(GameObject), false, GUILayout.Width(150.0f));
        }
        GUILayout.EndScrollView();
    }

    /// <summary>
    /// 지정 폴더와 하위 폴더의 프리팹을 순회해 SkeletonDataAsset 참조 누락을 수집합니다.
    /// 각 프리팹의 첫 번째 SkeletonAnimation을 검사하며, 비활성 자식은 제외합니다.
    /// 검사 결과를 목록으로 전달해 화면 표시와 검사 로직을 분리합니다.
    /// </summary>
    /// <param name="results">결과를 추가할 목록입니다. 기존 항목은 지우지 않습니다.</param>
    public static void FindMissingSkeletonData(List<GameObject> results)
    {
        if(results == null)
        {
            Debug.LogError("A result list is required.");
            return;
        }

        DirectoryInfo dir = new DirectoryInfo(Application.dataPath + "/CharacterPrefabs");
        if(dir.Exists == false)
        {
            Debug.LogError("Prefab folder was not found: " + dir.FullName);
            return;
        }
        FileInfo[] files = dir.GetFiles("*.prefab", SearchOption.AllDirectories);

        // 상위 폴더 이름에도 Assets가 포함될 수 있으므로 실제 Assets 루트를 기준으로 합니다.
        // 끝에 '/'를 붙여 폴더 경계까지 포함해 비교합니다.
        string assetsPath = Path.GetFullPath(Application.dataPath)
            .Replace('\\', '/').TrimEnd('/') + "/";
        for (int idx = 0; idx < files.Length; ++idx)
        {
            string fullPath = files[idx].FullName.Replace('\\', '/');
            if(fullPath.StartsWith(assetsPath, System.StringComparison.OrdinalIgnoreCase) == false)
            {
                Debug.LogError("Prefab path is outside the Assets folder: " + fullPath);
                continue;
            }

            // 파일 시스템의 절대 경로를 AssetDatabase가 사용하는 경로로 변환합니다.
            string prefabPath = "Assets/" + fullPath.Substring(assetsPath.Length);
            GameObject goPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(goPrefab != null)
            {
                // 기본 검색은 루트를 포함하고 비활성 자식은 제외합니다.
                // 모든 컴포넌트가 아닌 첫 번째 검색 결과만 검사합니다.
                SkeletonAnimation _skeleton = goPrefab.GetComponentInChildren<SkeletonAnimation>();
                if(_skeleton != null)
                {
                    // 컴포넌트는 있지만 데이터 참조가 없는 경우를 수집합니다.
                    // SkeletonAnimation 자체가 없는 프리팹은 검사 대상이 아닙니다.
                    if(_skeleton.SkeletonDataAsset == null)
                    {
                        results.Add(goPrefab);
                        Debug.Log(string.Format("skeletonDataAsset is null : {0}", prefabPath));
                    }
                }
            }
            else
            {
                Debug.LogWarning("Failed to load prefab: " + prefabPath);
            }
        }
        Debug.Log("End");
    }
}

#endif
