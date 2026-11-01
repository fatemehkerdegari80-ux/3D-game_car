using UnityEngine;

namespace NeonHighway
{
    public sealed class NeonEffects
    {
        readonly ParticleSystem exhaustLeft, exhaustRight;
        readonly ParticleSystem impact, coinPickup, fuelPickup, launchTrail;
        readonly ParticleSystem[] fireworkBursts;
        readonly Light underGlow;
        readonly Transform player;
        readonly Light[] headLights;
        readonly Transform groundGlow;
        readonly Material groundGlowMaterial;
        readonly Renderer underGlowStrip;
        readonly MaterialPropertyBlock stripBlock;
        readonly ParticleSystem speedStreaks;
        float streakCarry;
        float nextFirework;
        float rocketCountdown;
        Vector3 rocketTarget;
        bool rocketInFlight;
        int nextBurst;

        public NeonEffects(Transform parent, Transform player)
        {
            this.player = player;

            exhaustLeft = MakeParticle(parent, "Left rear exhaust smoke", new Color(.46f,.51f,.55f,.66f), .55f, .13f, 1.0f, 80);
            ConfigureExhaust(exhaustLeft, new Vector3(-.46f, .37f, -2.28f));

            exhaustRight = MakeParticle(parent, "Right rear exhaust smoke", new Color(.46f,.51f,.55f,.66f), .55f, .13f, 1.0f, 80);
            ConfigureExhaust(exhaustRight, new Vector3(.46f, .37f, -2.28f));

            impact = MakeParticle(parent, "Impact sparks", new Color(.96f,.61f,.20f,.95f), .42f, .12f, 6.5f, 180);
            ConfigureImpact(impact);

            coinPickup = MakeParticle(parent, "Coin pickup sparkle", new Color(.97f,.80f,.31f,.95f), .46f, .09f, 2.0f, 120);
            ConfigureCoinPickup(coinPickup);

            fuelPickup = MakeParticle(parent, "Fuel pickup energy", new Color(.35f,.90f,.77f,.92f), .58f, .10f, 2.6f, 120);
            ConfigureFuelPickup(fuelPickup);

            launchTrail = MakeParticle(parent, "Sky firework | rising rocket", new Color(.78f,.82f,.76f), .9f, .38f, 0f, 50);
            var launchMain = launchTrail.main;
            launchMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            launchMain.gravityModifier = 0f;
            var launchShape = launchTrail.shape; launchShape.enabled = false;
            EnableTrails(launchTrail, .7f, .74f);

            fireworkBursts = new ParticleSystem[3];
            for (int k = 0; k < fireworkBursts.Length; k++)
            {
                var burst = MakeParticle(parent, "SKY FIREWORK " + (k+1) + " | expanding sparks", Color.white, 2.4f, .38f, 0f, 300);
                var mainBurst = burst.main;
                mainBurst.simulationSpace = ParticleSystemSimulationSpace.Local;
                mainBurst.startLifetime = new ParticleSystem.MinMaxCurve(1.55f, 2.35f);
                mainBurst.startSize = new ParticleSystem.MinMaxCurve(.20f, .42f);
                mainBurst.gravityModifier = .12f;
                var shapeBurst = burst.shape; shapeBurst.enabled = false;
                EnableTrails(burst, .72f, .35f);
                fireworkBursts[k] = burst;
            }

            // Wind streaks: thin stretched light flecks rushing past the car; denser at high speed.
            speedStreaks = MakeParticle(parent, "Speed streaks", new Color(.70f,.88f,1f,.5f), .8f, .07f, 0f, 320);
            ConfigureStreaks(speedStreaks);

            groundGlowMaterial = CreateGlowMaterial();
            groundGlow = CreateGroundGlow(player, groundGlowMaterial);
            var strip = player.Find("AURORA GT - player model/Neon underglow");
            underGlowStrip = strip ? strip.GetComponent<Renderer>() : null;
            stripBlock = new MaterialPropertyBlock();
            underGlow = NewLight(player, "Animated cyan underglow", LightType.Point, NeonVisuals.Cyan, 4f, 9f, new Vector3(0,.30f,0));
            headLights = new Light[2];
            for(int s=0;s<2;s++)
            {
                headLights[s]=NewLight(player,"Headlight cone",LightType.Spot,new Color(.86f,.92f,1f),3f,35f,new Vector3(s==0?-.65f:.65f,.76f,2.03f));
                headLights[s].spotAngle=54f;
                headLights[s].transform.localRotation=Quaternion.Euler(6,0,0);
            }
            nextFirework = 2.0f;
        }

        static Material CreateGlowMaterial()
        {
            var shader=Resources.Load<Shader>("Shaders/SpeedGlow");
            if (!shader) shader=Shader.Find("NeonHighway/SpeedGlow");
            if (!shader) shader=NeonVisuals.FindCompatibleShader();
            var material=new Material(shader) { name="Animated speed-dependent ground neon" };
            if(material.HasProperty("_GlowColor")) material.SetColor("_GlowColor",NeonVisuals.Cyan);
            else if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",NeonVisuals.Cyan);
            else if(material.HasProperty("_Color")) material.SetColor("_Color",NeonVisuals.Cyan);
            return material;
        }

        static Transform CreateGroundGlow(Transform car,Material material)
        {
            var obj=new GameObject("DYNAMIC SPEED NEON | illuminated road halo");
            obj.transform.SetParent(car,false);
            obj.transform.localPosition=new Vector3(0,-.018f,0);
            var filter=obj.AddComponent<MeshFilter>();
            var mesh=new Mesh { name="Feathered elliptical undercar light" };
            const int segments=72, rings=4;
            var vertices=new Vector3[1 + segments*rings];
            var colors=new Color[vertices.Length];
            var triangles=new int[segments*(1+2*(rings-1))*3];
            vertices[0]=Vector3.zero;
            colors[0]=new Color(1,1,1,.34f);
            float[] radius={.30f,.56f,.79f,1.0f};
            float[] alpha={.31f,.22f,.09f,0f};
            for(int ring=0;ring<rings;ring++)
                for(int i=0;i<segments;i++)
                {
                    float angle=i*Mathf.PI*2f/segments;
                    int k=1+ring*segments+i;
                    vertices[k]=new Vector3(Mathf.Cos(angle)*2.55f*radius[ring],0,
                                             Mathf.Sin(angle)*3.65f*radius[ring]);
                    colors[k]=new Color(1,1,1,alpha[ring]);
                }
            int t=0;
            for(int i=0;i<segments;i++)
            {
                int j=(i+1)%segments;
                triangles[t++]=0; triangles[t++]=1+j; triangles[t++]=1+i;
            }
            for(int ring=0;ring<rings-1;ring++)
                for(int i=0;i<segments;i++)
                {
                    int j=(i+1)%segments;
                    int a=1+ring*segments+i, b=1+ring*segments+j;
                    int c=1+(ring+1)*segments+i, d=1+(ring+1)*segments+j;
                    triangles[t++]=a;triangles[t++]=b;triangles[t++]=c;
                    triangles[t++]=b;triangles[t++]=d;triangles[t++]=c;
                }
            mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;
            mesh.RecalculateBounds();
            filter.sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            return obj.transform;
        }

        static Light NewLight(Transform parent,string label,LightType type,Color color,float intensity,float range,Vector3 pos)
        {
            var g=new GameObject(label);g.transform.SetParent(parent,false);g.transform.localPosition=pos;
            var l=g.AddComponent<Light>();l.type=type;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;
            return l;
        }

        static ParticleSystem MakeParticle(Transform parent,string title,Color tint,float life,float size,float speed,int max)
        {
            var g=new GameObject(title);g.transform.SetParent(parent,false);
            var ps=g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.loop=false;main.duration=2f;main.playOnAwake=false;
            main.startLifetime=life;main.startSize=size;main.startSpeed=speed;main.startColor=tint;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=max;
            var emission=ps.emission;emission.enabled=true;emission.rateOverTime=0;
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=16f;
            var renderer=g.GetComponent<ParticleSystemRenderer>();
            var shader=Resources.Load<Shader>("Shaders/SoftParticle");
            if(!shader) shader=Shader.Find("NeonHighway/SoftParticle");
            if(!shader) shader=NeonVisuals.FindCompatibleShader(true);
            var material=new Material(shader){name="Soft particle: "+title};
            if(material.HasProperty("_Tint")) material.SetColor("_Tint",Color.white);
            if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",Color.white);
            if(material.HasProperty("_Color")) material.SetColor("_Color",Color.white);
            if(material.HasProperty("_Brightness")) material.SetFloat("_Brightness",1.15f);
            renderer.sharedMaterial=material;
            renderer.trailMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.alignment=ParticleSystemRenderSpace.View;
            return ps;
        }

        void ConfigureExhaust(ParticleSystem ps, Vector3 localPosition)
        {
            // The camera looks in +Z and the car's rear is -Z. Both outlets are
            // slightly BEHIND the rear bumper. World simulation leaves smoke behind.
            ps.transform.SetParent(player, false);
            ps.transform.localPosition = new Vector3(localPosition.x, .51f, -2.43f);
            ps.transform.localRotation = Quaternion.Euler(-9f, 180f, 0f);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.15f, 2.25f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.75f, 1.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(.27f, .46f);
            main.startColor = new Color(.67f,.72f,.76f,.92f);
            main.gravityModifier = 0f;
            main.maxParticles = 180;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 28f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 13f;
            shape.radius = .035f;

            // Do not mix MinMaxCurve modes in Unity's VelocityOverLifetime.
            // Cone rotation itself gives a reliable backward trajectory.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = false;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = false;
            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(0f);
            force.y = new ParticleSystem.MinMaxCurve(.30f);
            force.z = new ParticleSystem.MinMaxCurve(-.30f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .27f;
            noise.frequency = .45f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f,.38f), new Keyframe(.28f,1.0f), new Keyframe(1f,1.72f)));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(.76f,.79f,.83f), 0f),
                    new GradientColorKey(new Color(.62f,.67f,.71f), .38f),
                    new GradientColorKey(new Color(.42f,.48f,.53f), 1f)
                },
                new[] {
                    new GradientAlphaKey(.58f, 0f),
                    new GradientAlphaKey(.42f, .22f),
                    new GradientAlphaKey(.17f, .65f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            // Alpha blending (not additive) makes wispy grey exhaust visible
            // and avoids the old bright square-looking particles.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var smokeShader = Resources.Load<Shader>("Shaders/SoftSmoke");
            if (!smokeShader) smokeShader = Shader.Find("NeonHighway/SoftSmoke");
            if (smokeShader)
            {
                var smokeMaterial = new Material(smokeShader) { name="Feathered alpha smoke" };
                renderer.sharedMaterial = smokeMaterial;
                renderer.trailMaterial = smokeMaterial;
            }
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // Previous patch configured exhaust but never actually STARTED it.
            ps.Play();
        }

        static void ConfigureStreaks(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape; shape.enabled = false;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .05f;
            r.lengthScale = 1.4f;
            r.cameraVelocityScale = 0f;
            ps.Play();
        }

        void EmitStreaks(float dt, float normalized, float speed)
        {
            streakCarry += dt * Mathf.Lerp(16f, 72f, normalized);
            int count = Mathf.Min((int)streakCarry, 8);
            streakCarry -= (int)streakCarry;
            Vector3 origin = player.position;
            for (int i = 0; i < count; i++)
            {
                float z = Random.Range(20f, 90f);
                float side = Random.value < .5f ? -1f : 1f;
                bool overhead = Random.value < .22f;
                var p = new ParticleSystem.EmitParams();
                p.position = new Vector3(origin.x + (overhead ? Random.Range(-6f, 6f) : side * Random.Range(5.4f, 20f)),
                                         overhead ? Random.Range(5f, 9f) : Random.Range(.25f, 6.5f), z);
                p.velocity = new Vector3(0f, 0f, -speed);
                p.startLifetime = Mathf.Clamp((z + 14f) / Mathf.Max(8f, speed), .25f, 2.2f);
                p.startSize = Random.Range(.035f, .085f);
                p.startColor = new Color(.72f, .88f, 1f, Random.Range(.22f, .52f));
                speedStreaks.Emit(p, 1);
            }
        }

        static void ConfigureImpact(ParticleSystem ps)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.8f, 10.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.10f, .24f);
            var shape = ps.shape;
            shape.enabled = false;
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = .55f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(.17f);
            trails.minVertexDistance = .06f;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        }

        static void ConfigureCoinPickup(ParticleSystem ps)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .52f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 5.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.16f, .32f);
            var shape = ps.shape;
            shape.enabled = false;
            var noise = ps.noise; noise.enabled = true; noise.strength = .22f; noise.frequency = .75f;
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = .38f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(.12f);
            trails.minVertexDistance = .08f;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        }

        static void ConfigureFuelPickup(ParticleSystem ps)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.30f, .74f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.65f, 2.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(.19f, .42f);
            var shape = ps.shape;
            shape.enabled = false;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // Explicitly configure X/Y/Z in Constant mode (no mixed curve modes).
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(.65f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = .30f; noise.frequency = .55f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(.44f,.99f,.80f), 0f),
                    new GradientColorKey(new Color(.35f,.85f,.76f), .55f),
                    new GradientColorKey(new Color(.28f,.58f,.61f), 1f)
                },
                new[] {
                    new GradientAlphaKey(.72f, 0f),
                    new GradientAlphaKey(.34f, .50f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        static void EnableTrails(ParticleSystem system,float ratio,float lifetime)
        {
            var trails=system.trails;
            trails.enabled=true;
            trails.mode=ParticleSystemTrailMode.PerParticle;
            trails.ratio=ratio;
            trails.lifetime=new ParticleSystem.MinMaxCurve(lifetime);
            trails.minVertexDistance=.15f;
            trails.textureMode=ParticleSystemTrailTextureMode.Stretch;
        }

        public void Tick(float dt,float speed,float elapsed,bool playing)
        {
            float normalized=Mathf.InverseLerp(27f,61f,speed);
            float pulse=1f+.14f*Mathf.Sin(Time.unscaledTime*(5.3f+normalized*7.5f));
            float glowStrength=Mathf.Lerp(.9f,2.55f,normalized)*pulse;
            // Neon drifts from cool cyan towards electric violet as the car gains speed.
            Color baseNeon=Color.Lerp(NeonVisuals.Cyan,NeonVisuals.Violet,normalized*.65f);
            if(groundGlowMaterial)
            {
                Color c=baseNeon*(glowStrength*.85f); c.a=1f;
                if(groundGlowMaterial.HasProperty("_GlowColor")) groundGlowMaterial.SetColor("_GlowColor",c);
                else if(groundGlowMaterial.HasProperty("_BaseColor")) groundGlowMaterial.SetColor("_BaseColor",c);
                else if(groundGlowMaterial.HasProperty("_Color")) groundGlowMaterial.SetColor("_Color",c);
            }
            if(groundGlow)
                groundGlow.localScale=new Vector3(Mathf.Lerp(.85f,1.40f,normalized),1f, Mathf.Lerp(.94f,1.22f,normalized));
            if(underGlowStrip)
            {
                underGlowStrip.GetPropertyBlock(stripBlock);
                var stripColor=baseNeon;
                stripBlock.SetColor("_BaseColor",stripColor * Mathf.Lerp(.7f,1.5f,normalized)*pulse);
                stripBlock.SetColor("_Color",stripColor * Mathf.Lerp(.7f,1.5f,normalized)*pulse);
                stripBlock.SetColor("_EmissionColor",stripColor * Mathf.Lerp(1.3f,3.5f,normalized)*pulse);
                underGlowStrip.SetPropertyBlock(stripBlock);
            }
            underGlow.color=baseNeon;
            underGlow.intensity=Mathf.Lerp(2.5f,7.5f,normalized)*pulse;
            underGlow.range=Mathf.Lerp(7f,13f,normalized);
            foreach(var h in headLights) h.intensity=Mathf.Lerp(2.0f,4.3f,normalized);

            AnimateExhaust(exhaustLeft, normalized, playing);
            AnimateExhaust(exhaustRight, normalized, playing);
            if(playing && dt>0f) EmitStreaks(dt, normalized, speed);

            if(rocketInFlight)
            {
                rocketCountdown-=dt;
                if(rocketCountdown<=0f)
                {
                    rocketInFlight=false;
                    TriggerFirework(rocketTarget);
                }
            }
            if(!playing)return;
            nextFirework-=dt;
            if(nextFirework<=0f)
            {
                LaunchFirework();
                nextFirework=Random.Range(6.4f,9.8f);
            }
        }

        static void AnimateExhaust(ParticleSystem ps, float normalized, bool playing)
        {
            var emission = ps.emission;
            emission.rateOverTime = playing ? Mathf.Lerp(24f, 45f, normalized) : 0f;
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Lerp(1.0f, 1.50f, normalized), Mathf.Lerp(2.2f, 3.2f, normalized));
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(.24f, .30f, normalized), Mathf.Lerp(.42f, .54f, normalized));
            main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Lerp(.75f, .85f, normalized), Mathf.Lerp(1.25f, 1.48f, normalized));
        }

        public void DebugFirework()
        {
            LaunchFirework();
            nextFirework=Random.Range(7f,10f);
        }

        void LaunchFirework()
        {
            float x=Random.Range(-16f,16f);
            float z=Random.Range(67f,88f);
            float targetHeight=Random.Range(23f,30f);
            rocketTarget=new Vector3(x,targetHeight,z);
            launchTrail.transform.position=new Vector3(x,9f,z);
            launchTrail.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            launchTrail.Play();
            var p=new ParticleSystem.EmitParams();
            p.position=Vector3.zero;
            p.velocity=new Vector3(0,(targetHeight-9f)/.84f,0);
            p.startColor=new Color(.76f,.85f,.79f);
            p.startSize=.46f;
            p.startLifetime=.84f;
            launchTrail.Emit(p,1);
            rocketCountdown=.84f;
            rocketInFlight=true;
        }

        void TriggerFirework(Vector3 location)
        {
            var burst=fireworkBursts[nextBurst];
            nextBurst=(nextBurst+1)%fireworkBursts.Length;
            burst.transform.position=location;
            burst.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            burst.Play();
            Color[] palette={ NeonVisuals.Pink, NeonVisuals.Cyan, NeonVisuals.Gold, NeonVisuals.Violet, NeonVisuals.Mint, new Color(1f,.45f,.25f)};
            Color c=palette[Random.Range(0,palette.Length)];
            int rays=22;
            for(int ray=0;ray<rays;ray++)
            {
                float angle=ray*Mathf.PI*2f/rays;
                for(int j=0;j<5;j++)
                {
                    float elevation=Random.Range(-.72f,.73f);
                    var direction=new Vector3(Mathf.Cos(angle),elevation,Mathf.Sin(angle)).normalized;
                    var p=new ParticleSystem.EmitParams();
                    p.position=Vector3.zero;
                    p.velocity=direction*Random.Range(5.3f,10.4f);
                    p.startLifetime=Random.Range(1.4f,2.45f);
                    p.startSize=Random.Range(.25f,.57f);
                    p.startColor=Color.Lerp(c,Color.white,Random.Range(0f,.25f));
                    burst.Emit(p,1);
                }
            }
            for(int i=0;i<34;i++)
            {
                var p=new ParticleSystem.EmitParams();
                p.position=Vector3.zero;
                p.velocity=Random.insideUnitSphere*Random.Range(1.3f,4.8f);
                p.startLifetime=Random.Range(.45f,1.15f);
                p.startSize=Random.Range(.24f,.50f);
                p.startColor=Color.Lerp(c,Color.white,.55f);
                burst.Emit(p,1);
            }
        }

        public void Crash(Vector3 world)
        {
            impact.transform.position = world + Vector3.up * .82f;
            for (int i=0;i<70;i++)
            {
                var p = new ParticleSystem.EmitParams();
                Vector3 dir = (Random.onUnitSphere + Vector3.up * .5f).normalized;
                p.position = Vector3.zero;
                p.velocity = dir * Random.Range(3.5f, 8.8f);
                p.startLifetime = Random.Range(.15f, .42f);
                p.startSize = Random.Range(.10f, .22f);
                p.startColor = Color.Lerp(new Color(.92f,.42f,.16f), new Color(1f,.85f,.40f), Random.Range(0f,1f));
                impact.Emit(p, 1);
            }
        }

        public void Collect(Vector3 world,bool isFuel)
        {
            if (isFuel)
            {
                fuelPickup.transform.position = world + Vector3.up * 1.1f;
                for (int i=0;i<35;i++)
                {
                    var p = new ParticleSystem.EmitParams();
                    p.position = Random.insideUnitSphere * .10f;
                    p.velocity = new Vector3(Random.Range(-.6f,.6f), Random.Range(.9f,2.2f), Random.Range(-.6f,.6f));
                    p.startLifetime = Random.Range(.28f,.72f);
                    p.startSize = Random.Range(.20f,.38f);
                    p.startColor = Color.Lerp(new Color(.43f,1f,.83f), new Color(.22f,.73f,.66f), Random.Range(0f,1f));
                    fuelPickup.Emit(p,1);
                }
                var center=new ParticleSystem.EmitParams();
                center.position=Vector3.zero;
                center.velocity=new Vector3(0,.35f,0);
                center.startSize=.67f;
                center.startLifetime=.27f;
                center.startColor=new Color(.37f,.95f,.73f,.48f);
                fuelPickup.Emit(center,1);
            }
            else
            {
                coinPickup.transform.position = world + Vector3.up * 1.0f;
                for (int i=0;i<30;i++)
                {
                    Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(.25f, 1.05f);
                    var p = new ParticleSystem.EmitParams();
                    p.position = new Vector3(0, Random.Range(-.05f,.05f), 0);
                    p.velocity = new Vector3(circle.x, Random.Range(.7f,1.8f), circle.y) * Random.Range(.8f, 2.2f);
                    p.startLifetime = Random.Range(.18f,.48f);
                    p.startSize = Random.Range(.14f,.29f);
                    p.startColor = Color.Lerp(new Color(1f,.82f,.26f), new Color(1f,.96f,.74f), Random.Range(0f,1f));
                    coinPickup.Emit(p,1);
                }
                var center=new ParticleSystem.EmitParams();
                center.position=Vector3.zero;
                center.velocity=new Vector3(0,.25f,0);
                center.startSize=.52f;
                center.startLifetime=.22f;
                center.startColor=new Color(1f,.87f,.49f,.72f);
                coinPickup.Emit(center,1);
            }
        }

        public void Reset()
        {
            nextFirework=2f;
            rocketInFlight=false;rocketCountdown=0f;
            impact.Clear();coinPickup.Clear();fuelPickup.Clear();launchTrail.Clear();speedStreaks.Clear();streakCarry=0f;
            exhaustLeft.Clear(); exhaustRight.Clear();
            if (!exhaustLeft.isPlaying) exhaustLeft.Play();
            if (!exhaustRight.isPlaying) exhaustRight.Play();
            foreach(var b in fireworkBursts) b.Clear();
        }
    }

    public sealed class NeonAudio
    {
        readonly AudioSource music, engine, effects;
        readonly AudioClip coin, fuel, hit, click;
        public NeonAudio(Transform parent)
        {
            music=CreateSource(parent,"Original ambient soundtrack",.36f,true);
            engine=CreateSource(parent,"Engine audio",.40f,true);
            effects=CreateSource(parent,"One shot effects",.67f,false);
            music.clip=Resources.Load<AudioClip>("Audio/NightDriveMusic");
            engine.clip=Resources.Load<AudioClip>("Audio/EngineLoop");
            coin=Resources.Load<AudioClip>("Audio/Coin");
            fuel=Resources.Load<AudioClip>("Audio/Fuel");
            hit=Resources.Load<AudioClip>("Audio/Crash");
            click=Resources.Load<AudioClip>("Audio/Menu");
            if(music.clip)music.Play();
            if(engine.clip)engine.Play();
        }
        static AudioSource CreateSource(Transform parent,string name,float volume,bool loop)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var s=go.AddComponent<AudioSource>();s.loop=loop;s.spatialBlend=0;s.volume=volume;s.playOnAwake=false;
            return s;
        }
        public void Tick(float speed,bool driving)
        {
            engine.pitch=driving?Mathf.Lerp(.82f,1.58f,Mathf.InverseLerp(22,64,speed)):.64f;
            engine.volume=driving?.33f:.10f;
        }
        public void Coin(){if(coin)effects.PlayOneShot(coin);}
        public void Fuel(){if(fuel)effects.PlayOneShot(fuel);}
        public void Crash(){if(hit)effects.PlayOneShot(hit);}
        public void Click(){if(click)effects.PlayOneShot(click);}
    }
}
