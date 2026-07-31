using UnityEngine;
using Oculus.Interaction;

public class ColliderTriggerMatch : MonoBehaviour
{
    public GameObject Answer1Pos, Answer2Pos, Answer3Pos, Answer4Pos, Answer5Pos;
    public GameObject Answer1HandGrab, Answer2HandGrab, Answer3HandGrab, Answer4HandGrab, Answer5HandGrab;

    private Transform ans1Pos, ans2Pos, ans3Pos, ans4Pos, ans5Pos;
    private void Start()
    {
        ans1Pos = Answer1Pos.transform;
        ans2Pos = Answer2Pos.transform;
        ans3Pos = Answer3Pos.transform;
        ans4Pos = Answer4Pos.transform;
        ans5Pos = Answer5Pos.transform;
        Debug.Log("Answer4Pos" + ans4Pos.localPosition);
        Debug.Log("Answer5Pos" + ans5Pos.localPosition);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == Answer1Pos && gameObject != Answer1Pos)
        {
            GetComponent<BoxCollider>().enabled = false;
            GetComponent<Grabbable>().enabled = false;

            Answer1Pos.GetComponent<BoxCollider>().enabled = false;
            Answer1Pos.GetComponent<Grabbable>().enabled = false;

            Answer1HandGrab.SetActive(false);

            Vector3 tempPos = transform.position;
            transform.position = Answer1Pos.transform.position;
            Answer1Pos.transform.position = tempPos;
        }
        else if (other.gameObject == Answer2Pos && gameObject != Answer2Pos)
        {
            GetComponent<BoxCollider>().enabled = false;
            GetComponent<Grabbable>().enabled = false;

            Answer2Pos.GetComponent<BoxCollider>().enabled = false;
            Answer2Pos.GetComponent<Grabbable>().enabled = false;

            Answer2HandGrab.SetActive(false);

            Vector3 tempPos = transform.position;
            transform.position = Answer2Pos.transform.position;
            Answer2Pos.transform.position = tempPos;
        }
        else if (other.gameObject == Answer3Pos && gameObject != Answer3Pos)
        {
            GetComponent<BoxCollider>().enabled = false;
            GetComponent<Grabbable>().enabled = false;

            Answer3Pos.GetComponent<BoxCollider>().enabled = false;
            Answer3Pos.GetComponent<Grabbable>().enabled = false;

            Answer3HandGrab.SetActive(false);

            Vector3 tempPos = transform.position;
            transform.position = Answer3Pos.transform.position;
            Answer3Pos.transform.position = tempPos;
        }
        else if (other.gameObject == Answer4Pos && gameObject != Answer4Pos)
        {
            gameObject.GetComponent<BoxCollider>().enabled = false;
            gameObject.GetComponent<Grabbable>().enabled = false;

            if (gameObject == Answer1Pos)
            {
               Answer1HandGrab.SetActive(false);
               Answer1Pos.transform.position = ans4Pos.position;
               Answer4Pos.transform.position = ans1Pos.position;
            }
            if (gameObject == Answer2Pos)
            {
                Answer2HandGrab.SetActive(false);
            }
            if (gameObject == Answer3Pos)
            {
                Answer3HandGrab.SetActive(false);
            }
            if (gameObject == Answer4Pos)
            {
                Answer4HandGrab.SetActive(false);
            }
            if (gameObject == Answer5Pos)
            {
                Answer5HandGrab.SetActive(false);
                Answer5Pos.transform.localPosition = ans4Pos.localPosition;
                Answer4Pos.transform.localPosition = ans5Pos.localPosition;
                Debug.Log("Answer4Pos"+ans4Pos.localPosition);
                Debug.Log("Answer5Pos"+ans5Pos.localPosition);
            }
            //Vector3 tempPos = transform.position;
            //Debug.Log("tempPos: " + tempPos.x + ", " + tempPos.y + ", " + tempPos.z);
            //transform.position = Answer4Pos.transform.position;
            //Debug.Log("changedPos: " + transform.position);
            //Answer4Pos.transform.position = tempPos;
            //Debug.Log("swappedPos: " + Answer4Pos.transform.position);
        }
        else if (other.gameObject == Answer5Pos && gameObject != Answer5Pos)
        {
            GetComponent<BoxCollider>().enabled = false;
            GetComponent<Grabbable>().enabled = false;

            Answer5Pos.GetComponent<BoxCollider>().enabled = false;
            Answer5Pos.GetComponent<Grabbable>().enabled = false;

            Answer5HandGrab.SetActive(false);

            Vector3 tempPos = transform.position;
            transform.position = Answer5Pos.transform.position;
            Answer5Pos.transform.position = tempPos;
        }
    }
}