using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class EventClick : MonoBehaviour
{
    private GameObject controller;
    void Start()
    {
        controller = GameObject.Find("Controller");
    }
    public void OnCollisionEnter(Collision collision)
    {
        controller.GetComponent<controller>().MoveTo(transform.parent);
    }
}
