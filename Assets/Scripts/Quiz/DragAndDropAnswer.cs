using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// If you are using Meta XR Interaction SDK, you can uncomment the line below:
// using Oculus.Interaction;

public class DragAndDropAnswer : MonoBehaviour
{
    [Header("Configuration")]
    public FillItManager fillItManager;
    public int answerIndex;
    public bool isCorrect;
    
    private Vector3 originalPosition;
    private float fixedZ;
    private bool isGrabbed = false;
    private bool hasSnapped = false;
    
    // Variables to track if we are hovering over the drop zone
    private bool isInsideDropZone = false;
    private Transform currentDropZone;

    private void Start()
    {
        originalPosition = transform.position;
        fixedZ = transform.position.z;
    }

    public void Setup(FillItManager manager, int index, bool correct)
    {
        fillItManager = manager;
        answerIndex = index;
        isCorrect = correct;
        hasSnapped = false;
        isGrabbed = false;
        
        // Reset position on setup
        if (originalPosition != Vector3.zero)
        {
            transform.position = originalPosition;
            fixedZ = transform.position.z;
        }
        
        // Reset color if needed
        SetColor(Color.white); // default color, adjust as needed

        // If you are using Meta's Grabbable, you might want to re-enable it here
        // var grabbable = GetComponent<Grabbable>();
        // if (grabbable != null) grabbable.enabled = true;
    }

    // --- META INTERACTION SDK METHODS ---
    
    // Assign this to InteractableUnityEventWrapper -> WhenSelect (Grab begins)
    public void OnGrabBegin()
    {
        if (hasSnapped) return;
        
        if (originalPosition == Vector3.zero) 
        {
            originalPosition = transform.position;
            fixedZ = transform.position.z;
        }
        
        isGrabbed = true;
    }

    // Assign this to InteractableUnityEventWrapper -> WhenUnselect (Grab ends)
    public void OnGrabEnd()
    {
        if (hasSnapped) return;
        isGrabbed = false;
        
        // When dropped, check if we are inside the drop zone
        if (isInsideDropZone && currentDropZone != null)
        {
            SnapTo(currentDropZone.position);
        }
        else
        {
            // If dropped outside, return to original position
            StartCoroutine(ReturnToOriginalPosition());
        }
    }

    private void LateUpdate()
    {
        // Enforce the Z-axis constraint while the Meta Interactable is moving the object
        if (isGrabbed && !hasSnapped)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, fixedZ);
        }
    }

    private IEnumerator ReturnToOriginalPosition()
    {
        float t = 0;
        Vector3 startPos = transform.position;
        while (t < 1f)
        {
            t += Time.deltaTime * 5f; // speed
            transform.position = Vector3.Lerp(startPos, originalPosition, t);
            yield return null;
        }
        transform.position = originalPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasSnapped) return; 

        if (other.CompareTag("AnswerPlace") || other.name.Contains("AnswerPlace"))
        {
            isInsideDropZone = true;
            currentDropZone = other.transform;
            
            // Note: If you want it to snap *instantly* while the user is still holding it, 
            // you can uncomment the line below. Otherwise, it will snap when they let go (OnGrabEnd).
            // SnapTo(other.transform.position);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("AnswerPlace") || other.name.Contains("AnswerPlace"))
        {
            isInsideDropZone = false;
            if (currentDropZone == other.transform) currentDropZone = null;
        }
    }

    private void SnapTo(Vector3 targetPosition)
    {
        hasSnapped = true;
        isGrabbed = false;
        isInsideDropZone = false;
        
        // Force the object to the target's X,Y while keeping the fixed Z axis
        transform.position = new Vector3(targetPosition.x, targetPosition.y, fixedZ);
        
        // Try to disable Grabbable so the user can't pick it back up after snapping
        var colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
        
        // Notify Manager
        if (fillItManager != null)
        {
            fillItManager.OnAnswerSnapped(this);
        }
    }

    public void SetColor(Color color)
    {
        // Try setting Graphic color (for Canvas UI)
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = color;
            return;
        }
        
        // Try setting MeshRenderer color (for 3D Objects)
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = color;
        }
        
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Setup Meta Components")]
    private void SetupMetaComponents()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) 
        { 
            rb = gameObject.AddComponent<Rigidbody>(); 
            rb.isKinematic = true; 
        }

        AddComponentByName("Oculus.Interaction.Grabbable");
        AddComponentByName("Oculus.Interaction.Surfaces.ColliderSurface");
        AddComponentByName("Oculus.Interaction.RayInteractable");
        AddComponentByName("Oculus.Interaction.InteractableUnityEventWrapper");
        
        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("Added missing Meta XR Interaction components to " + gameObject.name + ". Please manually link the Surface and Unity Events in the Inspector.");
    }

    private void AddComponentByName(string typeName)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(typeName);
            if (type != null)
            {
                if (GetComponent(type) == null)
                {
                    gameObject.AddComponent(type);
                }
                return;
            }
        }
        Debug.LogWarning("Could not find type " + typeName + " in any assembly. Make sure Oculus Interaction SDK is imported.");
    }
#endif
}
