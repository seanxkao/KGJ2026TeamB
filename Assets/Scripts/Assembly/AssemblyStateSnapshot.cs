using System;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 可序列化（JsonUtility）的組裝快照根物件；勿使用頂層陣列作為 Json 根。
    /// </summary>
    [Serializable]
    public sealed class AssemblyStateSnapshot
    {
        public int formatVersion = 1;
        public int rootIndex;
        public AssemblyPieceSnapshotRecord[] pieces = Array.Empty<AssemblyPieceSnapshotRecord>();
        public AssemblyJointEdgeRecord[] joints = Array.Empty<AssemblyJointEdgeRecord>();
    }

    [Serializable]
    public sealed class AssemblyPieceSnapshotRecord
    {
        public string modelId;
        public string instanceGuid;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    [Serializable]
    public struct AssemblyJointEdgeRecord
    {
        public int ownerIndex;
        public int connectedIndex;
    }
}
