using UnityEngine;

[System.Serializable]
public class EnemyData
{
    // Enemy 객체의 고정 기본 속성
    public EnemyType enemyType;
    public Enemy prefab;

    public float moveSpeed;
    public int maxHp;
    public int power;

    public float detectionRange;
    public float attackRange;
    public bool isImmortal;

    [Header("Hit Reaction")]
    [Min(0f)] public float knockbackDistance = 0.35f;
    [Min(0f)] public float knockbackDuration = 0.12f;

    [Header("Combat Safety")]
    [Tooltip("Animation Event가 누락되어도 공격 상태가 영구 고정되지 않도록 강제로 종료하는 시간입니다.")]
    [Min(0.1f)] public float attackTimeout = 5f;
}
