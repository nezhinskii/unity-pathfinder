using BaseAI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Platform1Movement : MonoBehaviour, BaseAI.IBaseRegion
{
    private Vector3 initialPosisition;
    [SerializeField] private bool moving;
    public Vector3 rotationCenter;
    private Vector3 rotationStartPos;
    [SerializeField] private float rotationSpeed = 1.0f;

    /// <summary>
    /// Тело региона - коллайдер
    /// </summary>
    public SphereCollider body;

    /// <summary>
    /// Индекс региона в списке регионов
    /// </summary>
    public int index { get; set; } = -1;
    bool IBaseRegion.Dynamic { get; } = true;

    public IList<BaseAI.IBaseRegion> Neighbors { get; set; } = new List<BaseAI.IBaseRegion>();

    private List<Transform> objectsToTransform = new List<Transform>();

    void Start()
    {
        rotationCenter = transform.position + 10 * Vector3.back;
        rotationStartPos = transform.position;
    }

    void Update()
    {
        ApplyTransform(transform);
        foreach (var obj in objectsToTransform)
        {
            ApplyTransform(obj);
        }
    }

    void ApplyTransform(Transform transform)
    {
        if (!moving) return;

        transform.RotateAround(rotationCenter, Vector3.up, Time.deltaTime * rotationSpeed);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Transform playerTransform = collision.transform;
            objectsToTransform.Add(playerTransform);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Transform playerTransform = collision.transform;
            objectsToTransform.Remove(playerTransform);
        }
    }

    void IBaseRegion.TransformPoint(PathNode parent, PathNode node) {
        
        float timeDelta = node.TimeMoment - parent.TimeMoment;

        Vector3 dir = node.Position - rotationCenter;
        node.Position = rotationCenter + Quaternion.AngleAxis(-rotationSpeed * timeDelta, Vector3.up) * dir;
        node.Direction = Quaternion.AngleAxis(-rotationSpeed * timeDelta, Vector3.up) * node.Direction;
        return;
    }

    Vector3 IBaseRegion.PredictPosition(PathNode node, float timeDelta)
    {
        Vector3 dir = node.Position - rotationCenter;
        return rotationCenter + Quaternion.AngleAxis(rotationSpeed * timeDelta, Vector3.up) * dir;
    }

    Vector3 IBaseRegion.PredictDirection(PathNode node, float timeDelta)
    {
        return Quaternion.AngleAxis(rotationSpeed * timeDelta, Vector3.up) * node.Direction;
    }

    void IBaseRegion.TransformGlobalToLocal(PathNode node) 
    {
        //  Вот тут всё плохо - определяем момент времени, через который нам нужна точка
        float timeDelta = node.TimeMoment - Time.time;
        //  Откручиваем точку обратно в направлении, противоположном движению региона

        Vector3 dir = node.Position - rotationCenter;
        node.Position = rotationCenter + Quaternion.AngleAxis(-rotationSpeed * timeDelta, Vector3.up) * dir;
        node.Direction = Quaternion.AngleAxis(-rotationSpeed * timeDelta, Vector3.up) * node.Direction;
        //  Преобразуем в локальные координаты
        node.Position = transform.InverseTransformPoint(node.Position);
        node.Direction = transform.InverseTransformDirection(node.Direction);
        //  Всё вроде бы
        node.PositionIsLocal = true;
    }

    void IBaseRegion.TransformLocalToGlobal(PathNode node)
    {
        node.Position = transform.TransformPoint(node.Position);
        node.Direction = transform.TransformDirection(node.Direction);

        float timeDelta = node.TimeMoment - Time.time;
        Vector3 dir = node.Position - rotationCenter;
        node.Position = rotationCenter + Quaternion.AngleAxis(rotationSpeed * timeDelta, Vector3.up) * dir;
        node.Direction = Quaternion.AngleAxis(rotationSpeed * timeDelta, Vector3.up) * node.Direction;

        node.PositionIsLocal = false;
    }

    PathNode IBaseRegion.GetGlobalFromLocal(PathNode node)
    {
        PathNode result = node.SpawnChild(0, 0, 0);
        if (!node.PositionIsLocal)
        {
            return result;
        }
        result.Position = transform.TransformPoint(result.Position);
        result.Direction = transform.TransformDirection(result.Direction);

        float timeDelta = result.TimeMoment - Time.time;
        Vector3 dir = result.Position - rotationCenter;
        result.Position = rotationCenter + Quaternion.AngleAxis(rotationSpeed * timeDelta, Vector3.up) * dir;
        result.Direction = Quaternion.AngleAxis(rotationSpeed * timeDelta, Vector3.up) * result.Direction;

        result.PositionIsLocal = false;
        return result;
    }

    bool IBaseRegion.Contains(PathNode node)
    {
        //  Самая жуткая функция - тут думать надо
        //  Вывести точку через 2 секунды - положение платформы через 2 секунды в будущем
        float deltaTime = node.TimeMoment - Time.time;
        if (deltaTime < 0) return false;

        // DEBUG INFO
        var center = transform.position;
        var centerDir = center - rotationCenter;
        var newCenterPoint = rotationCenter + Quaternion.AngleAxis(rotationSpeed * deltaTime, Vector3.up) * centerDir;
        if (node.JumpNode)
        {
            Debug.Log(node.Position + " : " + newCenterPoint);
        }
        // DEBUG INFO

        Vector3 dir = node.Position - rotationCenter;
        Vector3 newPoint = rotationCenter + Quaternion.AngleAxis(-rotationSpeed * deltaTime, Vector3.up) * dir;
        //  Осторожно! Тут два коллайдера у объекта, проверить какой именно вытащили.
        var coll = GetComponent<Collider>();
        return coll != null && coll.bounds.Contains(newPoint);
    }

    Vector3 IBaseRegion.GetCenter()
    {
        return transform.position;
    }

    float IBaseRegion.SqrDistanceTo(PathNode node)
    {
        //  Вот тоже должно быть странно - как-то надо узнать, эта точка вообще попадает в коллайдер, 
        //  и если попадает, то когда? Может, тупо до центра области сделать? Сойдёт же!
        throw new System.NotImplementedException();
    }

    float IBaseRegion.TransferTime(IBaseRegion source, float transitStart, IBaseRegion dest)
    {
        //  Время перехода через регион - вроде бы несложно, можно даже захардкодить
        throw new System.NotImplementedException();
    }

    void IBaseRegion.AddTransferTime(IBaseRegion source, IBaseRegion dest)
    {
        throw new System.NotImplementedException();
    }
}
