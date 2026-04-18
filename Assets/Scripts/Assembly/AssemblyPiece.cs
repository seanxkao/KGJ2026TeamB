using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 可組裝零件標記；需搭配 Rigidbody 與 Collider。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class AssemblyPiece : MonoBehaviour
    {
    }
}
