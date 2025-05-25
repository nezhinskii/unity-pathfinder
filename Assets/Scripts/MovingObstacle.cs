using BaseAI;
using System.Drawing;
using UnityEngine;

public class MovingObstacle : MonoBehaviour
{
    [SerializeField] private Vector3 startPoint;
    [SerializeField] private Vector3 endPoint;
    [SerializeField] private float travelSpeed = 10f;
    private bool movingToEnd = true;
    private const float proximityThreshold = 0.5f;
    private Vector3 currentDestination;
    private BoxCollider boxCollider;

    private void Start()
    {
        currentDestination = movingToEnd ? endPoint : startPoint;
        boxCollider = GetComponent<BoxCollider>();
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

    public bool Contains(PathNode node, float radiusBuffer = 2.5f)
    {
        float timeSinceStart = node.TimeMoment - Time.fixedTime;
        float cycleTime = Vector3.Distance(startPoint, endPoint) / travelSpeed * 2;
        if (cycleTime <= 0) return false;

        float normalizedTime = timeSinceStart % cycleTime;
        if (normalizedTime < 0) normalizedTime += cycleTime;

        float halfCycleTime = cycleTime / 2;
        bool isMovingToEnd = normalizedTime < halfCycleTime;
        float t = isMovingToEnd ? normalizedTime / halfCycleTime : (normalizedTime - halfCycleTime) / halfCycleTime;
        Vector3 obstaclePosition;

        if (isMovingToEnd)
        {
            obstaclePosition = Vector3.Lerp(startPoint, endPoint, t);
        }
        else
        {
            obstaclePosition = Vector3.Lerp(endPoint, startPoint, t);
        }

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