using UnityEngine;
using Oculus.Interaction;

public class ColliderTriggerMatch : MonoBehaviour
{
    public GameObject Answer1Pos, Answer2Pos, Answer3Pos, Answer4Pos, Answer5Pos;

    private void Start()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == Answer1Pos && gameObject != Answer1Pos)
        {
            GetComponent<BoxCollider>().enabled = false;
            GetComponent<Grabbable>().enabled = false;

            Answer1Pos.GetComponent<BoxCollider>().enabled = false;
            Answer1Pos.GetComponent<Grabbable>().enabled = false;

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

            Vector3 tempPos = transform.position;
            transform.position = Answer3Pos.transform.position;
            Answer3Pos.transform.position = tempPos;
        }
        else if (other.gameObject == Answer4Pos && gameObject != Answer4Pos)
        {
            GetComponent<BoxCollider>().enabled = false;
            GetComponent<Grabbable>().enabled = false;

            Answer4Pos.GetComponent<BoxCollider>().enabled = false;
            Answer4Pos.GetComponent<Grabbable>().enabled = false;

            Vector3 tempPos = transform.position;
            transform.position = Answer4Pos.transform.position;
            Answer4Pos.transform.position = tempPos;
        }
        else if (other.gameObject == Answer5Pos && gameObject != Answer5Pos)
        {
            GetComponent<BoxCollider>().enabled = false;
            GetComponent<Grabbable>().enabled = false;

            Answer5Pos.GetComponent<BoxCollider>().enabled = false;
            Answer5Pos.GetComponent<Grabbable>().enabled = false;

            Vector3 tempPos = transform.position;
            transform.position = Answer5Pos.transform.position;
            Answer5Pos.transform.position = tempPos;
        }
    }
}