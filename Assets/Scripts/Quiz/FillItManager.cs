using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Networking;

public class FillItManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI questionText;
    public TextMeshProUGUI questionPanelText;
    
    [Header("Answers")]
    public DragAndDropAnswer[] draggableAnswers; // Ensure these are set to Answer1, Answer2, Answer3, Answer4

    [Header("Events")]
    public UnityEvent StartEvent;
    public UnityEvent reloadSceneEvent;

    [Header("Managers")]
    private GamificationManager gamificationManager;
    private API apiManager;
    
    private List<AnswerData> currentAnswers = new List<AnswerData>();
    private int currentQuestionIndex = 0;
    private bool gameCompleted = false;

    private void OnEnable()
    {
        gamificationManager = FindAnyObjectByType<GamificationManager>();
        apiManager = FindAnyObjectByType<API>();
        
        StartEvent?.Invoke();
        gameCompleted = false;
    }

    private void DisplayQuestionImage(string url)
    {
        Transform parentTransform = questionText != null ? questionText.transform.parent : transform;
        if (parentTransform == null) return;
        Transform existing = parentTransform.Find("DynamicQuestionImage");
        RawImage rawImg = null;
        if (existing != null)
        {
            rawImg = existing.GetComponent<RawImage>();
        }
        else
        {
            GameObject go = new GameObject("DynamicQuestionImage");
            go.transform.SetParent(parentTransform, false);
            rawImg = go.AddComponent<RawImage>();
            
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1.2f, 0.8f); 
            rect.anchoredPosition = new Vector3(0f, 150f, 0f); 
        }

        if (rawImg != null)
        {
            if (string.IsNullOrEmpty(url))
            {
                rawImg.gameObject.SetActive(false);
            }
            else
            {
                rawImg.gameObject.SetActive(true);
                StartCoroutine(LoadImageCoroutine(url, texture => {
                    rawImg.texture = texture;
                }));
            }
        }
    }

    private IEnumerator LoadImageCoroutine(string url, System.Action<Texture2D> onSuccess)
    {
        if (string.IsNullOrEmpty(url)) yield break;
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                onSuccess?.Invoke(texture);
            }
            else
            {
                DebugUtil.LogError("Failed to load image from: " + url + " Error: " + webRequest.error);
            }
        }
    }

    public void DisplayQuestion(List<string> heading, List<AnswerData> answerOptions, string actualQuestion, string questionImageUrl)
    {
        currentAnswers = answerOptions;
        
        if (questionText != null) questionText.text = actualQuestion;
        if (questionPanelText != null && heading.Count > 1) questionPanelText.text = heading[1];
        
        DisplayQuestionImage(questionImageUrl);

        // Setup the draggable answers
        for (int i = 0; i < draggableAnswers.Length; i++)
        {
            if (i < answerOptions.Count)
            {
                draggableAnswers[i].gameObject.SetActive(true);
                
                // Set text
                TextMeshProUGUI answerText = draggableAnswers[i].GetComponentInChildren<TextMeshProUGUI>();
                if (answerText != null)
                {
                    answerText.text = answerOptions[i].text;
                }
                
                // Initialize the drag logic with correctness
                draggableAnswers[i].Setup(this, i, answerOptions[i].isCorrect);
            }
            else
            {
                // Hide unused answers
                if (draggableAnswers[i] != null)
                {
                    draggableAnswers[i].gameObject.SetActive(false);
                }
            }
        }
        
        gameCompleted = false;
    }

    public void OnAnswerSnapped(DragAndDropAnswer snappedAnswer)
    {
        if (gameCompleted) return;
        gameCompleted = true;

        Color greenWithAlpha = new Color(Color.green.r, Color.green.g, Color.green.b, 255f / 255f);
        Color redWithAlpha = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);

        // Change color based on correctness
        if (snappedAnswer.isCorrect)
        {
            snappedAnswer.SetColor(greenWithAlpha);
            gamificationManager.fillItScore++; // This needs to be added to GamificationManager
        }
        else
        {
            snappedAnswer.SetColor(redWithAlpha);
            
            // Optionally find and highlight the correct answer
            for (int i = 0; i < draggableAnswers.Length; i++)
            {
                if (draggableAnswers[i].gameObject.activeSelf && draggableAnswers[i].isCorrect)
                {
                    draggableAnswers[i].SetColor(greenWithAlpha);
                    break;
                }
            }
        }

        // Disable all other answers
        foreach (var answer in draggableAnswers)
        {
            if (answer != snappedAnswer)
            {
                answer.gameObject.SetActive(false); // Or just disable dragging
            }
        }

        // Wait a moment before moving to next question/game
        StartCoroutine(ProceedToNextGame());
    }

    private IEnumerator ProceedToNextGame()
    {
        yield return new WaitForSeconds(2f);
        
        gamificationManager.OnGameCompleted();
        reloadSceneEvent?.Invoke();
    }
}
