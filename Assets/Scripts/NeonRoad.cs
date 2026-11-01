using System.Collections.Generic;
using UnityEngine;

namespace NeonHighway
{
    public sealed class NeonRoad
    {
        private const float Length = 40f;
        private readonly List<Transform> tiles = new List<Transform>();
        private readonly Transform parent;
        private float last;
        public NeonRoad(Transform parent)
        {
            this.parent = parent;
            for (int i=0;i<10;i++)
            {
                var go=NeonVisuals.CreateRoadTile(parent,i,Length);
                tiles.Add(go.transform);
            }
            Reset();
        }
        public void Reset()
        {
            for(int i=0;i<tiles.Count;i++) tiles[i].position = new Vector3(0,0,(i-1)*Length);
            last=(tiles.Count-2)*Length;
        }
        public void Tick(float dt,float speed)
        {
            for(int i=0;i<tiles.Count;i++)
            {
                var tile=tiles[i];
                tile.position+=Vector3.back*speed*dt;
                if(tile.position.z < -Length*1.5f)
                {
                    float maximum=float.NegativeInfinity;
                    foreach(var t in tiles) maximum=Mathf.Max(maximum,t.position.z);
                    tile.position=new Vector3(0,0,maximum+Length);
                }
            }
        }
    }
}
