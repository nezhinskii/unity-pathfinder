using BaseAI;
using System.Drawing;
using UnityEngine;

public class MovingObstacle : MonoBehaviour
{
    [SerializeField] private Vector3 startPoint;
    [SerializeField] private Vector3 endPoint;
    [SerializeField] private float travelSpeed = 10f;
    private bool movingToEnd = true;
    private bool initialMovingToEnd = true;
    private const float proximityThreshold = 0.5f;
    private Vector3 currentDestination;
    private BoxCollider boxCollider;
    private float initialProgress;

    private void Start()
    {
        currentDestination = movingToEnd ? endPoint : startPoint;
        initialMovingToEnd = movingToEnd;
        boxCollider = GetComponent<BoxCollider>();

        float totalDistance = Vector3.Distance(startPoint, endPoint);
        if (totalDistance > 0)
        {
            float distanceToStart = Vector3.Distance(transform.position, startPoint);
            initialProgress = distanceToStart / totalDistance;
        }
        else
        {
            initialProgress = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (travelSpeed <= 0) return;

        Vector3 direction = (currentDestination - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, currentDestination);

        float step = travelSpeed * Time.fixedDeltaTime;
        transform.position += direction * step;

        if (distanceToTarget < proximityThreshold)
        {
            movingToEnd = !movingToEnd;
            currentDestination = movingToEnd ? endPoint : startPoint;
        }
    }


    public Vector3 GetPositionAtTime(float time)
    {
        if (travelSpeed <= 0) return transform.position;

        float totalDistance = Vector3.Distance(startPoint, endPoint);
        float travelTime = totalDistance / travelSpeed;
        float fullCycleTime = travelTime * 2;

        float adjustedTime = time + ((initialMovingToEnd ? initialProgress : (1 + 1 - initialProgress)) * travelTime);

        float cycleTime = adjustedTime % fullCycleTime;
        bool isMovingToEnd = cycleTime < travelTime;
        float progress = isMovingToEnd ? cycleTime / travelTime : (fullCycleTime - cycleTime) / travelTime;

        return Vector3.Lerp(startPoint, endPoint, progress);
    }

    public bool Contains(PathNode node, float radiusBuffer = 2.5f)
    {
        Vector3 obstaclePosition = GetPositionAtTime(node.TimeMoment);

        Vector3 colliderCenter = obstaclePosition + boxCollider.center;
        Vector3 colliderSize = Vector3.Scale(boxCollider.size, transform.localScale) * 0.5f;

        colliderSize += Vector3.one * radiusBuffer;

        Vector3 nodePosition = node.Position;
        Vector3 minBounds = colliderCenter - colliderSize;
        Vector3 maxBounds = colliderCenter + colliderSize;

        //Debug.DrawLine(minBounds, maxBounds, UnityEngine.Color.red, 2.0f);

        bool isInside = nodePosition.x >= minBounds.x && nodePosition.x <= maxBounds.x &&
                        nodePosition.y >= minBounds.y && nodePosition.y <= maxBounds.y &&
                        nodePosition.z >= minBounds.z && nodePosition.z <= maxBounds.z;

        return isInside;
    }
}