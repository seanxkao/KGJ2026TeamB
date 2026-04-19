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
        public int formatVersion = 2;
        public int rootIndex;
        public AssemblyPieceSnapshotRecord[] pieces = Array.Empty<AssemblyPieceSnapshotRecord>();
        public AssemblyJointEdgeRecord[] joints = Array.Empty<AssemblyJointEdgeRecord>();
    }

    [Serializable]
    public sealed class AssemblyPieceSnapshotRecord
    {
        public string modelId;
        public string instanceGuid;

        /// <summary>相對於連通群「根」剛體之位置／旋轉（便於整組平移到 Battle 出生點）。</summary>
        public Vector3 localPosition;
        public Quaternion localRotation;

        /// <summary>擷取當下之世界座標（組裝場內）；下一場若要整組偏移請自行減去根再平移。</summary>
        public Vector3 worldPosition;
        public Quaternion worldRotation;

        /// <summary>零件根 <see cref="Transform.localScale"/>（擷取當下）。</summary>
        public Vector3 localScale;
    }

    [Serializable]
    public struct AssemblyJointEdgeRecord
    {
        public int ownerIndex;
        public int connectedIndex;
    }
}
