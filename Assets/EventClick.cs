using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Valve.VR.Extras;

public class EventClick : MonoBehaviour
{
    public SteamVR_LaserPointer laserPointer_l, laserPointer_r;
    // public SteamVR_LaserPointer laserPointer;
    private GameObject controller;
    void Awake() {
        laserPointer_l.PointerClick += PointerClick;
        laserPointer_r.PointerClick += PointerClick;
        // laserPointer.PointerClick += PointerClick;
    }
    void Start() {
        controller = GameObject.Find("Controller");
    }
    // Start is called before the first frame update
    public void PointerClick(object sender, PointerEventArgs e) {
        controller.GetComponent<controller>().MoveTo(e.target);
    }
}
