#if UNITY_EDITOR

using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

/// <summary>
/// SkeletonDataAsset을 기반으로 캐릭터 프리팹을 생성하는 에디터 도구입니다.
/// 계층, 렌더링, 플랫폼별 텍스처 설정을 같은 규칙으로 적용해 반복 작업을 줄입니다.
/// </summary>
public class SpinePrefabBuilder : EditorWindow
{
    private const string SourceRootPath = "Assets/SourceCharacters/";
    private const string PrefabRootPath = "Assets/CharacterPrefabs/";
    private const string PrefabGroupPrefix = "Group_";
    private const string SkeletonDataNameMarker = "_SkeletonData";
    private const string SkeletonDataFileSuffix = SkeletonDataNameMarker + ".asset";
    private const string SpineShaderName = "Spine/Skeleton Tint Black";
    private const string DefaultAnimationName = "IDLE";
    private const float DefaultCharacterScale = 0.24f;
    private const int DefaultSortingOrder = 65;
    private const float DefaultAnimationMix = 0.1f;
    private const float DefaultSkeletonDataScale = 1f;

    private static string characterFilter;

    [MenuItem("Tools/Spine Prefabs/Build Prefabs")]
    public static void OpenWindow()
    {
        EditorWindow.GetWindow(typeof(SpinePrefabBuilder));        
    }

    public void OnGUI()
    {
        GUILayout.Space(20);

        GUILayout.BeginVertical();
        GUILayout.Label("* Place skeleton data assets under " + SourceRootPath + "<GroupName>.");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Group filter : ", GUILayout.Width(100.0f));
        characterFilter = GUILayout.TextField(characterFilter, GUILayout.Width(160.0f));
        GUILayout.EndHorizontal();

        if(GUILayout.Button("Build Matching Prefabs"))
        {
            if(string.IsNullOrEmpty(characterFilter) == false)
                BuildMatchingPrefabs();                
        }
        GUILayout.EndVertical();

        GUILayout.Space(20);

        GUILayout.BeginVertical();
        GUILayout.Label("Compare skeleton data assets with existing prefabs and create missing prefabs.");

        if(GUILayout.Button("Build Missing Prefabs"))
        {
            BuildMissingPrefabs();
        }
        GUILayout.EndVertical();
    }

    /// <summary>
    /// SkeletonDataAsset 하나로 캐릭터를 구성하고 프리팹으로 저장합니다.
    /// 두 생성 방식에서 같은 생성 규칙을 사용하도록 공통 과정을 처리합니다.
    /// 실패한 루트는 정리하고, 저장에 성공한 오브젝트는 씬에 유지합니다.
    /// </summary>
    /// <param name="assetPath">SkeletonDataAsset의 경로입니다.</param>
    /// <param name="strResult">저장에 성공한 프리팹 경로를 누적합니다.</param>
    private static void CreatePrefab(string assetPath, ref string strResult)
    {
        if(string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("Skeleton data path is empty.");
            return;
        }

        // 파일 시스템 경로도 Unity 자산 경로와 같은 구분자를 사용하도록 합니다.
        if(assetPath.Contains("\\"))
            assetPath = assetPath.Replace("\\", "/");

        // 데이터 파일의 접미사를 제거해 오브젝트와 프리팹의 이름으로 사용합니다.
        string[] strSplit = assetPath.Split("/");
        string strObjName = strSplit[strSplit.Length - 1];
        strObjName = strObjName.Replace(SkeletonDataFileSuffix, "");

        Object objSkeletonData = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Spine.Unity.SkeletonDataAsset));

        if(objSkeletonData != null)
        {
            // 필수 셰이더가 없으면 오브젝트를 만들기 전에 중단합니다.
            Shader shader = Shader.Find(SpineShaderName);
            if(shader == null)
            {
                Debug.LogError("Required Spine shader was not found: " + assetPath);
                return;
            }

            GameObject go = null;
            bool isSaved = false;

            try
            {
                go = new GameObject(strObjName);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                // 생성 루트에 캐릭터 연동용 컴포넌트를 부착합니다.
                // 이 컴포넌트의 구현은 소스 제공 범위에 포함하지 않습니다.
                go.AddComponent<CharacterComponent>();

                // 표시용 계층을 분리해 루트의 기준 좌표와 별개로 표시 배율을 조정합니다.
                GameObject goAnchor = new GameObject("Anchor");
                goAnchor.transform.parent = go.transform;
                goAnchor.transform.localPosition = Vector3.zero;
                goAnchor.transform.localScale = new Vector3(DefaultCharacterScale, DefaultCharacterScale, 1.0f);

                GameObject goCharacter = new GameObject("Character");
                goCharacter.transform.parent = goAnchor.transform;
                goCharacter.transform.localPosition = Vector3.zero;
                goCharacter.transform.localScale = Vector3.one;

                // 데이터 연결 후 기본 애니메이션과 반복 재생을 설정합니다.
                // 대상 데이터에는 DefaultAnimationName에 지정된 애니메이션이 있어야 합니다.
                Spine.Unity.SkeletonAnimation skeletonAni = goCharacter.AddComponent<Spine.Unity.SkeletonAnimation>();
                skeletonAni.skeletonDataAsset = objSkeletonData as Spine.Unity.SkeletonDataAsset;
                skeletonAni.Initialize(false);
                skeletonAni.AnimationName = DefaultAnimationName;
                skeletonAni.loop = true;
                skeletonAni.tintBlack = true;

                // 이미 렌더러가 있으면 재사용해 동일 컴포넌트의 중복 추가를 피합니다.
                // AddMissingComponent는 NGUI의 확장 메서드입니다.
                MeshRenderer mesh = goCharacter.AddMissingComponent<MeshRenderer>();
                if(mesh == null)
                {
                    Debug.LogError("MeshRenderer was not created: " + assetPath);
                    return;
                }
                // 이 캐릭터 프리셋에서는 그림자, 프로브, 모션 벡터를 사용하지 않습니다.
                mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mesh.receiveShadows = false;

                mesh.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mesh.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mesh.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                // 렌더링 순서에 사용하는 기본 프리셋 값입니다.
                mesh.sortingOrder = DefaultSortingOrder;

                // 머티리얼의 셰이더 설정은 별도 보조 함수에 위임합니다.
                MaterialSetupUtility.ApplyShader(skeletonAni, shader);

                // 애니메이션 전환 시간과 데이터 스케일의 기본값을 설정합니다.
                // 새 복사본이 아닌 입력 SkeletonDataAsset의 설정을 변경합니다.
                Spine.Unity.SkeletonDataAsset skAsset = objSkeletonData as Spine.Unity.SkeletonDataAsset;
                skAsset.defaultMix = DefaultAnimationMix;
                skAsset.scale = DefaultSkeletonDataScale;

                // 실제 텍스처와 임포터가 있어야 플랫폼별 설정을 적용할 수 있습니다.
                // 아래 검증에서 실패하면 이번에 만든 루트를 정리하고 중단합니다.
                if(ConfigureTextureImporter(mesh, assetPath) == false)
                    return;

                // 이름의 첫 번째 '_' 앞부분으로 출력 폴더를 묶어 그룹별로 관리합니다.
                string[] strName = strObjName.Split("_");
                string folderPath = string.Format("{0}{1}{2}", PrefabRootPath, PrefabGroupPrefix, strName[0].ToLower());
                if(Directory.Exists(folderPath) == false)
                {
                    Directory.CreateDirectory(folderPath);
                }

                string prefabPath = string.Format("{0}/{1}.prefab", folderPath, strObjName);
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction, out bool isSuccess);

                // 저장 실패가 성공 목록에 섞이지 않도록 결과를 확인합니다.
                if(isSuccess == false)
                {
                    Debug.LogError("Failed to save prefab: " + prefabPath);
                    return;
                }

                isSaved = true;
                Debug.Log(string.Format("Success New Prefab : {0}", prefabPath));
                // 성공한 경로만 누적하며, 성공한 오브젝트는 기존처럼 씬에 유지합니다.
                strResult += prefabPath + ("\n");
            }
            catch(System.Exception exception)
            {
                Debug.LogError(string.Format(
                    "Failed to create prefab from {0}: {1}", assetPath, exception));
            }
            finally
            {
                // 저장한 오브젝트는 씬에 유지하고, 실패하거나 예외가 난 루트만 정리합니다.
                if(isSaved == false && go != null)
                    Object.DestroyImmediate(go);
            }
        }
        else
        {
            Debug.LogError("SkeletonDataAsset was not found: " + assetPath);
        }
        
    }

    /// <summary>
    /// 렌더러의 텍스처와 임포터를 확인하고 플랫폼별 압축 설정을 적용합니다.
    /// 텍스처 검증과 설정을 분리해 프리팹 생성 흐름을 간결하게 유지합니다.
    /// </summary>
    /// <param name="mesh">텍스처를 확인할 캐릭터 렌더러입니다.</param>
    /// <param name="assetPath">오류 메시지에 표시할 SkeletonDataAsset의 경로입니다.</param>
    /// <returns>필수 항목을 확인하고 설정을 적용했으면 true입니다.</returns>
    private static bool ConfigureTextureImporter(MeshRenderer mesh, string assetPath)
    {
        if(mesh.sharedMaterial == null)
        {
            Debug.LogError("Character material is missing: " + assetPath);
            return false;
        }

        Texture2D _texture = mesh.sharedMaterial.mainTexture as Texture2D;
        if(_texture == null)
        {
            Debug.LogError("Character Texture2D is missing: " + assetPath);
            return false;
        }

        string texturePath = AssetDatabase.GetAssetPath(_texture);
        TextureImporter _txImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if(_txImporter == null)
        {
            Debug.LogError("TextureImporter was not found: " + texturePath);
            return false;
        }

        _txImporter.textureType = TextureImporterType.Default;
        _txImporter.textureShape = TextureImporterShape.Texture2D;
        _txImporter.sRGBTexture = true;

        // 플랫폼별 기본값에 맡기지 않고 ASTC 4x4 압축 프리셋을 명시합니다.
        TextureImporterPlatformSettings androidSetting = _txImporter.GetPlatformTextureSettings("Android");
        if(androidSetting != null)
        {
            androidSetting.overridden = true;
            androidSetting.format = TextureImporterFormat.ASTC_4x4;
            _txImporter.SetPlatformTextureSettings(androidSetting);
        }

        TextureImporterPlatformSettings iosSetting = _txImporter.GetPlatformTextureSettings("iPhone");
        if(iosSetting != null)
        {
            iosSetting.overridden = true;
            iosSetting.format = TextureImporterFormat.ASTC_4x4;
            _txImporter.SetPlatformTextureSettings(iosSetting);
        }

        return true;
    }

    /// <summary>
    /// 이름에 검색어가 포함된 폴더와 하위 폴더의 데이터로 프리팹을 생성합니다.
    /// 필요한 그룹만 선택할 때 사용하며, 같은 경로의 기존 프리팹을 덮어쓸 수 있습니다.
    /// </summary>
    private static void BuildMatchingPrefabs()
    {
        string strRootPath = SourceRootPath;
        if(Directory.Exists(strRootPath) == false)
        {
            Debug.LogError("Skeleton data folder was not found: " + strRootPath);
            return;
        }

        string[] assetDirectories = Directory.GetDirectories(strRootPath, "*.*", SearchOption.AllDirectories);
        // 상위·하위 폴더가 모두 검색되어도 같은 자산은 한 번만 처리합니다.
        HashSet<string> processedAssetPaths = new HashSet<string>(System.StringComparer.Ordinal);
        for(int idx = 0; idx < assetDirectories.Length; ++idx)
        {
            string folderPath = assetDirectories[idx];
            folderPath = folderPath.Replace("\\", "/");

            string[] strSplit = folderPath.Split("/");
            string strObjName = strSplit[strSplit.Length - 1];

            // 전체 경로가 아닌 현재 폴더 이름에 필터를 적용합니다.
            if(strObjName.Contains(characterFilter))
            {
                string strResult = "";
                string[] assetFiles = Directory.GetFiles(folderPath, "*.asset", SearchOption.AllDirectories);
                for(int idy = 0; idy < assetFiles.Length; ++idy)
                {
                    string assetName = assetFiles[idy];
                    if(assetName.Contains(SkeletonDataNameMarker))
                    {
                        string normalizedAssetPath = assetName.Replace('\\', '/');
                        if(processedAssetPaths.Add(normalizedAssetPath) == false)
                            continue;

                        CreatePrefab(assetName, ref strResult);
                    }
                }
                EditorUtility.DisplayDialog("Notice", strResult, "OK");
            }
        }
    }

    /// <summary>
    /// 검색어와 관계없이 지정 경로의 전체 데이터를 기존 프리팹 이름과 비교합니다.
    /// SkeletonDataAsset은 있지만 대응하는 프리팹이 없는 항목만 이름순으로 생성합니다.
    /// </summary>
    private static void BuildMissingPrefabs()
    {
        // 기존 프리팹 이름을 수집해 이후 누락 여부 판단에 사용합니다.
        List<string> characterPrefabNames = new List<string>();
        string strAssetBundlePath = PrefabRootPath;
        if(Directory.Exists(strAssetBundlePath) == false)
        {
            Debug.LogError("Prefab folder was not found: " + strAssetBundlePath);
            return;
        }
        string[] bundleDirectories = Directory.GetDirectories(strAssetBundlePath, PrefabGroupPrefix + "*", SearchOption.AllDirectories);
        foreach(string bundlePath in bundleDirectories)
        {
            string[] prefabPath = Directory.GetFiles(bundlePath, "*.prefab", SearchOption.AllDirectories);
            foreach(string path in prefabPath)
            {
                string[] strSplit = path.Replace("\\", "/").Split("/");
                string name = strSplit[strSplit.Length - 1];
                name = name.Replace(".prefab", "");
                // 비교할 두 이름을 같은 형태로 맞춰 대소문자 차이를 무시합니다.
                characterPrefabNames.Add(name.ToLower());
            }
        }

        // 누락 대상을 먼저 수집해 이름순으로 생성합니다.
        List<string> createList = new List<string>();

        string strRootPath = SourceRootPath;
        if(Directory.Exists(strRootPath) == false)
        {
            Debug.LogError("Skeleton data folder was not found: " + strRootPath);
            return;
        }
        string[] assetPaths =  Directory.GetFiles(strRootPath, "*.asset", SearchOption.AllDirectories);
        foreach(string assetPath in assetPaths)
        {
            if(assetPath.Contains(SkeletonDataNameMarker))
            {
                string strPath = assetPath.Replace("\\", "/");
                string[] strSplit = strPath.Split("/");

                string name = strSplit[strSplit.Length - 1];
                name = name.Replace(SkeletonDataFileSuffix, "");

                // 파일 경로가 아닌 대응하는 프리팹 이름의 존재 여부로 판정합니다.
                if(characterPrefabNames.Contains(name.ToLower())== false)
                {
                    createList.Add(strPath);
                }
            }
        }

        // 탐색 순서 대신 파일 이름순으로 생성해 결과를 확인하기 쉽게 합니다.
        createList.Sort((string left, string right) => 
        {
            string[] leftSplit = left.Split("/");
            string leftName = leftSplit[leftSplit.Length - 1];
            string[] rightSplit = right.Split("/");
            string rightName = rightSplit[rightSplit.Length - 1];

            return string.Compare(leftName, rightName);
        });

        string strResult = "";
        foreach(string strPath in createList)
        {
            CreatePrefab(strPath, ref strResult);
        }

        EditorUtility.DisplayDialog("Notice", strResult, "OK");
    }
}

#endif
