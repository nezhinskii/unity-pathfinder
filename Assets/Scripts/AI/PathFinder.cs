using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Priority_Queue;
using UnityEngine;

namespace BaseAI
{
    /// <summary>
    /// Делегат для обновления пути - вызывается по завершению построения пути
    /// </summary>
    /// <param name="pathNodes"></param>
    /// /// <returns>Успешно ли построен путь до цели</returns>
    public delegate void UpdatePathListDelegate(List<PathNode> pathNodes);

    /// <summary>
    /// Глобальный маршрутизатор - сделать этого гада через делегаты и работу в отдельном потоке!!!
    /// </summary>
    public class PathFinder : MonoBehaviour
    {
        /// <summary>
        /// Объект сцены, на котором размещены коллайдеры
        /// </summary>
        [SerializeField] private GameObject CollidersCollection;

        /// <summary>
        /// Картограф - класс, хранящий информацию о геометрии уровня, регионах и прочем
        /// </summary>
        [SerializeField] private Cartographer cartographer;

        /// <summary>
        /// Маска слоя с препятствиями (для проверки столкновений)
        /// </summary>
        private int obstaclesLayerMask;

        /// <summary>
        /// 
        /// </summary>
        private float rayRadius;

        public PathFinder()
        {

        }

        /// <summary>
        /// Проверка того, что точка проходима. Необходимо обратиться к коллайдеру, ну ещё и проверить высоту над поверхностью
        /// </summary>
        /// <param name="node">Точка</param>
        /// <returns></returns>
        private bool CheckWalkable(ref PathNode node, IBaseRegion currentRegion, IBaseRegion targetRegion)
        {
            //  Сначала проверяем, принадлежит ли точка целевому региону (ну там и переприсвоим индекс если что)
            //  Первым проверяем целевой регион - это обязательно!

            if (targetRegion.Contains(node))
            {
                node.RegionIndex = targetRegion.index;
            }
            else if (currentRegion.Contains(node))
            {
                //  Теперь проверяем на принадлежность текущему региону
                node.RegionIndex = currentRegion.index;
            }
            else
            {
                //  Не принадлежит ни целевому, ни рабочему
                return false;
            }

            var copyNode = node;
            var nodePos = node.Position;
            // Следующая проверка - на то, что над поверхностью расстояние не слишком большое
            // Если один регионов не относящихся к Terrain содержит узел, то не надо проверять высоту. Но при этом важно, если попали в узел НЕ прыжком из НЕ динамического региона, то нужно проверить
            if (!cartographer.noTerrainRegions.Any((region) => region.Contains(copyNode)) || (!node.JumpNode && !currentRegion.Dynamic))
            {
                //  Технически, тут можно как-то корректировать высоту - с небольшим шагом, позволить объекту спускаться или подниматься, но в целом это проверку пока что можно отключить
                float distToFloor = nodePos.y - cartographer.SceneTerrain.SampleHeight(nodePos);
                if (distToFloor > 2.0f || distToFloor < 0.0f)
                {
                    //Debug.Log("Incorrect node height");
                    return false;
                }
            }

            //  Ну и осталось проверить препятствия - для движущихся не сработает такая штука, потому что проверка выполняется для
            //  момента времени в будущем.
            //  Но из этой штуки теоретически можно сделать и для перемещающихся препятствий работу - надо будет перемещающиеся
            //  заворачивать в отдельный 

            //if (node.Parent != null && Physics.CheckSphere(node.Position, 2.0f, obstaclesLayerMask))
            //if (node.Parent != null && Physics.Linecast(node.Parent.Position, node.Position, obstaclesLayerMask))
            if (node.Parent != null && Physics.CheckSphere(nodePos, 1.0f, obstaclesLayerMask))
                return false;

            return true;
        }

        private float Heur(PathNode node, PathNode target, MovementProperties properties)
        {
            Vector3 nodePos = node.Position;
            Vector3 targetPos = target.Position;
            float angle = Mathf.Abs(Vector3.SignedAngle(node.Direction, targetPos - nodePos, Vector3.up)) / properties.rotationAngle;
            return node.TimeMoment + 2 * Vector3.Distance(nodePos, targetPos) / properties.maxSpeed + angle * properties.deltaTime;
        }

        /// <summary>
        /// Получение списка соседей для некоторой точки.
        /// Координаты текущей точки могут быть как локальными, так и глобальными.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public List<PathNode> GetNeighbours(PathNode node, MovementProperties properties, PathNode target, IBaseRegion currentRegion, IBaseRegion targetRegion)
        {
            //  Вот тут хардкодить не надо, это должно быть в properties
            //  У нас есть текущая точка, и свойства движения (там скорость, всякое такое)

            float step = properties.deltaTime * properties.maxSpeed;

            List<PathNode> result = new List<PathNode>();

            //  Сначала прыжок проверяем, и если он возможен, то на этом и закончим
            //  Прыжок должен допускаться только между регионами с разными признаками динамичности
            if (targetRegion.Dynamic != currentRegion.Dynamic)
            {
                PathNode jumpPlace = node.SpawnJumpForward(properties);
                // jumpPlace.PredictTransformPoint(cartographer, node);
                if (CheckWalkable(ref jumpPlace, currentRegion, targetRegion))
                {
                    //  Ну, она проходима, но этого мало, мы должны оказаться в целевом регионе
                    if (jumpPlace.RegionIndex == targetRegion.index)
                    {
                        //  То есть прыгнуть можно, и это прыжок в другой регион
                        //  Маркируем точку как прыжковую (в которую прыгнуть надо)
                        jumpPlace.JumpNode = true;

                        jumpPlace.H = Heur(jumpPlace, target, properties);
                        result.Add(jumpPlace);

                        return result;
                    }
                }
            }

            //  А в обычные маршруты прыжки не попадают
            //  Внешний цикл отвечает за длину шага - либо 0 (остаёмся в точке), либо 1 - шагаем вперёд
            var max = 5;
            if (targetRegion.Dynamic != currentRegion.Dynamic)
            {
                max = 0;
            }
            for (int mult = max; mult >= 0; --mult)
                //  Внутренний цикл перебирает углы поворота
                for (int angleStep = -properties.angleSteps; angleStep <= properties.angleSteps; ++angleStep)
                {
                    PathNode next;
                    var m = max == 0 ? 0 : (float)mult / max;
                    next = node.SpawnChild(step * m, angleStep * properties.rotationAngle, properties.deltaTime);
                    next.PredictTransformPoint(cartographer, node);
                    next.RegionIndex = node.RegionIndex;
                    if (CheckWalkable(ref next, currentRegion, targetRegion))
                    {
                        next.H = Heur(next, target, properties);
                        result.Add(next);
                    }
                }
            result.Sort((a, b) => a.H.CompareTo(b.H));
            return result;
        }

        /// <summary>
        /// Поиск пути (локальный, sample-based, с отсечением пройденных региончиков)
        /// </summary>
        /// <param name="start">Начальная точка пути</param>
        /// <param name="target">Целевая точка пути</param>
        /// <param name="movementProperties">Параметры движения бота</param>
        /// <param name="updater">Делегат, обновляющий путь у бота - вызывается с построенным путём</param>
        /// <param name="finishPredicate">Условие остановки поиска пути</param>
        /// <returns></returns>

        private float FindPath(PathNode start, PathNode target, int targetRegionIndex, MovementProperties movementProperties, UpdatePathListDelegate updater, Func<PathNode, PathNode, bool> finishPredicate)
        {
            start.Parent = null;
            Debug.DrawLine(start.Position, target.Position, Color.black, 1f);
            if (Vector3.Distance(start.Position, target.Position) < movementProperties.epsilon)
            {
                updater(null);
                return -1f;
            }

            var opened = new SimplePriorityQueue<PathNode>();
            opened.Enqueue(start, 0);
            var closed = new HashSet<(int, int, int, int, int, int, int)>
            {
                start.ToGridPoint(movementProperties.deltaDist, movementProperties.deltaTime)
            };

            int steps = 0;
            float largestTime = 0;

            PathNode rn = null;
            while (opened.Count != 0 && steps < 1000)
            {
                steps++;
                var current = opened.Dequeue();

                if (finishPredicate(current, target))
                {
                    rn = current;
                    break;
                }

                var neighbours = GetNeighbours(current, movementProperties, target, cartographer.regions[current.RegionIndex], cartographer.regions[targetRegionIndex]);
                foreach (var nextNode in neighbours)
                {
                    if (nextNode.TimeMoment > largestTime) largestTime = nextNode.TimeMoment; // ???
                    var discreteNode = nextNode.ToGridPoint(movementProperties.deltaDist, movementProperties.deltaTime);
                    if (!closed.Contains(discreteNode))
                    {
                        opened.Enqueue(nextNode, nextNode.H);
                        closed.Add(discreteNode);
                    }
                }
            }
            if (rn == null || !finishPredicate(rn, target))
            {
                updater(null);
                return -1f;
            }
            // Debug.DrawLine(rn.PredictPosition(cartographer), target.PredictPosition(cartographer), Color.white, 5f);
            List<PathNode> result = new List<PathNode>();
            var currentNode = rn;

            while (currentNode != null)
            {
                if (currentNode.Parent != null)
                {
                    if (currentNode.JumpNode)
                        Debug.DrawLine(currentNode.Position, currentNode.Parent.Position, Color.magenta, 5f);
                    else
                        Debug.DrawLine(currentNode.Position, currentNode.Parent.Position, Color.red, 5f);
                }

                result.Add(currentNode);
                currentNode = currentNode.Parent;
            }
            result.Reverse();
            updater(result);
            Debug.Log("Финальная точка маршрута : " + result[result.Count - 1].Position.ToString() + "; target : " + target.Position.ToString());
            return result[result.Count - 1].TimeMoment - result[0].TimeMoment;
        }

        public bool BuildRoute(PathNode start, PathNode finish, MovementProperties movementProperties, UpdatePathListDelegate updater)
        {
            IBaseRegion[] startRegions = cartographer.GetRegions(start);
            IBaseRegion[] finishRegions = cartographer.GetRegions(finish);

            if (startRegions.Length == 0 || finishRegions.Length == 0)
            {
                Debug.LogError("Начальная или конечная точка не находится ни в одном регионе.");
                return false;
            }

            Func<PathNode, PathNode, bool> stopCondition = (cur, fin) =>
            {
                if (cur.RegionIndex != -1 && cur.RegionIndex == fin.RegionIndex) return true;
                if (finishRegions.Any(fr => fr.Contains(cur))) return true;

                Vector3 curPos = cur.Position;
                Vector3 finPos = fin.Position;

                return Vector2.Distance(new Vector2(curPos.x, curPos.z), new Vector2(finPos.x, finPos.z)) <= movementProperties.epsilon;
                // return Vector3.Distance(curPos, finPos) <= movementProperties.epsilon;
            };
            Func<PathNode, PathNode, bool> stopConditionSameRegion = (cur, fin) =>
            {
                Vector3 curPos = cur.Position;
                Vector3 finPos = fin.Position;
                return Vector2.Distance(new Vector2(curPos.x, curPos.z), new Vector2(finPos.x, finPos.z)) <= movementProperties.epsilon;
                // return Vector3.Distance(curPos, finPos) <= movementProperties.epsilon;
            };

            bool inSameRegion = startRegions.Any(sr => finishRegions.Any(fr => sr == fr));
            if (inSameRegion)
            {
                var commonRegion = startRegions.First(sr => finishRegions.Contains(sr));
                finish.RegionIndex = commonRegion.index;
                start.RegionIndex = commonRegion.index;
                FindPath(start, finish, commonRegion.index, movementProperties, updater, stopConditionSameRegion);
                return true;
            }
            Dictionary<int, float> distances = new Dictionary<int, float>();
            Dictionary<int, int> previous = new Dictionary<int, int>();
            SimplePriorityQueue<int, float> queue = new SimplePriorityQueue<int, float>();

            foreach (var region in cartographer.regions)
            {
                distances[region.index] = float.MaxValue;
                previous[region.index] = -1;
            }

            foreach (var startRegion in startRegions)
            {
                distances[startRegion.index] = 0;
                queue.Enqueue(startRegion.index, 0);
            }
            while (queue.Count > 0)
            {
                int currentRegionIndex = queue.Dequeue();

                if (finishRegions.Any(fr => fr.index == currentRegionIndex))
                {
                    break;
                }

                var neighbors = cartographer.regions[currentRegionIndex].Neighbors;

                foreach (var neighbor in neighbors)
                {
                    if (neighbor.index == -1) continue;

                    // Расстояние между центрами регионов как базовая метрика
                    float distance = Vector3.Distance(
                        cartographer.regions[currentRegionIndex].GetCenter(),
                        cartographer.regions[neighbor.index].GetCenter()
                    );

                    float newDistance = distances[currentRegionIndex] + distance;

                    if (newDistance < distances[neighbor.index])
                    {
                        distances[neighbor.index] = newDistance;
                        previous[neighbor.index] = currentRegionIndex;
                        queue.Enqueue(neighbor.index, newDistance);
                    }
                }
            }

            int targetRegionIndex = -1;
            float minDistance = float.MaxValue;
            foreach (var finishRegion in finishRegions)
            {
                if (distances[finishRegion.index] < minDistance)
                {
                    minDistance = distances[finishRegion.index];
                    targetRegionIndex = finishRegion.index;
                }
            }
            if (targetRegionIndex == -1)
            {
                Debug.LogError("Путь между начальным и конечным регионами не найден.");
                return false;
            }
            List<int> regionPath = new List<int>();
            int current = targetRegionIndex;
            while (current != -1)
            {
                regionPath.Add(current);
                current = previous[current];
            }
            regionPath.Reverse();
            int startRegionIndex = regionPath[0];
            int nextRegionIndex = regionPath[1];

            PathNode targetPoint = finish.RegionIndex == nextRegionIndex
                ? new PathNode(finish.Position, Vector3.zero) { PositionIsLocal = finish.PositionIsLocal }
                : new PathNode(cartographer.regions[nextRegionIndex].GetCenter(), Vector3.zero);
            targetPoint.RegionIndex = nextRegionIndex;
            start.RegionIndex = startRegionIndex;
            FindPath(start, targetPoint, nextRegionIndex, movementProperties, updater, stopCondition);
            return true;
        }

        //// Start is called before the first frame update
        void Start()
        {
            //  Инициализируем картографа, ну и всё вроде бы
            cartographer = new Cartographer(CollidersCollection);
            obstaclesLayerMask = 1 << LayerMask.NameToLayer("Obstacles");
            var rend = GetComponent<Renderer>();
            if (rend != null)
                rayRadius = rend.bounds.size.y / 2.5f;
        }
    }
}