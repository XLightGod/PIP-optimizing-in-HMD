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
    public float disX;
    public float disY;
    public float disZ;

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

        // Set transform and rendering of PIP board
        private void RendBoard(GameObject board, GameObject camera, Vector3 dir, controller father)
        {
            board.GetComponentInChildren<MeshRenderer>().material.mainTexture = camera.GetComponent<Camera>().GetComponent<Camera>().targetTexture;

            //Disapper function
            if (!father.alwaysShowAll &&
                Math.Abs(dir.x) < father.disappearVertical &&
                Math.Abs(dir.y) < father.disappearHorizontal)
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
            else
            {
                rot.z = 0;
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
            else
            {
                rot.x = 0;
                rot.y = 0;
            }
            rot.z += 180;

            board.transform.localEulerAngles = rot;

            //Pos calculation

            Vector3 pos = Vector3.zero;

            pos.x = father.disX;
            pos.y = pos.x * Math.Abs(dir.x) / Math.Abs(dir.y);

            if (pos.y > father.disY)
            {
                pos.x *= father.disY / pos.y;
                pos.y = father.disY;
            }

            if (dir.y < 0) pos.x *= -1;
            if (dir.x > 0) pos.y *= -1;

            //Depth
            float depth = 0;
            if (father.enableDepth)
            {
                depth = father.maxDepth * (1.414f - dir.magnitude);
            }
            pos.z = father.disZ + depth;
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

            boardNum = Math.Min(activePoints.Count, 4);
            for (int i = 0; i < 4; i++) boards[i].SetActive(false);
            for (int i = 0; i < boardNum; i++)
            {
                Vector3 dir = Normalize(activePoints[i].camera.transform.eulerAngles - mainCamera.transform.eulerAngles);
                //dir = new Vector3(Normalize(dir.y) / 180, -Normalize(dir.x) / 180, 0);

                RendBoard(boards[i], activePoints[i].camera, dir, father);
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

    private const float dragSpeed = 1.5f;
    private const float moveSpeed = 1;
    private bool dragging = false;
    private bool moving = false;
    private GameObject targetCamera;

    public void MoveTo(Transform board)
    {
        targetCamera = VPC.GetTargetCamera(board);
        moving = true;
    }
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            moving = false;
            if (!dragging) dragging = true;
            else
            {
                mainCamera.transform.localEulerAngles += new Vector3(Input.GetAxis("Mouse Y") * dragSpeed, Input.GetAxis("Mouse X") * -dragSpeed, 0);
            }
        }
        else
        {
            dragging = false;
        }

        if (moving)
        {
            Vector3 vec = Normalize(targetCamera.transform.localEulerAngles - mainCamera.transform.localEulerAngles);
            if (vec.magnitude <= moveSpeed)
            {
                mainCamera.transform.localEulerAngles = targetCamera.transform.localEulerAngles;
                moving = false;
            }
            else
            {
                mainCamera.transform.localEulerAngles += moveSpeed * Vector3.Normalize(vec);
            }
        }

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
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (videoPlayer.GetComponent<VideoPlayer>().isPlaying)
            {
                videoPlayer.GetComponent<VideoPlayer>().Pause();
            }
            else
            {
                videoPlayer.GetComponent<VideoPlayer>().Play();
            }
        }

        VPC.Update(videoPlayer.GetComponent<VideoPlayer>().frame, mainCamera, this);
    }
}
