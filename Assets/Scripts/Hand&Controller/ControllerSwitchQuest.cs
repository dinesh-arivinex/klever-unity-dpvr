using UnityEngine;

public class ControllerSwitchQuest : MonoBehaviour
{
    public static ControllerSwitchQuest Instance { get; private set; }
    private bool isFirst;
    public GameObject gamificationObj;
    [SerializeField]
    public bool isQuest3;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    public void AssignGamification()
    {
        isFirst = false;
        foreach (Transform child in transform)
        {
            GameObject childObject = child.gameObject;

            // Check if GameObject is active (enabled)
            if (childObject.activeInHierarchy && !isFirst)
            {
                gamificationObj.transform.SetParent(child, false); // false = keep localPosition
                if (isQuest3)
                {
                    Vector3 pos = gamificationObj.transform.localPosition;
                    pos.x = 1.6f;
                    gamificationObj.transform.localPosition = pos;
                    Vector3 scale = gamificationObj.transform.localScale;
                    scale.x = 100f;
                    scale.y = 100f;
                    scale.z = 100f;
                    gamificationObj.transform.localScale = scale;
                }
                isFirst = true;
            }
        }
    }
}