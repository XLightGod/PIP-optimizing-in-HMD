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
        public float frame;
        public Vector3 pos;
        public float fov;
        public TimePoint(float frame, float posx, float posy, float fov)
        {
            this.frame = frame;
            float px = (posx - width / 2) / (width / 4);
            float qx = 0;
            if (px > 1) qx = (float)(Math.Atan(2 * (px - 1.5)) / (Math.PI / 2)) + 1.5f;
            else if (px < -1) qx = (float)(Math.Atan(2 * (px + 1.5)) / (Math.PI / 2)) - 1.5f;
            else if (px > 0) qx = (float)(Math.Atan(2 * (px - 0.5)) / (Math.PI / 2)) + 0.5f;
            else qx = (float)(Math.Atan(2 * (px + 0.5)) / (Math.PI / 2)) - 0.5f;

            float py = (posy - height / 2) / (height / 2);
            float qy = 0;
            if (py > 0) qy = (float)(Math.Atan(2 * (py - 0.5)) / (Math.PI / 2)) + 0.5f;
            else qy = (float)(Math.Atan(2 * (py + 0.5)) / (Math.PI / 2)) - 0.5f;

            this.pos = new Vector3(qy * 90f, qx * 90f, 0);
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
        private GameObject[] boards = new GameObject[4];
        //private GameObject[] arrows = new GameObject[4];

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


        private Vector2 Pos(float x)
        {
            float t = (float)Math.Tan(x * Math.PI / 180);
            if (x >= -135 && x < -45) return new Vector2(-1 / t, -1);
            if (x >= -45 && x < 45) return new Vector2(1, t);
            if (x >= 45 && x < 135) return new Vector2(1 / t, 1);
            return new Vector2(-1, -t);
        }

        public void Update(float frame, GameObject mainCamera, float boardSize, float boardDis)
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
            activePoints.Sort((x, y) => Normalize((x.camera.transform.eulerAngles - mainCamera.transform.eulerAngles).y).CompareTo(Normalize((y.camera.transform.eulerAngles - mainCamera.transform.eulerAngles).y)));

            int boardNum = Math.Min(activePoints.Count, 4);
            for (int i = 0; i < 4; i++) boards[i].SetActive(false);
            for (int i = 0; i < boardNum; i++)
            {
                boards[i].GetComponentInChildren<MeshRenderer>().material.mainTexture = activePoints[i].camera.GetComponent<Camera>().GetComponent<Camera>().targetTexture;
                Vector3 dir = activePoints[i].camera.transform.eulerAngles - mainCamera.transform.eulerAngles;
                dir = new Vector3(Normalize(dir.y) / 180, -Normalize(dir.x) / 180, 0);

                float arc = 85 - Math.Min(dir.magnitude, 1) * 75;
                float d1 = (float)(Math.Atan2(dir.y, dir.x) * 180 / Math.PI - arc / 2);
                float d2 = (float)(Math.Atan2(dir.y, dir.x) * 180 / Math.PI + arc / 2);
                Vector2 p1 = Pos(d1) / 2;
                Vector2 p2 = Pos(d2) / 2;
                boards[i].transform.localScale = new Vector3(boardSize, boardSize, 0.01f);
                boards[i].transform.localPosition =
                    new Vector3((i - ((boardNum - 1) / 2.0f)) * (boardSize + 0.6f), -0.25f, boardDis);
                //arrows[i].transform.localPosition = -(p1 + p2) / 2;
                //arrows[i].transform.localScale = new Vector3((float)Math.Abs(p1.x - p2.x) + 0.1f, (float)Math.Abs(p1.y - p2.y) + 0.1f, 0.995f);

                boards[i].SetActive(true);
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
        VPC.Update(videoPlayer.GetComponent<VideoPlayer>().frame, mainCamera, boardSize, boardDis);
    }
}
