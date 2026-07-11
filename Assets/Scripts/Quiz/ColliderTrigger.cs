using System.Collections.Generic;
using Oculus.Interaction;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public class ColliderTrigger : MonoBehaviour
{
    public List<string> targetObjects = new List<string>();

    public GameObject AnswerPlacePos;
    public List<GameObject> GrabInteractable;
    private void OnTriggerEnter(Collider other)
    {
        foreach (string targetName in targetObjects)
        {
            if (other.gameObject.tag == targetName)
            {
                GameObject snapTarget = GameObject.Find(targetName);

                if (snapTarget != null)
                {
                    snapTarget.GetComponent<BoxCollider>().enabled = false;
                    GameObject grandParent = snapTarget.transform.parent.parent.gameObject;
                    grandParent.GetComponent<Grabbable>().enabled = false;
                    Transform handGrabInteractable = snapTarget.transform.parent.parent.Find("HandGrabInteractable");

                    if (handGrabInteractable != null)
                    {
                        handGrabInteractable.gameObject.SetActive(false);
                    }
                    grandParent.GetComponent<SnapInteractor>().enabled = false;
                    grandParent.transform.localPosition = AnswerPlacePos.transform.localPosition;
                    this.gameObject.GetComponent<BoxCollider>().enabled = false;
                    this.gameObject.GetComponent<Image>().enabled=false;

                    foreach(var items in GrabInteractable)
                    {
                        items.gameObject.SetActive(false);
                    }
                }

                break;
            }
        }
    }
}