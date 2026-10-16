using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyChunk
{
    [SerializeField] private List<EnemyEntry> _entries = new List<EnemyEntry>();

    public List<EnemyEntry> Entries => _entries;
}

