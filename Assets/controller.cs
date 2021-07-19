using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Video;

public class controller : MonoBehaviour
{
    public bool alwaysShowAll;   //0
    public float disappearHorizontal;
    public float disappearVertical;
    public bool enablePerspectiveProjection; //1
    public bool enableRotation;  //2
    public bool enableScale;     //3
    public float size;
    public float maxTilt;
    public bool enableDepth;     //4
    public float maxDepth;
    const float disX = 37;

    public GameObject mainCamera;
    public GameObject videoPlayer;
    public GameObject cameraTemplate;
    public GameObject boardTemplate;

    private class ViewPoint {
        controller father;
        float startTime;
        float endTime;
        Vector3 pos;
        bool active;
        GameObject object1;
        GameObject camera;
        GameObject board;
        public ViewPoint(controller father, float startTime, float endTime, Vector3 pos)
        {
            this.father = father;
            this.startTime = startTime;
            this.endTime = endTime;
            this.pos = pos;
            this.active = false;
        }

        private void normalize(ref float x)
        {
            while (x > 180) x -= 360;
            while (x < -180) x += 360;
        }
        
        public void update()
        {
            Vector3 rotation = object1.transform.eulerAngles - father.mainCamera.transform.eulerAngles;
            normalize(ref rotation.x);
            normalize(ref rotation.y);
            rotation.z = 0;

            //Disapper function
            if (!father.alwaysShowAll && 
                Math.Abs(rotation.x) < father.disappearHorizontal && 
                Math.Abs(rotation.y) < father.disappearVertical)
            {
                board.SetActive(false);
                return;
            }
            board.SetActive(true);

            //Rotation calculation
            Vector3 rot = Vector3.zero;
            if (father.enableRotation)
            {
                rot.z = (float)(180 / Math.PI * Math.Atan2(rotation.y, rotation.x));
            }
            camera.transform.localEulerAngles = rot;

            rotation.x /= 90;
            rotation.y /= 180;

            //Perspective projection
            if (father.enablePerspectiveProjection)
            {
                rot.x = rotation.x * father.maxTilt;
                rot.y = rotation.y * father.maxTilt;
            }
            rot.z += 180;

            board.transform.localEulerAngles = rot;

            //Pos calculation

            Vector3 pos = Vector3.zero;

            //Depth
            float depth = 0;
            if (father.enableDepth)
            {
                depth = father.maxDepth * (0.8f - rotation.magnitude);
            }
            pos.x = disX - depth;
            pos.y = pos.x * Math.Abs(rotation.x) / Math.Abs(rotation.y);

            if (pos.y > disX / 2)
            {
                pos.x *= disX / 2 / pos.y;
                pos.y = disX / 2;
            }

            if (rotation.y < 0) pos.x *= -1;
            if (rotation.x > 0) pos.y *= -1;

            //?
            pos.z = 70;// + depth;
            board.transform.localPosition = pos;

            //Size calculation
            Vector3 scale = new Vector3(father.size, father.size, 0.5f);
            if (father.enableScale)
            {
                float scaleFactor = 1.3f - rotation.magnitude;
                if (scaleFactor < 0.7f) scaleFactor = 0.7f;
                if (scaleFactor > 1) scaleFactor = 1;
                scale.x = scale.y = father.size * scaleFactor;
            }
            board.transform.localScale = scale;
        }

        public void start()
        {
            active = true;
            object1 = Instantiate(new GameObject(), father.transform);
            camera = Instantiate(father.cameraTemplate, object1.transform);
            object1.transform.localEulerAngles = pos;
            board = Instantiate(father.boardTemplate, father.mainCamera.transform);
            board.GetComponent<MeshRenderer>().material.mainTexture = 
                camera.GetComponent<Camera>().targetTexture = 
                new RenderTexture(500, 500, 24);
            update();
        }

        public void stop()
        {
            active = false;
            Destroy(camera);
            Destroy(board);
        }

        public void check(float time)
        {
            if (startTime <= time && time < endTime && !active) start();
            else if (time >= endTime && active) stop();

            if (active) update();
        }

    }

    private int viewNum = 0;
    ViewPoint[] viewPoints;

    private float timer = 0;

    private void loadViews()
    {
        viewNum = 7;
        viewPoints = new ViewPoint[viewNum];
        // 添加viewPoint
        viewPoints[0] = new ViewPoint(this, 22, 64, new Vector3(2.465f, 30.901f, 0));//rabbit1
        viewPoints[1] = new ViewPoint(this, 64, 108, new Vector3(4.917f, 69.21001f, 0));//rabbit2
        viewPoints[2] = new ViewPoint(this, 75, 78, new Vector3(-6.808f, 134.055f, 0));//???
        viewPoints[3] = new ViewPoint(this, 79, 90, new Vector3(-32.239f, -357.21f, 0));//plane1
        viewPoints[4] = new ViewPoint(this, 90, 244, new Vector3(-8.024f, 165.992f, 0));//plane2
        viewPoints[5] = new ViewPoint(this, 108, 165, new Vector3(17.486f, -361.763f, 0));//rabbit3
        viewPoints[6] = new ViewPoint(this, 149, 155, new Vector3(-23.808f, -257.602f, 0));//eagle
    }

    void Start()
    {
        loadViews();
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

        for (int i = 0; i < viewNum; i++)
            viewPoints[i].check(timer);

        timer += Time.deltaTime;
    }
}
