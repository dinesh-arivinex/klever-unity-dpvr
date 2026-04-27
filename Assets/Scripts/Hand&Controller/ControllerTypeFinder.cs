using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllerTypeFinder : MonoBehaviour
{
    public bool isHand;
    public bool isGamificationExist; 
    public bool isQuest3;
    public GamificationManager gamificationManager;
    private void OnEnable()
    {
        if (!isHand)
        {
            GamificationControllerHandSwitcher.Instance.isHandMode = false;
            GamificationControllerHandSwitcher.Instance.SwitchControllerandhand(GamificationControllerHandSwitcher.Instance.isHandMode);
            if (isGamificationExist)
            {
                ControllerSwitchQuest.Instance.isQuest3 = isQuest3;
                ControllerSwitchQuest.Instance.AssignGamification();
                Transform getChildObj = this.gameObject.transform.GetChild(0);
                gamificationManager.rightcontroller2 = getChildObj.gameObject;
            }
            else
            {
                Transform getChildObj = this.gameObject.transform.GetChild(0);
                gamificationManager.rightcontroller1 = getChildObj.gameObject;
            }
        }
        else
        {
            GamificationControllerHandSwitcher.Instance.isHandMode = true;
            GamificationControllerHandSwitcher.Instance.SwitchControllerandhand(GamificationControllerHandSwitcher.Instance.isHandMode);
        }
     
    }

    private void OnDisable()
    {
        if (!isHand)
        {
            GamificationControllerHandSwitcher.Instance.isHandMode = true;
            GamificationControllerHandSwitcher.Instance.SwitchControllerandhand(GamificationControllerHandSwitcher.Instance.isHandMode);
        }
        else
        {
            GamificationControllerHandSwitcher.Instance.isHandMode = false;
            GamificationControllerHandSwitcher.Instance.SwitchControllerandhand(GamificationControllerHandSwitcher.Instance.isHandMode);
        }
    }
}
