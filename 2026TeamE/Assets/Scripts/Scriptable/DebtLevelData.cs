using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "DebtLevelData", menuName = "Scriptable Objects/DebtLevelData")]
public class DebtLevelData : ScriptableObject
{
    public int totalGoal = 100000000;
    public List<DebtPhase> phase;
}
