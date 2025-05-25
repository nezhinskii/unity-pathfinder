using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// По-хорошему это октодерево должно быть, но неохота.
/// Класс, владеющий полной информацией о сцене - какие области где расположены, 
/// как связаны между собой, и прочая информация.
/// Должен по координатам точки определять номер области.
/// </summary>

namespace BaseAI
{
    /// <summary>
    /// Базовый класс для реализации региона - квадратной или круглой области
    /// </summary>


    public class Cartographer: MonoBehaviour
    {
        public List<PathNode> pathNodes = new List<PathNode>();

        public List<MovingObstacle> movingObstacles = new List<MovingObstacle>();
        //  Список регионов
        public List<IBaseRegion> regions = new List<IBaseRegion>();

        public List<IBaseRegion> noTerrainRegions = new List<IBaseRegion>();

        //  Поверхность (Terrain) сцены
        public Terrain SceneTerrain;

        public Cartographer(GameObject collidersCollection)
        {
            MovingObstacle[] obstacles = FindObjectsOfType<MovingObstacle>();
            movingObstacles.AddRange(obstacles);
            regions = new List<IBaseRegion>();
            noTerrainRegions = new List<IBaseRegion>();
            SceneTerrain = Terrain.activeTerrain;

            foreach (var region in collidersCollection.GetComponentsInChildren<IBaseRegion>())
            {
                regions.Add(region);
                region.index = regions.Count - 1;
                if (region.Dynamic || region.GetType() == typeof(Region))
                {
                    noTerrainRegions.Add(region);
                }
            }
            for (int i = 0; i < regions.Count; ++i)
                Debug.Log("Region : " + i + " -> " + regions[i].GetCenter().ToString());

            if (regions.Count < 2)
            {
                return;
            }

            if (regions.Count == 13) // Сцена Лаб3
            {
                regions[0].Neighbors.Add(regions[1]);

                regions[1].Neighbors.Add(regions[0]);
                regions[1].Neighbors.Add(regions[2]);

                regions[2].Neighbors.Add(regions[1]);
                regions[2].Neighbors.Add(regions[3]);

                regions[3].Neighbors.Add(regions[2]);
                regions[3].Neighbors.Add(regions[4]);

                regions[4].Neighbors.Add(regions[3]);
                regions[4].Neighbors.Add(regions[5]);
                regions[4].Neighbors.Add(regions[6]);

                regions[5].Neighbors.Add(regions[4]);
                regions[5].Neighbors.Add(regions[7]);

                regions[6].Neighbors.Add(regions[4]);
                regions[6].Neighbors.Add(regions[7]);

                regions[7].Neighbors.Add(regions[5]);
                regions[7].Neighbors.Add(regions[6]);
                regions[7].Neighbors.Add(regions[8]);
                regions[7].Neighbors.Add(regions[9]);

                regions[8].Neighbors.Add(regions[7]);
                regions[8].Neighbors.Add(regions[10]);

                regions[9].Neighbors.Add(regions[7]);
                regions[9].Neighbors.Add(regions[10]);

                regions[10].Neighbors.Add(regions[8]);
                regions[10].Neighbors.Add(regions[9]);
                regions[10].Neighbors.Add(regions[11]);

                regions[11].Neighbors.Add(regions[10]);
                regions[11].Neighbors.Add(regions[12]);

                regions[12].Neighbors.Add(regions[11]);
            }
            else // Сцена Лаб2
            {
                regions[0].Neighbors.Add(regions[1]);
                regions[0].Neighbors.Add(regions[3]);

                regions[1].Neighbors.Add(regions[0]);
                regions[1].Neighbors.Add(regions[2]);

                regions[2].Neighbors.Add(regions[1]);
                regions[2].Neighbors.Add(regions[4]);

                regions[3].Neighbors.Add(regions[0]);
                regions[3].Neighbors.Add(regions[5]);

                regions[4].Neighbors.Add(regions[2]);

                regions[5].Neighbors.Add(regions[3]);
                regions[5].Neighbors.Add(regions[6]);

                regions[6].Neighbors.Add(regions[5]);
                regions[6].Neighbors.Add(regions[7]);

                regions[7].Neighbors.Add(regions[6]);
                regions[7].Neighbors.Add(regions[8]);

                regions[8].Neighbors.Add(regions[9]);

                regions[9].Neighbors.Add(regions[8]);
            }
        }

        /// <summary>
        /// Регион, которому принадлежит точка. Сделать абы как
        /// </summary>
        /// <param name="node"></param>
        /// <returns>Индекс региона, -1 если не принадлежит (не проходима)</returns>
        // public IBaseRegion GetRegion(PathNode node)
        // {
        //     for (var i = 0; i < regions.Count; ++i)
        //         //  Метод полиморфный и для всяких платформ должен быть корректно в них реализован
        //         if (regions[i].Contains(node))
        //             return regions[i];
        //     //Debug.Log("Not found region for " + node.Position.ToString());
        //     return null;
        // }

        public IBaseRegion[] GetRegions(PathNode node)
        {
            List<IBaseRegion> res = new List<IBaseRegion>();
            PathNode globalNode = node;

            foreach (var region in regions)
            {
                if (region.Contains(globalNode))
                {
                    res.Add(region);
                }
            }
            return res.ToArray();
        }

        public bool IsInRegion(PathNode node, int RegionIndex)
        {
            return RegionIndex >= 0 && RegionIndex < regions.Count && regions[RegionIndex].Contains(node);
        }
    }
}