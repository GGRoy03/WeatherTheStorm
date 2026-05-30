using UnityEngine;

[CreateAssetMenu(menuName = "Map/CompatibilityTable")]
public class SocketCompatibilityTable : ScriptableObject
{
    [System.Serializable]
    public struct SocketPair
    {
        public SocketType A;
        public SocketType B;
    }
}
