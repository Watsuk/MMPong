using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Génère un Font Asset TextMeshPro (SDF dynamique) à partir de space_invaders.ttf
/// puis l'applique à tous les textes TMP du GameObject "ScoreCanva" (labels d'équipe
/// qui contiennent aussi les scores).
///
/// S'exécute AUTOMATIQUEMENT au chargement / à la recompilation de l'éditeur tant que
/// l'application n'a pas réussi (aucun clic de menu requis). Le menu reste dispo pour
/// re-générer / ré-appliquer manuellement.
///
/// Pourquoi un script : un Font Asset TMP embarque un atlas + des métriques que seul
/// Unity sait construire ; on s'appuie sur l'API officielle TMP_FontAsset.CreateFontAsset.
/// </summary>
[InitializeOnLoad]
public static class SpaceInvadersFontSetup
{
    const string TtfPath = "Assets/Demos/Pong/Fonts/space_invaders.ttf";
    // Dans un dossier Resources pour être chargeable à l'exécution (menus du hub via UIFactory).
    const string FontAssetPath = "Assets/Demos/Pong/Fonts/Resources/space_invaders SDF.asset";
    const string ScoreCanvaName = "ScoreCanva";
    const string AppliedKey = "MMPong.SpaceInvaders.Applied";

    // Appelé à chaque rechargement de domaine (compilation, ouverture du projet…).
    static SpaceInvadersFontSetup()
    {
        if (EditorPrefs.GetBool(AppliedKey, false)) return; // déjà fait → on ne refait rien
        EditorApplication.delayCall += AutoRun;             // diffère après l'init de l'AssetDatabase
    }

    static void AutoRun()
    {
        var fontAsset = GetOrCreateFontAsset();
        if (fontAsset == null) return;

        // Applique seulement si le ScoreCanva est chargé dans une scène ouverte.
        // Sinon on laisse le flag à false : on réessaiera au prochain reload (ex. après
        // ouverture de la scène Pong).
        if (TryApplyToScoreCanva(fontAsset, autoSave: true))
            EditorPrefs.SetBool(AppliedKey, true);
        else
            Debug.Log($"[SpaceInvaders] Font Asset prêt. Ouvre la scène Pong : la police s'appliquera automatiquement (ou menu Tools ▸ MMPong).");
    }

    [MenuItem("Tools/MMPong/Police Space Invaders sur le ScoreCanva")]
    public static void GenerateAndApplyMenu()
    {
        var fontAsset = GetOrCreateFontAsset();
        if (fontAsset == null) return;

        if (TryApplyToScoreCanva(fontAsset, autoSave: true))
        {
            EditorPrefs.SetBool(AppliedKey, true);
        }
        else
        {
            Debug.LogError($"[SpaceInvaders] '{ScoreCanvaName}' introuvable. Ouvre la scène Pong puis relance ce menu.");
        }
    }

    [MenuItem("Tools/MMPong/Réinitialiser le flag police Space Invaders")]
    public static void ResetFlag()
    {
        EditorPrefs.DeleteKey(AppliedKey);
        Debug.Log("[SpaceInvaders] Flag réinitialisé : l'auto-application retentera au prochain reload.");
    }

    /// <summary>Crée le Font Asset SDF dynamique s'il n'existe pas, sinon le recharge.</summary>
    static TMP_FontAsset GetOrCreateFontAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null) return existing;

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[SpaceInvaders] Police introuvable : {TtfPath}");
            return null;
        }

        // Overload simple : 90pt, padding 9, SDFAA, atlas 1024², mode Dynamic.
        var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        if (fontAsset == null)
        {
            Debug.LogError("[SpaceInvaders] Échec de TMP_FontAsset.CreateFontAsset.");
            return null;
        }

        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        // L'atlas et le material doivent être stockés comme sous-assets du .asset.
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
        {
            fontAsset.atlasTextures[0].name = "space_invaders Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "space_invaders SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath);

        Debug.Log($"[SpaceInvaders] Font Asset créé : {FontAssetPath}");
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    /// <summary>Applique la police à tous les textes TMP enfants du ScoreCanva. Retourne false si le ScoreCanva n'est dans aucune scène ouverte.</summary>
    static bool TryApplyToScoreCanva(TMP_FontAsset fontAsset, bool autoSave)
    {
        var canvas = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == ScoreCanvaName);
        if (canvas == null) return false;

        var texts = canvas.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        foreach (var t in texts)
        {
            Undo.RecordObject(t, "Police Space Invaders");
            t.font = fontAsset; // met aussi à jour le material partagé vers celui de l'asset
            EditorUtility.SetDirty(t);
        }

        var scene = canvas.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (autoSave) EditorSceneManager.SaveScene(scene);

        Debug.Log($"[SpaceInvaders] Police appliquée à {texts.Length} texte(s) sous '{ScoreCanvaName}'" + (autoSave ? " (scène sauvegardée)." : "."));
        return true;
    }
}
