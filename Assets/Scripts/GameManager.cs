using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonHighway
{
    public enum GameState { Menu, Playing, Paused, GameOver }

    // Singleton coordinator. UI and gameplay both ask this single object for game state.
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public GameState State { get; private set; } = GameState.Menu;
        public Vector3 CarPosition { get { return carRoot != null ? carRoot.position : Vector3.zero; } }
        public float Speed { get { return currentSpeed; } }
        public int Best { get { return best; } }

        Transform carRoot;
        Renderer carPaint;
        MaterialPropertyBlock paintBlock;
        Camera camera3D;
        NeonRoad road;
        NeonActors actors;
        NeonEffects effects;
        NeonAudio audioSystem;
        NeonInterface ui;
        InputActionAsset actionAsset;
        InputActionMap driveMap;
        InputAction left, right, pauseAction, confirmAction, fireworkAction;
        float currentSpeed, distance, fuel, damageCooldown, cameraShake, hitFlash, elapsed;
        // Presentation-only state (never affects gameplay rules).
        float bodyYaw, bodyRoll, camRoll, fovNow = 62f;
        Vector3 lookNow = new Vector3(0, 1.1f, 21.5f);
        int lane=1, lives=3, coins, best;
        const string SaveKey = "NeonHighway_BestScore_v1";

        public int Score { get { return Mathf.FloorToInt(distance*.65f)+coins*50; } }

        void Awake()
        {
            if(Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance=this;
            paintBlock = new MaterialPropertyBlock(); // Native Unity objects must be created in Awake/Start.
            Application.targetFrameRate=90;
        }

        void Start()
        {
            best=PlayerPrefs.GetInt(SaveKey,0);
            SetupWorld();
            road=new NeonRoad(MakeGroup("Recycled procedural road"));
            actors=new NeonActors(MakeGroup("Object pools | obstacles, coins, fuel"));
            effects=new NeonEffects(MakeGroup("Particles and lighting"),carRoot);
            audioSystem=new NeonAudio(MakeGroup("Procedural original audio"));
            ui=new NeonInterface(this);
            SetupActions();
            ResetWorld();
            ReturnToMenu();
        }

        Transform MakeGroup(string label)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            return go.transform;
        }

        static Light MakeDirectional(Transform parent,string label,Color color,float intensity,Vector3 euler)
        {
            var go=new GameObject(label,typeof(Light));
            go.transform.SetParent(parent,false);
            var l=go.GetComponent<Light>();
            l.type=LightType.Directional;
            l.color=color;
            l.intensity=intensity;
            l.shadows=LightShadows.None;
            go.transform.rotation=Quaternion.Euler(euler);
            return l;
        }

        void SetupWorld()
        {
            // Gradient ambient (indigo sky, plum horizon, near-black ground): richer than a flat grey fill.
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.13f,.10f,.27f);
            RenderSettings.ambientEquatorColor=new Color(.11f,.07f,.18f);
            RenderSettings.ambientGroundColor=new Color(.03f,.03f,.06f);
            RenderSettings.fog=true;
            RenderSettings.fogColor=NeonVisuals.FogColor;
            RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=.0062f;
            camera3D=Camera.main;
            if (!camera3D)
            {
                var camObj=new GameObject("Third person camera",typeof(Camera),typeof(AudioListener));
                camObj.tag="MainCamera";
                camera3D=camObj.GetComponent<Camera>();
            }
            bool skyboxReady=NeonVisuals.ConfigureNightSkybox();
            camera3D.clearFlags=skyboxReady ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera3D.backgroundColor=new Color(.04f,.025f,.08f);
            camera3D.allowHDR=true;
            camera3D.fieldOfView=fovNow;
            camera3D.nearClipPlane=.1f;
            camera3D.farClipPlane=330f;
            camera3D.transform.position=new Vector3(0,7.0f,-12.8f);
            camera3D.transform.LookAt(lookNow);
            NeonVisuals.CreateMoonAndStars(MakeGroup("Procedural star sky"));
            // Key light (cool moon) from behind the camera, plus a magenta rim light from ahead
            // that catches the roof, hood and spoiler edges of the cars.
            MakeDirectional(transform,"Moonlight",new Color(.52f,.58f,.92f),.62f,new Vector3(52f,-30f,0f));
            MakeDirectional(transform,"Magenta rim light",new Color(1f,.34f,.64f),.42f,new Vector3(34f,205f,0f));
            var playerHolder=new GameObject("PLAYER | Lane switching car");
            playerHolder.transform.SetParent(transform,false);
            carRoot=playerHolder.transform;
            NeonVisuals.CreateCar(carRoot,NeonVisuals.PlayerPaint,true,"AURORA GT - player model");
            carPaint=carRoot.Find("AURORA GT - player model/Aero chassis").GetComponent<Renderer>();
        }

        void SetupActions()
        {
            actionAsset=Resources.Load<InputActionAsset>("Inputs/NeonControls");
            if (actionAsset==null)
            {
                Debug.LogError("NeonControls.inputactions could not be imported. Check Input System package installation.");
                return;
            }
            // Asset contains a real Action Map and individual Button InputActions.
            driveMap=actionAsset.FindActionMap("Drive",true);
            left=driveMap.FindAction("Left",true);
            right=driveMap.FindAction("Right",true);
            pauseAction=driveMap.FindAction("Pause",true);
            confirmAction=driveMap.FindAction("Confirm",true);
            fireworkAction=driveMap.FindAction("FireworkTest",false);
            left.performed += OnLeft;
            right.performed += OnRight;
            pauseAction.performed += OnPause;
            confirmAction.performed += OnConfirm;
            if(fireworkAction!=null) fireworkAction.performed += OnFireworkTest;
            driveMap.Enable();
        }
        void OnFireworkTest(InputAction.CallbackContext ctx) { if (effects!=null && State!=GameState.Paused) effects.DebugFirework(); }
        void OnLeft(InputAction.CallbackContext ctx) { if (State==GameState.Playing) MoveLane(-1); }
        void OnRight(InputAction.CallbackContext ctx){ if (State==GameState.Playing) MoveLane(1); }
        void OnPause(InputAction.CallbackContext ctx){ if (State==GameState.Playing || State==GameState.Paused) TogglePause(); }
        void OnConfirm(InputAction.CallbackContext ctx)
        {
            if(State==GameState.Menu) Begin();
            else if(State==GameState.GameOver) Restart();
        }
        void OnDisable()
        {
            if(driveMap!=null)driveMap.Disable();
            if(left!=null)left.performed-=OnLeft;
            if(right!=null)right.performed-=OnRight;
            if(pauseAction!=null)pauseAction.performed-=OnPause;
            if(confirmAction!=null)confirmAction.performed-=OnConfirm;
            if(fireworkAction!=null)fireworkAction.performed-=OnFireworkTest;
        }
        void MoveLane(int direction)
        {
            lane=Mathf.Clamp(lane+direction,0,2);
            if (audioSystem!=null) audioSystem.Click();
        }

        void Update()
        {
            if(ui==null)return;
            float dt=Time.unscaledDeltaTime;
            if(State==GameState.Playing)
            {
                elapsed+=dt;
                float difficulty=Mathf.Clamp01((elapsed+distance*.015f)/190f);
                currentSpeed=Mathf.Lerp(27f,61f,difficulty);
                distance+=currentSpeed*dt;
                fuel=Mathf.Max(0f,fuel-dt*(1.45f+Mathf.Lerp(0f,.78f,difficulty)));
                if(damageCooldown>0)damageCooldown-=dt;
                if(hitFlash>0)hitFlash-=dt;
                road.Tick(dt,currentSpeed);
                actors.Tick(dt,currentSpeed,difficulty,this);
                if(State==GameState.Playing && fuel<=0)EndGame();
            }
            else if(State==GameState.Menu)
            {
                // Attract mode: the empty road keeps rolling behind the main menu.
                road.Tick(dt,currentSpeed);
            }
            float norm=Mathf.InverseLerp(27f,61f,currentSpeed);
            if(carRoot)
            {
                var p=carRoot.position;
                float oldX=p.x;
                p.x=Mathf.MoveTowards(p.x,(lane-1)*3.7f,dt*13f);
                carRoot.position=p;
                // Body language: the car yaws and leans into lane changes and gently bobs on its suspension.
                float vx=dt>0f?(p.x-oldX)/dt:0f;
                float k=Mathf.Clamp01(dt*10f);
                bodyYaw=Mathf.Lerp(bodyYaw,Mathf.Clamp(vx*1.15f,-14f,14f),k);
                bodyRoll=Mathf.Lerp(bodyRoll,Mathf.Clamp(vx*.55f,-8f,8f),k);
                camRoll=Mathf.Lerp(camRoll,Mathf.Clamp(-vx*.22f,-3f,3f),Mathf.Clamp01(dt*6f));
                float bob=State==GameState.Playing||State==GameState.Menu ? Mathf.Sin(Time.unscaledTime*(11f+norm*9f))*.30f*(.4f+norm) : 0f;
                carRoot.rotation=Quaternion.Euler(bob,bodyYaw,bodyRoll);
                if(carPaint)
                {
                    carPaint.GetPropertyBlock(paintBlock);
                    var bodyColor = hitFlash>0 ? Color.Lerp(new Color(.94f,.24f,.19f), new Color(1f,.74f,.36f), .5f + .5f*Mathf.Sin(Time.unscaledTime*28f)) : NeonVisuals.PlayerPaint;
                    paintBlock.SetColor("_Color", bodyColor);      // Built-in render pipeline
                    paintBlock.SetColor("_BaseColor", bodyColor);  // Unity 6 URP Lit shader
                    carPaint.SetPropertyBlock(paintBlock);
                }
            }
            if(camera3D)
            {
                if(cameraShake>0)cameraShake-=dt;
                Vector3 target, look;
                float fov;
                if(State==GameState.Menu)
                {
                    // Slow cinematic orbit; the car sits on the right third so the left-aligned menu has room.
                    float t=Time.unscaledTime;
                    target=new Vector3(carRoot.position.x+1.4f+Mathf.Sin(t*.33f)*1.9f,2.35f+Mathf.Sin(t*.47f)*.30f,-7.4f+Mathf.Cos(t*.26f)*.9f);
                    look=new Vector3(carRoot.position.x-5.2f,1.25f,7.5f);
                    fov=54f;
                }
                else
                {
                    target=new Vector3(carRoot.position.x*.60f,7.0f,-12.8f-1.5f*norm);
                    look=new Vector3(carRoot.position.x*.23f,1.1f,21.5f);
                    fov=Mathf.Lerp(64f,77f,norm);   // FOV opens up with speed
                }
                if(cameraShake>0) target+=Random.insideUnitSphere * Mathf.Min(cameraShake,.52f);
                float follow=Mathf.Clamp01(dt*3.8f);
                camera3D.transform.position=Vector3.Lerp(camera3D.transform.position,target,follow);
                lookNow=Vector3.Lerp(lookNow,look,follow);
                fovNow=Mathf.Lerp(fovNow,fov,Mathf.Clamp01(dt*2.5f));
                camera3D.fieldOfView=fovNow;
                camera3D.transform.LookAt(lookNow);
                camera3D.transform.Rotate(0f,0f,camRoll,Space.Self);
            }
            float fxDt=State==GameState.Paused?0f:dt;
            effects.Tick(fxDt,currentSpeed,elapsed,State==GameState.Playing||State==GameState.Menu);
            audioSystem.Tick(currentSpeed,State==GameState.Playing);
            if(State==GameState.Playing)ui.UpdateHUD(currentSpeed,fuel,lives,Score,Mathf.Max(best,Score),distance);
            ui.Tick(dt);
        }

        public bool TakeDamage(int damage)
        {
            if(State!=GameState.Playing || damageCooldown>0)return false;
            lives=Mathf.Max(0,lives-damage);
            damageCooldown=1.35f;cameraShake=1.15f;hitFlash=1.15f;
            effects.Crash(CarPosition);audioSystem.Crash();
            ui.FlashDamage();
            if(lives<=0)EndGame();
            return true;
        }
        public void CollectCoin(Vector3 where)
        {
            if(State!=GameState.Playing)return;
            coins++;effects.Collect(where,false);audioSystem.Coin();
            ui.Popup("+50",where,NeonVisuals.Gold);
        }
        public void CollectFuel(Vector3 where)
        {
            if(State!=GameState.Playing)return;
            fuel=Mathf.Min(100f,fuel+30f);
            effects.Collect(where,true);audioSystem.Fuel();
            ui.Popup("+30 FUEL",where,NeonVisuals.Mint);
        }

        void ResetWorld()
        {
            lane=1;lives=3;fuel=100f;distance=0;coins=0;elapsed=0;
            currentSpeed=27f;damageCooldown=0;hitFlash=0;cameraShake=0;
            bodyYaw=0f;bodyRoll=0f;camRoll=0f;
            if(carRoot){carRoot.position=Vector3.zero;carRoot.rotation=Quaternion.identity;}
            if(road!=null)road.Reset();
            if(actors!=null)actors.Reset();
            if(effects!=null)effects.Reset();
        }
        public void Begin()
        {
            ResetWorld();State=GameState.Playing;Time.timeScale=1f;
            audioSystem.Click();ui.ShowPlay();
        }
        public void Restart(){Begin();}
        public void TogglePause()
        {
            if(State==GameState.Playing){State=GameState.Paused;Time.timeScale=0;ui.ShowPause();}
            else if(State==GameState.Paused){State=GameState.Playing;Time.timeScale=1;ui.ShowPlay();}
            audioSystem.Click();
        }
        void EndGame()
        {
            if(State!=GameState.Playing)return;
            bool record=Score>best;
            State=GameState.GameOver;Time.timeScale=1;
            if(record){best=Score;PlayerPrefs.SetInt(SaveKey,best);PlayerPrefs.Save();}
            ui.ShowEnd(Score,best,record);
        }
        public void ReturnToMenu()
        {
            // Leaving from Pause must also keep a new record (previously only Playing did).
            if((State==GameState.Playing||State==GameState.Paused) && Score>best){best=Score;PlayerPrefs.SetInt(SaveKey,best);PlayerPrefs.Save();}
            State=GameState.Menu;Time.timeScale=1f;
            ResetWorld(); // clear leftover traffic so the attract-mode road behind the menu is empty
            if(ui!=null)ui.ShowMenu(best);
        }
        public void Quit()
        {
            if(Score>best){PlayerPrefs.SetInt(SaveKey,Score);PlayerPrefs.Save();}
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#endif
        }
        void OnApplicationQuit()
        {
            if(Score>best){PlayerPrefs.SetInt(SaveKey,Score);PlayerPrefs.Save();}
            Time.timeScale=1;
        }
    }
}
