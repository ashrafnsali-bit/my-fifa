#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Football.Editor
{
    public static class SceneRebuilder
    {
        [MenuItem("FIFA/Force Rebuild Entire Stadium Scene")]
        public static void RebuildOnce()
        {
            if (EditorApplication.isPlaying) return;

            Debug.Log("<color=yellow>[FIFA World Cup Engine]</color> Force Rebuilding upgraded stadium graphics, pitch markings, LED boards, and player animations...");
            MatchBootstrapper.EditorSetupMatch();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("<color=green>[FIFA World Cup Engine]</color> Stadium Scene successfully saved with all new graphics and animations!");
            }
        }
    }
}
#endif
