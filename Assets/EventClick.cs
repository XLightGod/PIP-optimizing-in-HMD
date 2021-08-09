﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class EventClick : MonoBehaviour, IPointerClickHandler
{
    private GameObject controller;
    void Start()
    {
        controller = GameObject.Find("Controller");
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        print("Board On Click");
        controller.GetComponent<controller>().MoveTo(transform);
    }
}
