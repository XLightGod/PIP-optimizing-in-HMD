using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Video;
using System.IO;

public class controller : MonoBehaviour
{
    public float boardSize;
    public float boardSpace;
    public float boardDis;

    public GameObject mainCamera;
    public GameObject videoPlayer;
    public GameObject cameraTemplate;
    public GameObject boardTemplate;
    public GameObject lineTemplate;

    private ViewPointController VPC = new ViewPointController();

    private class TimePoint
    {
        public float time;
        public Vector3 pos;
        public float fov;
        public TimePoint(float time, Vector3 pos, float fov)
        {
            this.time = time;
            this.pos = pos;
            this.fov = fov;
        }
    }
    private class ViewPoint
    {
        private TimePoint[] timePoints;
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
            camera = Instantiate(cameraTemplate, cameraWrapper.transform);
            camera.GetComponent<Camera>().targetTexture = new RenderTexture(500, 500, 24);
        }

        public bool Check(float time)
        {
            while (state < timePoints.Length && time >= timePoints[state].time)
            {
                cameraWrapper.transform.localEulerAngles = timePoints[state].pos;
                camera.transform.eulerAngles = cameraWrapper.transform.eulerAngles;
                camera.GetComponent<Camera>().fieldOfView = timePoints[state].fov;
                state++;
            }
            // 考虑插值？

            if (state == 0) return false;
            if (state == timePoints.Length)
            {
                Destroy(camera);
                Destroy(cameraWrapper);
                return false;
            }

            return true;
        }

    }

    private class ViewPointController
    {
        private List<ViewPoint> viewPoints, activePoints;
        private GameObject[] boards = new GameObject[4];
        private GameObject[] lines = new GameObject[4];

        public ViewPointController()
        {
            viewPoints = new List<ViewPoint>();
            activePoints = new List<ViewPoint>();
        }

        public void Init(GameObject boardTemplate, GameObject lineTemplate, Transform mainCamera)
        {
            for (int i = 0; i < 4; i++)
            {
                boards[i] = Instantiate(boardTemplate, mainCamera);
                lines[i] = Instantiate(lineTemplate, boards[i].transform);
            }
        }

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
                        timePoints[j] = new TimePoint(float.Parse(info[0]), new Vector3(float.Parse(info[1]), float.Parse(info[2]), 0), float.Parse(info[3]));
                    }
                    viewPoints.Add(new ViewPoint(cameraTemplate, controller, timePoints));
                }
            }
        }
        private void Normalize(ref float x)
        {
            while (x > 180) x -= 360;
            while (x < -180) x += 360;
        }
        private Vector2 Pos(float x)
        {
            float t = (float)Math.Tan(x * Math.PI / 180);
            if (x >= -135 && x < -45) return new Vector2(-1 / t, -1);
            if (x >= -45 && x < 45) return new Vector2(1, t);
            if (x >= 45 && x < 135) return new Vector2(1 / t, 1);
            return new Vector2(-1, -t);
        }
        public void Update(float timer, GameObject mainCamera, float boardSize, float boardSpace, float boardDis)
        {
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

            activePoints.Sort((x, y) => Normalize((x.cameraWrapper.transform.eulerAngles - mainCamera.transform.eulerAngles).y).CompareTo(Normalize((y.cameraWrapper.transform.eulerAngles - mainCamera.transform.eulerAngles).y)));

            int boardNum = Math.Min(activePoints.Count, 4);
            for (int i = 0; i < 4; i++) boards[i].SetActive(false);
            for (int i = 0; i < boardNum; i++)
            {
                boards[i].GetComponent<MeshRenderer>().material.mainTexture = activePoints[i].camera.GetComponent<Camera>().targetTexture;
                Vector3 dir = activePoints[i].cameraWrapper.transform.eulerAngles - mainCamera.transform.eulerAngles;
                Normalize(ref dir.x);
                Normalize(ref dir.y);
                dir.z = 0;
                dir.x /= 90;
                dir.x = -dir.x;
                dir.y /= 180;
                float arc = 85 - Math.Min(dir.magnitude, 1) * 75;
                float d1 = (float)(Math.Atan2(dir.x, dir.y) * 180 / Math.PI - arc / 2);
                float d2 = (float)(Math.Atan2(dir.x, dir.y) * 180 / Math.PI + arc / 2);
                Vector2 p1 = Pos(d1) / 2;
                Vector2 p2 = Pos(d2) / 2;
                boards[i].transform.localScale = new Vector3(boardSize, boardSize, 0.01f);
                boards[i].transform.localPosition =
                    new Vector3((i - ((boardNum - 1) / 2.0f)) * (boardSize + boardSpace), -0.25f, boardDis);
                lines[i].transform.localPosition = -(p1 + p2) / 2;
                lines[i].transform.localScale = new Vector3((float)Math.Abs(p1.x - p2.x) + 0.1f, (float)Math.Abs(p1.y - p2.y) + 0.1f, 0.995f);

                boards[i].SetActive(true);
            }
        }
    }


    private int viewNum = 0;
    ViewPoint[] viewPoints;

    private float timer = 0;

    private void LoadViews()
    {
        VPC.Init(boardTemplate, lineTemplate, mainCamera.transform);
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

        VPC.Update(timer, mainCamera, boardSize, boardSpace, boardDis);

        timer += Time.deltaTime;
    }
}
