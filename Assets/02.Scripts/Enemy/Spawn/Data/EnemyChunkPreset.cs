using UnityEngine;

public enum EnemyChunkDifficulty
{
    Easy,
    Normal,
    Hard,
    VeryHard
}

[CreateAssetMenu(fileName = "EnemyChunkPreset", menuName = "ShotgunAndRun/Enemy/Chunk Preset")]
public class EnemyChunkPreset : ScriptableObject
{
    [SerializeField] private EnemyChunk _chunk = new EnemyChunk();
    [SerializeField] private EnemyChunkDifficulty _difficulty;

    public EnemyChunk Chunk => _chunk;
    public EnemyChunkDifficulty Difficulty => _difficulty;
}
