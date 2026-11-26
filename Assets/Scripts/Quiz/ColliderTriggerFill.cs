using System.Collections;
using System.Collections.Generic;
using LibTessDotNet;


using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
#if KLEVER_DPVR
using Klever.DPVR;
#endif

public class ColliderTriggerFill : MonoBehaviour
{
    public GameObject Answer1,Answer2, Answer3, Answer4,Answer5;
    public GameObject PositionGetter;
    public TextMeshProUGUI textfromRes;
    private Image targetImage;
    private Color correctColor = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
    private Color wrongColor = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);

    public QuizManagers fillItQuizManagers;
    private GamificationManager gamificationManager;

    private void Start()
    {
        gamificationManager = FindAnyObjectByType<GamificationManager>();
    }
    private void OnTriggerEnter(Collider other)
    {
#if KLEVER_DPN
        // The controller explicitly commits a drop on release.
        return;
#endif
        if (LessonPause.IsPaused) return;

        if (other.gameObject == Answer1)
        {
            GameObject grandParent = Answer1.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer1.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer1.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            targetImage = Answer1.GetComponent<Image>();

            this.GetComponent<BoxCollider>().enabled = false;
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer1.GetComponent<BoxCollider>().enabled = false;
            // grandParent.GetComponent<Grabbable>().enabled = false;
#if KLEVER_DPVR
            DpvrInteractionUtility.SetGrabEnabled(grandParent, false);
#endif
            // grandParent.GetComponent<SnapInteractor>().enabled = false; 
            // grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
            // grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
            // grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            {

                Debug.Log("Correct");
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                //this.GetComponent<BoxCollider>().enabled = false;
                //this.gameObject.GetComponent<Image>().enabled = false;
                //Answer1.GetComponent<BoxCollider>().enabled = false;
                //grandParent.GetComponent<Grabbable>().enabled = false;
                //grandParent.GetComponent<SnapInteractor>().enabled = false;
                //grandParent.transform.GetChild(1).gameObject.SetActive(false);
                //StartCoroutine(CallIfWrong());
                //this.GetComponent<BoxCollider>().enabled = true;
                //Answer1.GetComponent<BoxCollider>().enabled = true;
                //grandParent.GetComponent<Grabbable>().enabled = true;
                //grandParent.GetComponent<SnapInteractor>().enabled = true;
                //grandParent.transform.GetChild(1).gameObject.SetActive(true);
                //StartCoroutine(CallIfWrong());
                //this.gameObject.GetComponent<Image>().enabled = false;

                Debug.Log("Wrong");
                ApplyColor(wrongColor, targetImage);
            }
            Answer1.GetComponent<BoxCollider>().enabled = false;
            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
               fillItQuizManagers.nextButton.enabled = true;
               fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (other.gameObject == Answer2)
        {
            GameObject grandParent = Answer2.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer2.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer2.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

            targetImage = Answer2.GetComponent<Image>();

            this.GetComponent<BoxCollider>().enabled = false;
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer2.GetComponent<BoxCollider>().enabled = false;
// grandParent.GetComponent<Grabbable>().enabled = false;
#if KLEVER_DPVR
            DpvrInteractionUtility.SetGrabEnabled(grandParent, false);
#endif
// grandParent.GetComponent<SnapInteractor>().enabled = false;grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
// grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
// grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            {

                Debug.Log("Correct");
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                //this.GetComponent<BoxCollider>().enabled = false;
                //Answer2.GetComponent<BoxCollider>().enabled = false;
                //grandParent.GetComponent<Grabbable>().enabled = false;
                //grandParent.GetComponent<SnapInteractor>().enabled = false;
                //grandParent.transform.GetChild(1).gameObject.SetActive(false);
                //StartCoroutine(CallIfWrong());
                //this.GetComponent<BoxCollider>().enabled = true;
                //Answer2.GetComponent<BoxCollider>().enabled = true;
                //grandParent.GetComponent<Grabbable>().enabled = true;
                //grandParent.GetComponent<SnapInteractor>().enabled = true;
                //grandParent.transform.GetChild(1).gameObject.SetActive(true);
                //StartCoroutine(CallIfWrong());
                //this.gameObject.GetComponent<Image>().enabled = false;
                Debug.Log("Wrong");
                ApplyColor(wrongColor, targetImage);
            }

            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            Answer2.GetComponent<BoxCollider>().enabled = false;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
                fillItQuizManagers.nextButton.enabled = true;
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (other.gameObject == Answer3)
        {
            GameObject grandParent = Answer3.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer3.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer3.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            targetImage = Answer3.GetComponent<Image>();

            this.GetComponent<BoxCollider>().enabled = false;
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer3.GetComponent<BoxCollider>().enabled = false;
// grandParent.GetComponent<Grabbable>().enabled = false;
#if KLEVER_DPVR
            DpvrInteractionUtility.SetGrabEnabled(grandParent, false);
#endif
// grandParent.GetComponent<SnapInteractor>().enabled = false;grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
// grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
// grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            { 

                Debug.Log("Correct");
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                //this.GetComponent<BoxCollider>().enabled = false;
                //this.gameObject.GetComponent<Image>().enabled = false;
                //Answer3.GetComponent<BoxCollider>().enabled = false;
                //grandParent.GetComponent<Grabbable>().enabled = false;
                //grandParent.GetComponent<SnapInteractor>().enabled = false;
                //grandParent.transform.GetChild(1).gameObject.SetActive(false);
                //StartCoroutine(CallIfWrong());
                //this.GetComponent<BoxCollider>().enabled = true;
                //Answer3.GetComponent<BoxCollider>().enabled = true;
                //grandParent.GetComponent<Grabbable>().enabled = true;
                //grandParent.GetComponent<SnapInteractor>().enabled = true;
                //grandParent.transform.GetChild(1).gameObject.SetActive(true);
                //StartCoroutine(CallIfWrong());
                //this.gameObject.GetComponent<Image>().enabled = false;
                Debug.Log("Wrong");
                ApplyColor(wrongColor, targetImage);
            }

            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            Answer3.GetComponent<BoxCollider>().enabled = false;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
                fillItQuizManagers.nextButton.enabled = true;
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (other.gameObject == Answer4)
        {
            GameObject grandParent = Answer4.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer4.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer4.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

            this.GetComponent<BoxCollider>().enabled = false;
            targetImage = Answer4.GetComponent<Image>();
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer4.GetComponent<BoxCollider>().enabled = false;
// grandParent.GetComponent<Grabbable>().enabled = false;
#if KLEVER_DPVR
            DpvrInteractionUtility.SetGrabEnabled(grandParent, false);
#endif
// grandParent.GetComponent<SnapInteractor>().enabled = false;grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
// grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
// grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            {

                Debug.Log("Correct");
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                Debug.Log("Wrong");
                ApplyColor(wrongColor, targetImage);
            }

            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            Answer4.GetComponent<BoxCollider>().enabled = false;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
                fillItQuizManagers.nextButton.enabled = true;
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (other.gameObject == Answer5)
        {
            GameObject grandParent = Answer5.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer5.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer5.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            this.GetComponent<BoxCollider>().enabled = false;
            targetImage = Answer5.GetComponent<Image>();
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer5.GetComponent<BoxCollider>().enabled = false;
// grandParent.GetComponent<Grabbable>().enabled = false;
#if KLEVER_DPVR
            DpvrInteractionUtility.SetGrabEnabled(grandParent, false);
#endif
// grandParent.GetComponent<SnapInteractor>().enabled = false;grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
// grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
// grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            {

                Debug.Log("Correct");
                ApplyColor(correctColor,targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                Debug.Log("Wrong");
                ApplyColor(wrongColor,targetImage);
            }

            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            Answer5.GetComponent<BoxCollider>().enabled = false;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
                fillItQuizManagers.nextButton.enabled = true;
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }
    }

#if KLEVER_DPN
    public bool TryDropDpvr(BoxCollider answer)
    {
        var slot = GetComponent<BoxCollider>();
        if (LessonPause.IsPaused || !isActiveAndEnabled || slot == null || !slot.enabled ||
            answer == null || !answer.enabled || !answer.gameObject.activeInHierarchy ||
            PositionGetter == null || textfromRes == null || fillItQuizManagers == null) return false;
        var card = answer.gameObject;
        if (card != Answer1 && card != Answer2 && card != Answer3 && card != Answer4 && card != Answer5) return false;
        var root = card.transform.parent?.parent;
        var label = card.GetComponentInChildren<TextMeshProUGUI>(true);
        if (root == null || label == null || string.IsNullOrWhiteSpace(label.text)) return false;
        if (gamificationManager == null) gamificationManager = FindObjectOfType<GamificationManager>(true);
        if (gamificationManager == null) return false;

        slot.enabled = false;
        answer.enabled = false;
        var slotImage = GetComponent<Image>();
        if (slotImage != null) slotImage.enabled = false;
        DpvrInteractionUtility.SetGrabEnabled(root.gameObject, false);
        root.position = PositionGetter.transform.position;
        bool correct = label.text == textfromRes.text;
        targetImage = card.GetComponent<Image>();
        ApplyColor(correct ? correctColor : wrongColor, targetImage);
        if (correct) gamificationManager.mcqScore++;
        fillItQuizManagers.RecordFillAnswer(textfromRes, label.text);
        if (++fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
        {
            var next = fillItQuizManagers.nextButton;
            next.interactable = true;
            next.enabled = true;
            var nextCollider = next.GetComponent<BoxCollider>();
            if (nextCollider != null) nextCollider.enabled = true;
        }
        Debug.Log("[DPVR-FILL] Placed card; count=" + fillItQuizManagers.answerPlacedCount);
        return true;
    }
#endif

    private void ApplyColor(Color color, Image Image)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }
    }

    IEnumerator CallIfWrong()
    {
        yield return new WaitForSeconds(2f);
    }
}

