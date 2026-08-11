using UnityEngine;
using Cysharp.Threading.Tasks;
using YooAsset;
using UnityEngine.SceneManagement;

public class Boot : MonoBehaviour
{
    // Start is called before the first frame update
    private void Start()
    {
        LoadMainScene().Forget();
    }

    private async UniTaskVoid LoadMainScene()
    {
        Debug.Log("开始加载主场景: MainScene");
        const string location = "Assets/Res/Scenes/MainScene";
        const LocalPhysicsMode physicsMode = LocalPhysicsMode.None;
        var package = YooAssets.GetPackage("DefaultPackage");
        SceneHandle handle = package.LoadSceneAsync(location, LoadSceneMode.Single, physicsMode, true);
        await handle.ToUniTask();
        
        Debug.Log("主场景加载完毕");
    }
}
