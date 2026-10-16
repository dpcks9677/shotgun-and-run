using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(EnemyChunkDistributionSO))]
public sealed class EnemyChunkDistributionSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var distribution = (EnemyChunkDistributionSO)target;
        EditorGUILayout.LabelField("비율 합계", distribution.PercentageTotal + "%");
        if (!distribution.TryCreateAssignments(0, out _, out string error))
            EditorGUILayout.HelpBox(error, MessageType.Error);
    }
}

[CustomEditor(typeof(EnemyChunkDistributor))]
public sealed class EnemyChunkDistributorEditor : Editor
{
    private static readonly string[] DifficultyNames = { "쉬움", "보통", "어려움", "매우 어려움" };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var distributor = (EnemyChunkDistributor)target;
        EnemyStandardSpawner[] spawners = distributor.GetSpawners();
        EditorGUILayout.LabelField("배분 대상", spawners.Length + "개");
        int[] counts = new int[DifficultyNames.Length];
        foreach (EnemyStandardSpawner spawner in spawners)
            if (spawner.ChunkPreset != null) counts[(int)spawner.ChunkPreset.Difficulty]++;
        for (int i = 0; i < counts.Length; i++)
            EditorGUILayout.LabelField(DifficultyNames[i], counts[i] + "개");

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (!GUILayout.Button("청크 배분 미리보기")) return;
            Undo.RecordObjects(spawners, "Distribute Enemy Chunks");
            if (!distributor.TryDistribute(out string error))
            {
                Debug.LogError(error, distributor);
                return;
            }
            foreach (EnemyStandardSpawner spawner in spawners)
            {
                EditorUtility.SetDirty(spawner);
                PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
            }
            EditorSceneManager.MarkSceneDirty(distributor.gameObject.scene);
        }
    }
}
