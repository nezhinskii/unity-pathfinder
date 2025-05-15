using UnityEngine;

public class MovingObstacle : MonoBehaviour
{
    [SerializeField] private Vector3 startPoint;
    [SerializeField] private Vector3 endPoint;
    [SerializeField] private float travelSpeed = 10f;
    private bool movingToEnd = true;
    private const float proximityThreshold = 0.5f;
    private Vector3 currentDestination;

    private void Start()
    {
        currentDestination = movingToEnd ? endPoint : startPoint;
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
}