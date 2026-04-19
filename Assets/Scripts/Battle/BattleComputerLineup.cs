using System;
using UnityEngine;

[Serializable]
public class BattleComputerEntry
{
    [SerializeField]
    private string _displayName;

    [SerializeField]
    private BeybladePartPlayConfig[] _parts;

    public string DisplayName => _displayName;
    public BeybladePartPlayConfig[] Parts => _parts;
}

[CreateAssetMenu(fileName = "BattleComputerLineup", menuName = "Scriptable Objects/Battle/Computer Lineup")]
public class BattleComputerLineup : ScriptableObject
{
    [SerializeField]
    private BattleComputerEntry[] _entries;

    public BattleComputerEntry[] Entries => _entries;
}
