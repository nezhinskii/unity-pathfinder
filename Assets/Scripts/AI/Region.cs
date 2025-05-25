using System.Collections.Generic;
using UnityEngine;

namespace BaseAI
{
    public class Region : MonoBehaviour, IBaseRegion
    {
        private Collider _body;

        public Collider body
        {
            get
            {
                if (_body == null)
                {
                    _body = GetComponent<Collider>();
                }
                return _body;
            }
        }

        public int index { get; set; } = -1;
        public bool Dynamic { get; } = false;
        public IList<IBaseRegion> Neighbors { get; set; } = new List<IBaseRegion>();

        public bool Contains(PathNode node)
        {
            return body.bounds.Contains(node.Position);
        }

        public Vector3 GetCenter()
        {
            return body.bounds.center;
        }

        public float SqrDistanceTo(PathNode node)
        {
            return (node.Position - transform.position).sqrMagnitude;
        }

        public void TransformGlobalToLocal(PathNode node)
        {
            node.Position = transform.InverseTransformPoint(node.Position);
            node.Direction = transform.InverseTransformDirection(node.Direction);
            node.PositionIsLocal = true;
        }

        public void TransformLocalToGlobal(PathNode node)
        {
            node.Position = transform.TransformPoint(node.Position);
            node.Direction = transform.TransformDirection(node.Direction);
            node.PositionIsLocal = false;
        }

        public PathNode GetGlobalFromLocal(PathNode node)
        {
            PathNode result = node.SpawnChild(0, 0, 0);
            if (!node.PositionIsLocal)
            {
                return result;
            }
            result.Position = transform.TransformPoint(result.Position);
            result.Direction = transform.TransformDirection(result.Direction);
            result.PositionIsLocal = false;
            return result;
        }

        public void TransformPoint(PathNode parent, PathNode node) { }
        public Vector3 PredictPosition(PathNode node, float timeDelta) { return node.Position; }
        public Vector3 PredictDirection(PathNode node, float timeDelta) { return node.Direction; }

        public float TransferTime(IBaseRegion source, float transitStart, IBaseRegion dest)
        {
            throw new System.NotImplementedException();
        }

        public void AddTransferTime(IBaseRegion source, IBaseRegion dest)
        {
            throw new System.NotImplementedException();
        }
    }
}