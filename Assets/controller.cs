using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Video;
using System.IO;

public class controller : MonoBehaviour
{
    public float boardSize;
    public float boardDis;

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
        public float time;
        public Vector3 pos;
        public float fov;
        public TimePoint(float frame, float posx, float posy, float fov)
        {
            this.time = frame / framerate;
            this.pos = new Vector3((posy - height / 2) / (height / 2) * 90f, (posx - 8 - width / 2) / (width / 2) * 180f, 0);
            this.fov = Math.Max(fov, 15);
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
        public bool Check(float time)
        {
            while (state < timePoints.Length && time >= timePoints[state].time) state++;
            if (state == 0) return false;
            if (state == timePoints.Length)
            {
                Destroy(camera);
                Destroy(cameraWrapper);
                return false;
            }
            float k = (time - timePoints[state - 1].time) / (timePoints[state].time - timePoints[state - 1].time);
            camera.transform.localEulerAngles =
                timePoints[state - 1].pos + k * (timePoints[state].pos - timePoints[state - 1].pos);
            camera.GetComponent<Camera>().fieldOfView =
                timePoints[state - 1].fov + k * (timePoints[state].fov - timePoints[state - 1].fov);
            return true;
        }

    }


    // NFoV Controller
    private class ViewPointController
    {
        private List<ViewPoint> viewPoints, activePoints;
        private GameObject[] boards = new GameObject[8];
        private GameObject[] arrows = new GameObject[8];

        public ViewPointController()
        {
            viewPoints = new List<ViewPoint>();
            activePoints = new List<ViewPoint>();
        }

        // Create Unity objects
        public void Init(GameObject boardTemplate, GameObject arrowTemplate, Transform mainCamera)
        {
            for (int i = 0; i < 8; i++)
            {
                boards[i] = Instantiate(boardTemplate, mainCamera);
                arrows[i] = Instantiate(arrowTemplate, boards[i].transform);
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
        private void RendBoard(GameObject board, GameObject camera, Vector3 dir, float fov, float boardSize, float boardDis)
        {
            board.GetComponentInChildren<MeshRenderer>().material.mainTexture = camera.GetComponent<Camera>().GetComponent<Camera>().targetTexture;

            // some confusing calculation
            // X is useless if equals to 0
            float X = 0f, Y = boardDis;
            float K0 = fov / 2;
            float K = K0 - (float)(Math.Asin(X * Math.Sin(K0 / 180 * Math.PI) / (X + Y)) / Math.PI * 180);

            float tx = (float)((X + Y) * Math.Sin(dir.x * K / 180 * Math.PI));
            float ty = (float)(Math.Abs((X + Y) * Math.Cos(dir.x * K / 180 * Math.PI)) - X);
            board.transform.localPosition = new Vector3(tx, -0.275f, ty);
            board.transform.localEulerAngles = new Vector3(42.5f, dir.x * K, 0);
            board.transform.localScale = new Vector3(boardSize, boardSize, 0.01f);

            board.SetActive(true);
        }


        private void RendArrow(GameObject arrow, Vector3 dir)
        {
            float angle = (float)(Math.Atan2(dir.x, dir.y) * 180 / Math.PI);
            float dis = 0.33f + dir.magnitude * 0.67f;
            arrow.transform.localPosition = new Vector3(dis * (float)Math.Sin(Math.Atan2(dir.x, dir.y)), dis * (float)Math.Cos(Math.Atan2(dir.x, dir.y)), 0);
            arrow.transform.localEulerAngles = new Vector3(0, 0, -angle);
            arrow.transform.localScale = new Vector3(0.2f, 0.2f, 1);
        }

        public void Update(float timer, GameObject mainCamera, float boardSize, float boardDis)
        {
            // Remove or add cameras
            for (int i = 0; i < activePoints.Count; i++)
            {
                if (!activePoints[i].Check(timer))
                    activePoints.RemoveAt(i--);
            }
            for (int i = 0; i < viewPoints.Count; i++)
            {
                if (viewPoints[i].Check(timer))
                {
                    activePoints.Add(viewPoints[i]);
                    viewPoints.RemoveAt(i--);
                }
            }

            // sort by position
            activePoints.Sort((x, y) => Normalize((x.cameraWrapper.transform.eulerAngles - mainCamera.transform.eulerAngles).y).CompareTo(Normalize((y.cameraWrapper.transform.eulerAngles - mainCamera.transform.eulerAngles).y)));

            int boardNum = Math.Min(activePoints.Count, 4);
            for (int i = 0; i < 8; i++) boards[i].SetActive(false);
            for (int i = 0; i < boardNum; i++)
            {
                Vector3 dir = activePoints[i].cameraWrapper.transform.eulerAngles - mainCamera.transform.eulerAngles;
                dir = new Vector3(Normalize(dir.y) / 180, -Normalize(dir.x) / 180, 0);

                RendArrow(arrows[i], dir);
                RendArrow(arrows[i + boardNum], dir);
                RendBoard(boards[i], activePoints[i].camera, dir, mainCamera.GetComponent<Camera>().fieldOfView, boardSize, boardDis);
                if (dir.x < 0) dir.x += 2;
                else dir.x -= 2;
                RendBoard(boards[i + boardNum], activePoints[i].camera, dir, mainCamera.GetComponent<Camera>().fieldOfView, boardSize, boardDis);
            }
        }
    }

    private float timer = 0;


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
        VPC.Update(timer, mainCamera, boardSize, boardDis);
        if (videoPlayer.GetComponent<VideoPlayer>().isPlaying)
            timer += Time.deltaTime;
    }
}
