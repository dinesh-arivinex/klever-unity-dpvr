using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GamificationControllerHandSwitcher : MonoBehaviour
{
    public static GamificationControllerHandSwitcher Instance { get; private set; }
    [SerializeField]
    public bool isHandMode = false;
    public GameObject rightHandMesh;
    public UnityEvent controllerEvent, handEvent;

    public GameObject handPoseBall, handPoseBallHandMode;

    public GameObject leftHandRaycast, rightHandRaycast;
    public GameObject leftControllerRaycast, rightControllerRaycast;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public void SwitchControllerandhand(bool isHandMode)
    {
        if(isHandMode)
        {
            handEvent.Invoke();
        }
        else
        {
            controllerEvent.Invoke();
        }
    }

    //private void OnEnable()
    //{
    //    GamificationControllerHandSwitcher.Instance.isHandMode = false;
    //    SwitchControllerandhand(GamificationControllerHandSwitcher.Instance.isHandMode);
    //}

    //private void OnDisable()
    //{
    //    GamificationControllerHandSwitcher.Instance.isHandMode = true;
    //    SwitchControllerandhand(GamificationControllerHandSwitcher.Instance.isHandMode);
    //}
}
