using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

public class ColliderTriggerFill : MonoBehaviour
{
    public GameObject Answer1,Answer2, Answer3, Answer4,Answer5;
    public GameObject PositionGetter;
    public TextMeshProUGUI textfromRes;
    private void OnTriggerEnter(Collider other)
    {

        if (other.gameObject == Answer1)
        {
            GameObject grandParent1 = Answer1.transform.parent.parent.gameObject;
            TextMeshProUGUI FirstChildText1 = Answer1.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>();
            if (FirstChildText1 == textfromRes)
            {
                Debug.Log("Correct");
            }
            else 
            {
                Debug.Log("Wrong");
            }
            grandParent1.transform.localPosition = PositionGetter.transform.localPosition;

        }
        if (other.gameObject == Answer2)
        {
            GameObject grandParent2 = Answer1.transform.parent.parent.gameObject;
            grandParent2.transform.localPosition = PositionGetter.transform.localPosition;

        }
        if (other.gameObject == Answer3)
        {

            Answer3.transform.localPosition = PositionGetter.transform.localPosition;

        }
        if (other.gameObject == Answer4)
        {

            Answer4.transform.localPosition = PositionGetter.transform.localPosition;

        }
        if (other.gameObject == Answer5)
        {

            Answer5.transform.localPosition = PositionGetter.transform.localPosition;

        }
    }
}
