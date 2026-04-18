using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 可組裝零件標記；需搭配 Rigidbody。Collider 可在本物件或子物件（含 MeshCollider／BoxCollider），
    /// <see cref="AssemblyHoldAndSnapController"/> 會以階層搜尋。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AssemblyPiece : MonoBehaviour
    {
    }
}
