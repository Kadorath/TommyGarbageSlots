using UnityEngine;

[CreateAssetMenu(fileName = "SlotSymbol", menuName = "Scriptable Objects/SymbolSO")]
public class SymbolSO : ScriptableObject
{
    public SymbolType Symbol;
    public Sprite Icon;
    public int Count;
    public int[] Scores = new int[3];
}

public enum SymbolType
{
    PEEL,
    BOOT,
    DIAMOND,
    FISH,
    TV,
    UMBRELLA,
    TOMMY,
    WILD
}