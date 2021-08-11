using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Video;
using System.IO;

public class controller : MonoBehaviour
{
    public bool alwaysShowAll;   //0
    public float disappearHorizontal;
    public float disappearVertical;
    public bool enablePerspectiveProjection; //1
    public bool enableRotation;  //2
    public bool enableScale;     //3
    public float boardSize;
    public float maxTilt;
    public bool enableDepth;     //4
    public float maxDepth;
    const float disX = 37;

    public GameObject mainCamera;
    public GameObject videoPlayer;
    public GameObject cameraTemplate;
    public GameObject boardTemplate;
    public GameObject arrowTemplate;

    private ViewPointController VPC = new ViewPointController();

    const int width = 1024;
    const int height = 512;
    const int framerate = 30;

    private class TimePoint
    {
        public float frame;
        public Vector3 pos;
        public float fov;
        public TimePoint(float frame, float posx, float posy, float fov)
        {
            this.frame = frame;
            float px = (posx - width / 2) / (width / 4);
            float qx = 0;
            if (px > 1) qx = (float)(Math.Atan(px - 1.5) / (Math.PI / 4)) + 1.5f;
            else if (px < -1) qx = (float)(Math.Atan(px + 1.5) / (Math.PI / 4)) - 1.5f;
            else if (px > 0) qx = (float)(Math.Atan(px - 0.5) / (Math.PI / 4)) + 0.5f;
            else if (px < -0) qx = (float)(Math.Atan(px + 0.5) / (Math.PI / 4)) - 0.5f;
            this.pos = new Vector3((posy - height / 2) / (height / 2) * 90f, qx * 90f, 0);
            this.fov = Math.Max(fov, 10);
        }
    }
    private class ViewPoint
    {
        private TimePoint[] timePoints;
        // useless now
        public int importance { get; }
        private int state;
        public GameObject cameraWrapper { get; }
        public GameObject camera { get; }
        public ViewPoint(GameObject cameraTemplate, Transform controller, TimePoint[] timePoints, int importance = 0)
        {
            this.timePoints = timePoints;
            this.importance = importance;
            state = 0;

            cameraWrapper = new GameObject("CameraWrapper");
            cameraWrapper.transform.parent = controller;
            cameraWrapper.transform.localEulerAngles = new Vector3(0, 90, 0);
            camera = Instantiate(cameraTemplate, cameraWrapper.transform);
            camera.GetComponent<Camera>().targetTexture = new RenderTexture(500, 500, 24);
        }

        // Update content of camera
        // returns false if not start yet or has ended
        public bool Check(float frame)
        {
            while (state < timePoints.Length && frame >= timePoints[state].frame) state++;
            if (state == 0) return false;
            if (state == timePoints.Length)
            {
                Destroy(camera);
                Destroy(cameraWrapper);
                return false;
            }
            float k = (frame - timePoints[state - 1].frame) / (timePoints[state].frame - timePoints[state - 1].frame);
            camera.transform.localEulerAngles =
                timePoints[state - 1].pos + k * (timePoints[state].pos - timePoints[state - 1].pos) + new Vector3(0, 0, camera.transform.localEulerAngles.z);
            camera.GetComponent<Camera>().fieldOfView = 90;
            return true;
        }

    }


    // NFoV Controller
    private class ViewPointController
    {
        private List<ViewPoint> viewPoints, activePoints;
        private GameObject[] boards = new GameObject[4];
        //private GameObject[] arrows = new GameObject[8];

        public ViewPointController()
        {
            viewPoints = new List<ViewPoint>();
            activePoints = new List<ViewPoint>();
        }

        // Create Unity objects
        public void Init(GameObject boardTemplate, GameObject arrowTemplate, Transform mainCamera)
        {
            for (int i = 0; i < 4; i++)
            {
                boards[i] = Instantiate(boardTemplate, mainCamera);
                //arrows[i] = Instantiate(arrowTemplate, boards[i].transform);
            }
        }

        // Load NFoV data from file
        // Format:
        // Num of cameras
        // Num of time points of first camera
        // Time1 Posx Posy Fov
        // Time2 ......
        // Num of time points of second camera
        // ......
        public void Load(String filename, GameObject cameraTemplate, Transform controller)
        {
            using (TextReader reader = File.OpenText(filename))
            {
                int n = int.Parse(reader.ReadLine());
                for (int i = 0; i < n; i++)
                {
                    int numTimePoints = int.Parse(reader.ReadLine());
                    TimePoint[] timePoints = new TimePoint[numTimePoints];
                    for (int j = 0; j < numTimePoints; j++)
                    {
                        string[] info = reader.ReadLine().Split(' ');
                        timePoints[j] = new TimePoint(int.Parse(info[0]), int.Parse(info[1]), int.Parse(info[2]), float.Parse(info[3]));
                    }
                    viewPoints.Add(new ViewPoint(cameraTemplate, controller, timePoints));
                }
            }
        }

        // Make sure angle x is in [-180, 180]
        private float Normalize(float x)
        {
            while (x > 180) x -= 360;
            while (x < -180) x += 360;
            return x;
        }


        // Set transform and rendering of PIP board
        private void RendBoard(GameObject board, GameObject camera, Vector3 dir, controller father)
        {
            dir.x = Normalize(dir.x);
            dir.y = Normalize(dir.y);
            dir.z = 0;

            board.GetComponentInChildren<MeshRenderer>().material.mainTexture = camera.GetComponent<Camera>().GetComponent<Camera>().targetTexture;

            //Disapper function
            if (!father.alwaysShowAll && 
                Math.Abs(dir.x) < father.disappearHorizontal && 
                Math.Abs(dir.y) < father.disappearVertical)
            {
                return;
            }
            board.SetActive(true);

            //Rotation calculation
            Vector3 rot = camera.transform.localEulerAngles;
            if (father.enableRotation)
            {
                rot.z = (float)(180 / Math.PI * Math.Atan2(dir.y, dir.x));
            }
            camera.transform.localEulerAngles = rot;

            dir.x /= 90;
            dir.y /= 180;

            //Perspective projection
            if (father.enablePerspectiveProjection)
            {
                rot.x = dir.x * father.maxTilt;
                rot.y = dir.y * father.maxTilt;
            }
            rot.z += 180;

            board.transform.localEulerAngles = rot;

            //Pos calculation

            Vector3 pos = Vector3.zero;

            //Depth
            float depth = 0;
            if (father.enableDepth)
            {
                depth = father.maxDepth * (0.8f - dir.magnitude);
            }
            pos.x = disX - depth;
            pos.y = pos.x * Math.Abs(dir.x) / Math.Abs(dir.y);

            if (pos.y > disX / 2)
            {
                pos.x *= disX / 2 / pos.y;
                pos.y = disX / 2;
            }

            if (dir.y < 0) pos.x *= -1;
            if (dir.x > 0) pos.y *= -1;

            //?
            pos.z = 70;// + depth;
            board.transform.localPosition = pos;

            //Size calculation
            Vector3 scale = new Vector3(father.boardSize, father.boardSize, 0.5f);
            if (father.enableScale)
            {
                float scaleFactor = 1.3f - dir.magnitude;
                if (scaleFactor < 0.7f) scaleFactor = 0.7f;
                if (scaleFactor > 1) scaleFactor = 1;
                scale.x = scale.y = father.boardSize * scaleFactor;
            }
            board.transform.localScale = scale;
        }

        public void Update(float frame, GameObject mainCamera, controller father)
        {
            // Remove or add cameras
            for (int i = 0; i < activePoints.Count; i++)
            {
                if (!activePoints[i].Check(frame))
                    activePoints.RemoveAt(i--);
            }
            for (int i = 0; i < viewPoints.Count; i++)
            {
                if (viewPoints[i].Check(frame))
                {
                    activePoints.Add(viewPoints[i]);
                    viewPoints.RemoveAt(i--);
                }
            }

            // sort by position
            //activePoints.Sort((x, y) => Normalize((x.camera.transform.eulerAngles - mainCamera.transform.eulerAngles).y).CompareTo(Normalize((y.camera.transform.eulerAngles - mainCamera.transform.eulerAngles).y)));

            int boardNum = Math.Min(activePoints.Count, 4);
            for (int i = 0; i < 4; i++) boards[i].SetActive(false);
            for (int i = 0; i < boardNum; i++)
            {
                Vector3 dir = activePoints[i].camera.transform.eulerAngles - mainCamera.transform.eulerAngles;
                //dir = new Vector3(Normalize(dir.y) / 180, -Normalize(dir.x) / 180, 0);

                RendBoard(boards[i], activePoints[i].camera, dir, father);
            }
        }
    }

    private void LoadViews()
    {
        VPC.Init(boardTemplate, arrowTemplate, mainCamera.transform);
        VPC.Load("data/data.txt", cameraTemplate, transform);
    }

    void Start()
    {
        LoadViews();
    }

    private const float speed = 0.25f;
    void Update()
    {
        Vector3 rotation = mainCamera.transform.eulerAngles;
        if (Input.GetKey(KeyCode.W))
        {
            rotation.x -= speed;
        }
        if (Input.GetKey(KeyCode.S))
        {
            rotation.x += speed;
        }
        if (Input.GetKey(KeyCode.A))
        {
            rotation.y -= speed;
        }
        if (Input.GetKey(KeyCode.D))
        {
            rotation.y += speed;
        }
        mainCamera.transform.eulerAngles = rotation;

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            alwaysShowAll = !alwaysShowAll;
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            enablePerspectiveProjection = !enablePerspectiveProjection;
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            enableRotation = !enableRotation;
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            enableDepth = !enableDepth;
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            enableScale = !enableScale;
        }
        
        VPC.Update(videoPlayer.GetComponent<VideoPlayer>().frame, mainCamera, this);
    }
}
