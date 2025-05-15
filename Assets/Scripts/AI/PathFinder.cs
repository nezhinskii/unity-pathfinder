using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            if ( (targetRegion.Dynamic == currentRegion.Dynamic && targetRegion.Contains(node))
               ||(targetRegion.Dynamic != currentRegion.Dynamic && targetRegion.Contains(node) && node.JumpNode)
            )
            {
                node.RegionIndex = targetRegion.index;
            }
            else
                //  Теперь проверяем на принадлежность текущему региону
                if (currentRegion.Contains(node))
                {
                    node.RegionIndex = currentRegion.index;
                }
                else 
                    return false;  //  Не принадлежит ни целевому, ни рабочему
            var copyNode = node;
            // Следующая проверка - на то, что над поверхностью расстояние не слишком большое
            // Если один регионов не относящихся к Terrain содержит узел, то не надо проверять высоту. Но при этом важно, если попали в узел НЕ прыжком из НЕ динамического региона, то нужно проверить
            if (!cartographer.noTerrainRegions.Any((region) => region.Contains(copyNode)) || (!node.JumpNode && !currentRegion.Dynamic))
            {
                //  Технически, тут можно как-то корректировать высоту - с небольшим шагом, позволить объекту спускаться или подниматься, но в целом это проверку пока что можно отключить
                float distToFloor = node.Position.y - cartographer.SceneTerrain.SampleHeight(node.Position);
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
            if (node.Parent != null && Physics.CheckSphere(node.Position, 1.0f, obstaclesLayerMask))
                return false;
            
            return true;
        }

        private float Heur(PathNode node, PathNode target, MovementProperties properties)
        {
            var targetRegion = cartographer.regions[target.RegionIndex];
            var targetPos = target.Position;
            if (targetRegion.Dynamic)
            {
                if (node.RegionIndex == targetRegion.index)
                {
                    float timeDelta = node.TimeMoment - Time.time;
                    targetPos = targetRegion.PredictPosition(target, timeDelta);
                }
                else
                {
                    targetPos = (targetRegion as Platform1Movement).rotationCenter;
                }
            }
            //  Эвристику переделать - сейчас учитываются уже затраченное время, оставшееся до цели время и угол поворота
            float angle = Mathf.Abs(Vector3.SignedAngle(node.Direction, targetPos - node.Position, Vector3.up)) / properties.rotationAngle;
            return node.TimeMoment + 2 * node.Distance(targetPos) / properties.maxSpeed + angle * properties.deltaTime;
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
                Debug.DrawLine(jumpPlace.Position - new Vector3(0, 0, 0.2f), jumpPlace.Position + new Vector3(0, 0, 0.2f), Color.magenta, 20f);
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
            for (int mult = 0; mult <= 1; ++mult)
                //  Внутренний цикл перебирает углы поворота
                for (int angleStep = -properties.angleSteps; angleStep <= properties.angleSteps; ++angleStep)
                {
                    PathNode next = node.SpawnChild(step * mult, angleStep * properties.rotationAngle, properties.deltaTime);
                    // Для динамического региона пердсказываем дельту положения для текущего шага (для отсальных 0)
                    Vector3 predictedOffset = currentRegion.PredictPosition(node, properties.deltaTime) - node.Position;
                    next.Position += predictedOffset;
                    /*float predictedDirectionOffset = Vector3.SignedAngle(currentRegion.PredictDirection(node, properties.deltaTime), node.Direction, Vector3.up);
                    next.Direction = Quaternion.AngleAxis(predictedDirectionOffset, Vector3.up) * next.Direction;
                    next.Direction.Normalize();*/

                    //  Точка передаётся по ссылке, т.к. возможно обновление региона, которому она принадлежит
                    if (CheckWalkable(ref next, currentRegion, targetRegion))
                        //if(next.RegionIndex == node.RegionIndex || next.RegionIndex == target.RegionIndex)
                    {
                        next.H = Heur(next, target, properties);
                        result.Add(next);
                        Debug.DrawLine(node.Position, next.Position, Color.blue, 10f);
                        if (next.RegionIndex == 9)
                            Debug.Log("Nine'th region visited");
                    }
                }
            //  Если регион node динамический, то трансформировать те точки, которые принадлежат тому же региону
            /*IBaseRegion region = cartographer.GetRegion(node);

            if (region != null && region.Dynamic)
            {
                //  Надо заставить регион преобразовать все точки в списке дочерних
                for (int i = 0; i < result.Count; ++i)
                {
                    region.TransformPoint(node, result[i]);
                }
            }*/
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
        private float FindPath(PathNode start, PathNode target, int targetRegionIndex, MovementProperties movementProperties, UpdatePathListDelegate updater, System.Func<PathNode, PathNode, bool> finishPredicate)
        {
            Debug.Log("Начато построение пути");

            //  Целевой регион определяем
            var targetRegion = cartographer.regions[targetRegionIndex];

            var currentRegion = cartographer.regions[start.RegionIndex];
            start.Parent = null;

            //  Вот тут вместо equals надо использовать == (как минимум), а лучше измерять расстояние между точками
            //  сравнивая с некоторым epsilon. Хотя может это для каких-то специальных случаев?
            //  if (position.Position.Equals(target.Position)) return new List<PathNode>();
            if (Vector3.Distance(start.Position, target.Position) < movementProperties.epsilon)
            {
                updater(null);  //  Это функция, которая обновляет путь в агенте
                return -1f;
            }

            Priority_Queue.SimplePriorityQueue<PathNode> opened = new Priority_Queue.SimplePriorityQueue<PathNode>();

            //  Тут тоже вопрос - а почему с 0 добавляем? Хотя она же сразу извлекается из очереди, не важно
            opened.Enqueue(start, 0);
            int steps = 0;

            //  Посещенные узлы (с некоторым шагом, аналог сетки)
            HashSet<(int, int, int, int)> closed = new HashSet<(int, int, int, int)>();
            closed.Add(start.ToGrid4DPoint(movementProperties.deltaDist, movementProperties.deltaTime));

            PathNode current = opened.First;
            float largestTime = 0;

            while (opened.Count != 0 && steps < 4000)
            {
                steps++;
                current = opened.Dequeue();

                if(current.Parent != null)
                {
                    Vector3 toTarget = target.Position - current.Position;
                    if(toTarget.magnitude <= movementProperties.deltaTime * movementProperties.maxSpeed)
                    {
                        float angl = Mathf.Abs(Vector3.Angle(toTarget, current.Position - current.Parent.Position));
                        if(angl < movementProperties.rotationAngle*movementProperties.rotationAngle)
                        {
                            //  Цель в пределах досягаемости
                            current.Position = target.Position;
                        }
                    }
                }

                //  Тут сложная проверка - если извлечённая вершина находится в одном шаге от цели
                if(finishPredicate(current, target))
                //if (Vector3.Distance(current.Position, target.Position) <= movementProperties.epsilon)
                {
                    //  Последнюю точку заменяем целевой - чтобы в точности в неё прийти
                    //  current.Position = target.Position;
                    Debug.Log("Braked by closest point. Steps : " + steps.ToString());
                    break;
                }

                //  Получаем список соседей
                var neighbours = GetNeighbours(current, movementProperties, target, currentRegion, targetRegion);
                if (neighbours.Any((n) => n.JumpNode))
                {
                    Debug.Log("-----------");
                }
                foreach (var nextNode in neighbours)
                {
                    //  Это отладочная информация - чтобы понять, насколько далеко мы успеваем по времени
                    if (nextNode.TimeMoment > largestTime) largestTime = nextNode.TimeMoment;

                    var discreteNode = nextNode.ToGrid4DPoint(movementProperties.deltaDist, movementProperties.deltaTime);
                    if (!closed.Contains(discreteNode))
                    {
                        nextNode.H = Heur(nextNode, target, movementProperties);
                        opened.Enqueue(nextNode, nextNode.H);
                        closed.Add(discreteNode);
                    }
                }
            }

            if (finishPredicate(current, target) == false)
            {
                

                Debug.Log("Largest time : " + largestTime);
                Debug.Log("Failed to build a way. Steps : " + steps.ToString());
                updater(null);
                return -1f;
            }

            List<PathNode> result = new List<PathNode>();

            //  Восстанавливаем путь от целевой к стартовой
            //  Может, заменить последнюю на целевую, с той же отметкой по времени? Но тогда с поворотом сложновато получается
            while (current != null)
            {
                if(current.Parent != null) 
                    Debug.DrawLine(current.Position, current.Parent.Position, Color.red, 20f);
                result.Add(current);
                current = current.Parent;
            }

            result.Reverse();
            //  Если точки принадлежат динамическому региону, то их необходимо преобразовать в локальные координаты
            //  С точки зрения производительности это лучше делать в процессе движения бота, т.к. затраты размазываются
            //  на каждый кадр.
            //  Как-то так
            /*for (var i = 0; i < result.Count; ++i) 
            {
                if (cartographer.regions[result[i].RegionIndex].Dynamic)
                {
                    cartographer.regions[result[i].RegionIndex].TransformGlobalToLocal(result[i]);
                }
            }*/

            if (result.Count > 0 && cartographer.IsInRegion(result[result.Count - 1], targetRegionIndex))
                result[result.Count - 1].RegionIndex = targetRegionIndex;

            //  Обновляем результат у бота
            updater(result);

            Debug.Log("Финальная точка маршрута : " + result[result.Count-1].Position.ToString() + "; target : " + target.Position.ToString());
            return result[result.Count - 1].TimeMoment - result[0].TimeMoment;

        }

        /// <summary>
        /// Основной метод поиска пути, запускает работу в отдельном потоке. Аккуратно с асинхронностью - мало ли, вроде бы 
        /// потокобезопасен, т.к. не изменяет данные о регионах сценах и прочем - работает как ReadOnly
        /// </summary>
        /// <returns></returns>
        public bool BuildRoute(PathNode start, PathNode finish, MovementProperties movementProperties, UpdatePathListDelegate updater)
        {
            /*  Эта функция выполняет построение глобального пути. Её задача - определить, находятся ли 
             *  начальная и целевая точка в одном регионе. Если да, то просто запустить локальный
             *  маршрутизатор и построить маршрут в этом регионе.
             *  Иначе, если регионы разные - найти кратчайший маршрут между регионами (это задача
             *  глобального планировщика, и он должен вернуть регион, соседний с текущим – это регион, 
             *  в который должны шагнуть. После этого необходимо использовать другой вариант функции поиска пути - с другой эвристикой, в качестве которой можно использовать расстояние до центральной точки целевого региона, и другой функций проверки целевого состояния, вместо близости к некоторой точке надо проверять, достигли ли мы целевого региона. А кто умеет лямбды в C#?
             *  В целом тут можно банально использовать алгоритм Дейкстры. Можно немного усложнить, проверяя
             *  расстояние до границ текущего региона, и как-то до цели, но это уже улучшения. Вообще до 
             *  bounds можно это самое расстояние считать как-то. В базовой версии никаких особых извращений не нужно. Можно, конечно, и не Дейкстру, ну или модифицировать его немного.
            */

            //  Получить регион текущей позиции
            //  Если целевая в другом регионе - построить глобальный путь, получить следующий регион
            //  Построить маршрут до следующего региона


            IBaseRegion startRegion = null;
            if (start.RegionIndex == -1)
            {
                startRegion = cartographer.GetRegion(start);
                start.RegionIndex = startRegion.index;
            }
            else
                startRegion = cartographer.regions[start.RegionIndex];
            
            IBaseRegion finishRegion = cartographer.GetRegion(finish);
            finish.RegionIndex = finishRegion.index;

            Func<PathNode, PathNode, bool> stopCondition = (curPathNode, finPathNode) =>
            {
                if (cartographer.regions[finPathNode.RegionIndex].Dynamic || cartographer.regions[curPathNode.RegionIndex].Dynamic)
                {
                    return cartographer.regions[finPathNode.RegionIndex].Contains(curPathNode);
                }
                var targetRegion = cartographer.regions[finPathNode.RegionIndex];
                float timeDelta = curPathNode.TimeMoment - Time.time;
                Vector3 predictedPos = targetRegion.PredictPosition(finPathNode, timeDelta);
                return Vector3.Distance(curPathNode.Position, predictedPos) <= movementProperties.epsilon;
            };

            if (startRegion != finishRegion)
            {
                //  Тут работает глобальный планировщик
                switch(startRegion.index)
                {
                    case 0: { finish.RegionIndex = 3; break; }
                    case 1: { finish.RegionIndex = 0; break; }
                    case 2: { finish.RegionIndex = 1; break; }
                    case 3: { finish.RegionIndex = 5; break; }
                    case 5: { finish.RegionIndex = 6; break; }
                    case 6: { finish.RegionIndex = 7; break; }
                }
                PathNode targetPoint;
                if (finish.RegionIndex == finishRegion.index)
                {
                    targetPoint = new PathNode(finish.Position, Vector3.zero);
                } else
                {
                    finishRegion = cartographer.regions[finish.RegionIndex];
                    targetPoint = new PathNode(finishRegion.GetCenter(), Vector3.zero);
                }
                targetPoint.RegionIndex = finishRegion.index;
                Debug.Log("Going from " + start.RegionIndex.ToString() + " to " + finish.RegionIndex);
                FindPath(start, targetPoint, finish.RegionIndex, movementProperties, updater, stopCondition);
                return true;
            }

            //if (Vector3.Distance(current.Position, target.Position) <= movementProperties.epsilon)

            FindPath(start, finish, finish.RegionIndex, movementProperties, updater, stopCondition);
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