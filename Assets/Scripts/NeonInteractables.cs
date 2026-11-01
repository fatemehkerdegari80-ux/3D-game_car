using System.Collections.Generic;
using UnityEngine;

namespace NeonHighway
{
    public abstract class Interactable : MonoBehaviour
    {
        public int Lane { get; private set; }
        public bool Active { get; private set; }
        protected float age;
        protected float speedFactor = 1f;
        protected Transform art;

        public virtual void Setup(int lane, float z)
        {
            Lane = lane;
            age = 0f;
            Active = true;
            transform.position = new Vector3((lane - 1) * 3.7f, 0, z);
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
        }

        public virtual void Tick(float dt, float roadSpeed)
        {
            age += dt;
            transform.position += Vector3.back * (roadSpeed * speedFactor * dt);
            if (transform.position.z < -16f) Recycle();
        }

        public bool Touching(Vector3 playerPosition)
        {
            return Active && Mathf.Abs(playerPosition.z - transform.position.z) < 2.50f
                && Mathf.Abs(playerPosition.x - transform.position.x) < 1.65f;
        }

        public virtual void Interact(GameManager game) { }
        public virtual void Recycle()
        {
            Active = false;
            gameObject.SetActive(false);
        }
    }

    public abstract class Obstacle : Interactable
    {
        protected abstract float RelativeSpeed { get; }
        protected virtual int Damage { get { return 1; } }

        public override void Setup(int lane, float z)
        {
            base.Setup(lane, z);
            speedFactor = RelativeSpeed;
        }
        public override void Interact(GameManager game)
        {
            if (game.TakeDamage(Damage)) Recycle();
        }
    }

    // A stalled car: it does not move relative to the road and blinks its hazard lights,
    // which makes it readable at a distance in the dark.
    public sealed class StaticObstacle : Obstacle
    {
        Renderer[] hazards;
        Material hazardOn, hazardOff;
        protected override float RelativeSpeed { get { return 1f; } }

        public void BindHazards(Renderer[] lamps, Material on, Material off)
        {
            hazards = lamps; hazardOn = on; hazardOff = off;
        }

        public override void Tick(float dt, float roadSpeed)
        {
            base.Tick(dt, roadSpeed);
            if (!Active || hazards == null) return;
            var lit = Mathf.Repeat(age * 2.2f + Lane * .37f, 1f) < .5f ? hazardOn : hazardOff;
            for (int i = 0; i < hazards.Length; i++)
                if (hazards[i] != null) hazards[i].sharedMaterial = lit;
        }
    }

    public sealed class MovingObstacle : Obstacle
    {
        protected override float RelativeSpeed { get { return .72f; } }
        public override void Tick(float dt, float roadSpeed)
        {
            base.Tick(dt, roadSpeed);
            if (Active)
            {
                var p = transform.position;
                p.x = (Lane - 1) * 3.7f + Mathf.Sin(age * 1.7f) * .40f;
                transform.position = p;
            }
        }
    }

    public abstract class Collectible : Interactable
    {
        public override void Tick(float dt, float speed)
        {
            base.Tick(dt, speed);
            if (Active && art != null)
            {
                art.Rotate(Vector3.up, 145f * dt, Space.World);
                var p = art.localPosition;
                p.y = 1.5f + Mathf.Sin(age * 5f) * .20f;
                art.localPosition = p;
            }
        }
        public void SetArt(Transform source) { art = source; }
    }

    public sealed class CoinCollectible : Collectible
    {
        public override void Interact(GameManager game)
        {
            game.CollectCoin(transform.position);
            Recycle();
        }
    }

    public sealed class FuelCollectible : Collectible
    {
        public override void Interact(GameManager game)
        {
            game.CollectFuel(transform.position);
            Recycle();
        }
    }

    // All repeatable gameplay actors are pre-created and then merely enabled/disabled.
    public sealed class ObjectPool<T> where T : Interactable
    {
        private readonly List<T> members = new List<T>();
        public ObjectPool(int capacity, System.Func<int, T> build)
        {
            for(int i=0; i<capacity; i++)
            {
                T component = build(i);
                component.Recycle();
                members.Add(component);
            }
        }
        public T Spawn(int lane, float z)
        {
            foreach (var item in members)
                if (!item.Active) { item.Setup(lane,z); return item; }
            return null; // Pool stays fixed-size; no repetitive Instantiate/Destroy.
        }
        public void Tick(float dt, float speed, GameManager game)
        {
            foreach (var item in members)
            {
                if (!item.Active) continue;
                item.Tick(dt, speed);
                if (item.Touching(game.CarPosition)) item.Interact(game);
            }
        }
        public void Clear() { foreach (var x in members) x.Recycle(); }
    }

    public sealed class NeonActors
    {
        readonly ObjectPool<StaticObstacle> staticCars;
        readonly ObjectPool<MovingObstacle> movingCars;
        readonly ObjectPool<CoinCollectible> coins;
        readonly ObjectPool<FuelCollectible> fuels;
        readonly System.Random rng = new System.Random();
        // Varied paint so traffic does not look cloned. Moving cars are cooler / lighter, stalled ones warmer / duller.
        static readonly Color[] StalledPalette =
        {
            new Color(.55f,.12f,.14f), new Color(.14f,.30f,.26f), new Color(.30f,.31f,.36f),
            new Color(.62f,.60f,.55f), new Color(.40f,.26f,.12f)
        };
        static readonly Color[] FlowPalette =
        {
            new Color(.12f,.20f,.42f), new Color(.45f,.45f,.50f), new Color(.08f,.09f,.12f),
            new Color(.50f,.20f,.38f), new Color(.14f,.36f,.40f), new Color(.80f,.78f,.72f)
        };
        static readonly Color TaxiPaint = new Color(.95f,.72f,.12f);
        float spawnCountdown;
        int waveCount;
        public NeonActors(Transform parent)
        {
            staticCars = new ObjectPool<StaticObstacle>(25, i => NewCar<StaticObstacle>(parent, i, StalledPalette[i % StalledPalette.Length], CarStyle.Stalled));
            movingCars = new ObjectPool<MovingObstacle>(20, i => i % 5 == 0
                ? NewCar<MovingObstacle>(parent, i, TaxiPaint, CarStyle.Taxi)
                : NewCar<MovingObstacle>(parent, i, FlowPalette[i % FlowPalette.Length], CarStyle.Standard));
            coins = new ObjectPool<CoinCollectible>(38, i =>
            {
                var holder = new GameObject("Pooled coin " + i);
                holder.transform.SetParent(parent,false);
                var c = holder.AddComponent<CoinCollectible>();
                var art = NeonVisuals.CreateCoin(holder.transform);
                art.transform.localPosition = Vector3.up * 1.5f;
                c.SetArt(art.transform);
                // The halo is a sibling of the art, so it keeps facing the camera while the coin spins.
                NeonVisuals.Halo(holder.transform, "Coin glow", Vector3.up * 1.5f, new Vector2(2.4f, 2.4f), new Color(1f,.78f,.30f,.75f), 1.5f);
                return c;
            });
            fuels = new ObjectPool<FuelCollectible>(14, i =>
            {
                var holder = new GameObject("Pooled fuel " + i);
                holder.transform.SetParent(parent,false);
                var f = holder.AddComponent<FuelCollectible>();
                var art = NeonVisuals.CreateFuel(holder.transform);
                art.transform.localPosition = Vector3.up * 1.4f;
                f.SetArt(art.transform);
                NeonVisuals.Halo(holder.transform, "Fuel glow", Vector3.up * 1.5f, new Vector2(3.6f, 3.6f), new Color(.30f,1f,.72f,.80f), 1.5f);
                // Tall light shaft: fuel cans can be spotted from far down the road.
                NeonVisuals.Halo(holder.transform, "Fuel light shaft", Vector3.up * 8f, new Vector2(1.7f, 16f), new Color(.30f,1f,.72f,.55f), 1.2f);
                return f;
            });
        }

        T NewCar<T>(Transform parent,int i,Color color,CarStyle style) where T:Obstacle
        {
            var holder=new GameObject("Pooled " + typeof(T).Name + " " + i);
            holder.transform.SetParent(parent,false);
            T actor=holder.AddComponent<T>();
            var art=NeonVisuals.CreateCar(holder.transform,color,false,"Traffic car geometry",style);
            art.transform.localScale=Vector3.one * (.89f + ((i * 37) % 5) * .03f);
            var stalled=(object)actor as StaticObstacle;
            if(stalled!=null)
            {
                var lamps=new List<Renderer>();
                foreach(var r in art.GetComponentsInChildren<Renderer>(true))
                    if(r.gameObject.name=="Hazard lamp") lamps.Add(r);
                stalled.BindHazards(lamps.ToArray(),NeonVisuals.HazardOn(),NeonVisuals.HazardOff());
            }
            return actor;
        }

        public void Reset()
        {
            staticCars.Clear(); movingCars.Clear(); coins.Clear(); fuels.Clear();
            spawnCountdown=1f; waveCount=0;
        }

        public void Tick(float dt,float speed,float difficulty,GameManager game)
        {
            staticCars.Tick(dt,speed,game);
            movingCars.Tick(dt,speed,game);
            coins.Tick(dt,speed,game);
            fuels.Tick(dt,speed,game);
            spawnCountdown -= dt;
            if (spawnCountdown>0) return;
            SpawnWave(difficulty);
            spawnCountdown=Mathf.Lerp(1.45f,.64f,difficulty)+Random.Range(-.10f,.16f);
        }

        void SpawnWave(float difficulty)
        {
            waveCount++;
            int blockedLane=rng.Next(3);
            if (rng.NextDouble()<.25 + difficulty*.28) movingCars.Spawn(blockedLane,178f);
            else staticCars.Spawn(blockedLane,178f);
            int safeLane=(blockedLane+1+rng.Next(2))%3;
            int thirdLane=3-blockedLane-safeLane;
            // Keep at least one safe lane: never block the two alternatives with cars.
            if (rng.NextDouble()<.21 + difficulty*.55)
                staticCars.Spawn(thirdLane,178f);
            if (waveCount%4==0 || rng.NextDouble()<.17)
                fuels.Spawn(safeLane,180f);
            else
                coins.Spawn(safeLane,179f);
            if (rng.NextDouble()<.5) coins.Spawn(safeLane,187f);
        }
    }
}
