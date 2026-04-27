using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GamificationControllerHandSwitcherLeft : MonoBehaviour
{
    public UnityEvent controllerEvent, handEvent;


   
    private void OnEnable()
    {
        controllerEvent.Invoke();
    }

    private void OnDisable()
    {
        handEvent.Invoke();
    }
}
