using System.Collections.Generic;
using UnityEngine;

namespace BaseAI
{
    public interface IBaseRegion
    {
        int index { get; set; }
        IList<IBaseRegion> Neighbors { get; set; }
        bool Contains(PathNode node);
        bool Dynamic { get; }
        void TransformPoint(PathNode parent, PathNode node);
        Vector3 PredictPosition(PathNode node, float timeDelta);
        Vector3 PredictDirection(PathNode node, float timeDelta);
        void TransformGlobalToLocal(PathNode node);
        void TransformLocalToGlobal(PathNode node);
        PathNode GetGlobalFromLocal(PathNode node);
        float SqrDistanceTo(PathNode node);
        void AddTransferTime(IBaseRegion source, IBaseRegion dest);
        float TransferTime(IBaseRegion source, float transitStart, IBaseRegion dest);
        Vector3 GetCenter();
    }
}