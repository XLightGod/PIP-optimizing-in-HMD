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

    // Make sure angle x is in [-180, 180]
    static private Vector3 Normalize(Vector3 v)
    {
        while (v.x > 180) v.x -= 360;
        while (v.x < -180) v.x += 360;
        while (v.y > 180) v.y -= 360;
        while (v.y < -180) v.y += 360;
        v.z = 0;
        return v;
    }
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
        public Vector3 dir { get; set; }
        public Vector3 last_dir { get; set; }
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
            cameraWrapper.transform.localEulerAngles = new Vector3(0, -90, 0);
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
        private GameObject[] arrows = new GameObject[4];
        private int boardNum = 0;

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

        // Set transform and rendering of PIP board
        private void RendBoard(GameObject board, GameObject camera, Vector3 dir, float fov, float boardSize, float boardDis)
        {
            board.GetComponentInChildren<MeshRenderer>().material.mainTexture = camera.GetComponent<Camera>().GetComponent<Camera>().targetTexture;

            // some confusing calculation
            // X is useless if equals to 0
            float X = 0f, Y = boardDis;
            //float K0 = fov / 2;
            float K0 = 50 / 2;
            float K = K0 - (float)(Math.Asin(X * Math.Sin(K0 / 180 * Math.PI) / (X + Y)) / Math.PI * 180);

            float tx = (float)((X + Y) * Math.Sin(dir.x * K / 180 * Math.PI));
            float ty = (float)(Math.Abs((X + Y) * Math.Cos(dir.x * K / 180 * Math.PI)) - X);
            board.transform.localPosition = new Vector3(tx, -0.16f, ty);
            board.transform.localEulerAngles = new Vector3(42.5f, dir.x * K0, 0);
            board.transform.localScale = new Vector3(boardSize, boardSize, 0.01f);

            board.SetActive(true);
        }


        private void RendArrow(GameObject arrow, Vector3 dir)
        {
            float angle = (float)(Math.Atan2(dir.x, dir.y) * 180 / Math.PI);
            arrow.transform.localScale = new Vector3(0.3f, 0.1f + 0.25f * dir.magnitude, 1);
            float dis = 0.5f + arrow.transform.localScale.y / 2;
            arrow.transform.localPosition = new Vector3(dis * (float)Math.Sin(Math.Atan2(dir.x, dir.y)), dis * (float)Math.Cos(Math.Atan2(dir.x, dir.y)), 0);
            arrow.transform.localEulerAngles = new Vector3(0, 0, -angle);
        }

        public void Update(float frame, GameObject mainCamera, float boardSize, float boardDis)
        {
            float Vfov = mainCamera.GetComponent<Camera>().fieldOfView;
            float Hfov = Camera.VerticalToHorizontalFieldOfView(Vfov, mainCamera.GetComponent<Camera>().aspect);

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
            for (int i = 0; i < activePoints.Count; i++)
            {
                Vector3 _dir = Normalize(activePoints[i].camera.transform.eulerAngles - mainCamera.transform.eulerAngles);
                if (_dir.y * activePoints[i].dir.y < 0 && Math.Abs(_dir.y) > 150)
                {
                    viewPoints.Add(activePoints[i]);
                    activePoints.RemoveAt(i--);
                    continue;
                }
                activePoints[i].last_dir = activePoints[i].dir;
                activePoints[i].dir = _dir;
            }
            activePoints.Sort((x, y) => Math.Abs(x.dir.y).CompareTo(Math.Abs(y.dir.y)));

            if (activePoints.Count > 0) // adaptive scaling
            {
                float dis = activePoints[0].dir.magnitude - activePoints[0].camera.GetComponent<Camera>().fieldOfView / 2;
                for (int i = 1; i < activePoints.Count; i++)
                {
                    dis = Math.Min(dis, activePoints[i].dir.magnitude - activePoints[i].camera.GetComponent<Camera>().fieldOfView / 2);
                }
                float minDis = 30;
                float maxDis = Vfov / 2;
                dis = Math.Min(maxDis, Math.Max(minDis, dis));
                boardSize += ((dis - minDis) / (maxDis - minDis)) * boardSize * 0.25f;
            }


            for (int i = 0; i < activePoints.Count; i++)
            {
                Vector3 dir = activePoints[i].dir;
                if ((Math.Abs(dir.x) < Vfov / 2 && Math.Abs(dir.y) < Hfov / 2))
                {
                    viewPoints.Add(activePoints[i]);
                    activePoints.RemoveAt(i--);
                }
            }
            boardNum = Math.Min(activePoints.Count, 4);
            for (int i = boardNum; i < activePoints.Count; i++)
            {
                viewPoints.Add(activePoints[i]);
                activePoints.RemoveAt(i--);
            }

            activePoints.Sort((x, y) => x.dir.y.CompareTo(y.dir.y));

            const float threshold = 90;//????????????????????????
            if (boardNum >= 2)
            {
                for (int i = boardNum - 1; i >= 0; i--)
                {
                    int right = i, len = 1;
                    for(;;)
                    {
                        for(;right + 1 < boardNum; right = right + 1)
                        {
                            float dist = activePoints[right + 1].dir.y - activePoints[right].dir.y;
                            if (dist >= ((float)len + 1) / 2 * threshold) break;
                        }
                        int newLen = (right - i + boardNum) % boardNum + 1;
                        if (len == newLen) break;
                        len = newLen;
                    }
                    float pos1 = Normalize(activePoints[i + (len - 1) / 2].camera.transform.eulerAngles - mainCamera.transform.eulerAngles).y;
                    float pos2 = Normalize(activePoints[i + len / 2].camera.transform.eulerAngles - mainCamera.transform.eulerAngles).y;
                    float center = (pos1 + pos2) / 2;
                    if (center + (((float)len - 1) / 2) * threshold > 180) {
                        center -= center + (((float)len - 1) / 2) * threshold - 180;
                    }
                    for (int j = 0; j < len; j++)
                    {
                        Vector3 dir = activePoints[i + j].dir;
                        dir.y = (center + (j - ((float)len - 1) / 2) * threshold);
                        activePoints[i + j].dir = dir;
                    }
                }
            }

            for (int i = 0; i < 4; i++) boards[i].SetActive(false);
            for (int i = 0; i < boardNum; i++)
            {
                Vector3 dir = activePoints[i].dir;
                dir = new Vector3(dir.y / 180, -dir.x / 180, 0);

                //RendArrow(arrows[i + boardNum], dir);
                RendBoard(boards[i], activePoints[i].camera, dir, mainCamera.GetComponent<Camera>().fieldOfView, boardSize, boardDis);
                dir.x -= (float)(Math.Atan2(boards[i].transform.localPosition.x, boards[i].transform.localPosition.z) / Math.PI);
                dir.y -= (float)(Math.Atan2(boards[i].transform.localPosition.y, boards[i].transform.localPosition.z) / Math.PI);
                RendArrow(arrows[i], dir);
                //if (dir.x < 0) dir.x += 2;
                //else dir.x -= 2;
                //RendBoard(boards[i + boardNum], activePoints[i].camera, dir, mainCamera.GetComponent<Camera>().fieldOfView, boardSize, boardDis);
            }
        }
        public GameObject GetTargetCamera(Transform board)
        {
            for (int i = 0; i < boardNum; i++)
            {
                if (boards[i].transform == board)
                {
                    return activePoints[i].camera;
                }
            }
            // error
            print("error");
            return new GameObject();
        }
    }

    private void LoadViews()
    {
        VPC.Init(boardTemplate, arrowTemplate, mainCamera.transform);
        VPC.Load("data/" + videoPlayer.GetComponent<VideoPlayer>().clip.name.Split('.')[0] + ".txt", cameraTemplate, transform);
    }

    void Start()
    {
        LoadViews();
    }

    private const float moveSpeed = 2;
    private bool moving = false;
    private GameObject targetCamera;

    
    // using current timestamp as log file path
    String logFilePath = DateTime.Now.ToString("yyyyddMM-HHmmss") + ".txt";


    public void MoveTo(Transform board)
    {
        if (!moving)
        {
            targetCamera = VPC.GetTargetCamera(board);
            moving = true;
        }
    }
    void Update()
    {
        if (moving)
        {
            Vector3 vec = Normalize(new Vector3(0, (targetCamera.transform.eulerAngles - mainCamera.transform.eulerAngles).y, 0));
            if (vec.magnitude <= moveSpeed)
            {
                mainCamera.transform.parent.localEulerAngles += vec.magnitude * Vector3.Normalize(vec);
                moving = false;
            }
            else
            {
                mainCamera.transform.parent.localEulerAngles += moveSpeed * Vector3.Normalize(vec);
            }
        }
        VPC.Update(videoPlayer.GetComponent<VideoPlayer>().frame, mainCamera, boardSize, boardDis);
        
        if (videoPlayer.GetComponent<VideoPlayer>().isPlaying) {
            using (StreamWriter writer = File.AppendText(logFilePath))
            {
                writer.WriteLine(videoPlayer.GetComponent<VideoPlayer>().frame / 30.0f + " " + mainCamera.transform.eulerAngles.x + " " + mainCamera.transform.eulerAngles.y + " " + mainCamera.transform.eulerAngles.z);
            }
        }
    }
}
