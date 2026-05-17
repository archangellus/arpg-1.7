using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class MouseFollower : MonoBehaviour
{
    [Header("References")]
    public string followObjectName = "FollowObject";
    public string movingObjectTag = "MovingObject";
    public Camera raycastCamera;

    [Header("New Input System")]
    [Tooltip("Optional. Assign a Value/Vector2 action bound to <Pointer>/position or <Mouse>/position.")]
    public InputActionReference pointerPositionAction;

    [Header("Ground Detection")]
    public LayerMask groundLayer;

    [Header("Position Settings")]
    public float yOffset;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public bool fixedYPosition = false;
    public bool followCursor = true;
    public string FollowTagName;

    [Header("Active Zone")]
    public float activeZoneAngle = 45f;
    public float activeZoneRotation = 0f;
    public int segmentCount = 12;
    public Color activeZoneColor = Color.red;

    [Header("Visibility")]
    public bool showWhileMoving = false;

    private GameObject movingObject;
    private GameObject followObject;
    private NavMeshAgent movingAgent;

    private GameObject cachedTaggedFollowObject;
    private bool pointerActionEnabledByThisScript;
    private bool warnedMissingMovingTag;
    private bool warnedMissingFollowTag;

    private const float StoppedVelocityThreshold = 0.0001f;

    private void OnEnable()
    {
        EnablePointerActionIfNeeded();
    }

    private void OnDisable()
    {
        DisablePointerActionIfEnabledByThisScript();
    }

    private void Start()
    {
        StartCoroutine(FindMovingObject());
    }

    private void EnablePointerActionIfNeeded()
    {
        if (pointerPositionAction == null || pointerPositionAction.action == null)
            return;

        if (!pointerPositionAction.action.enabled)
        {
            pointerPositionAction.action.Enable();
            pointerActionEnabledByThisScript = true;
        }
    }

    private void DisablePointerActionIfEnabledByThisScript()
    {
        if (!pointerActionEnabledByThisScript)
            return;

        if (pointerPositionAction != null && pointerPositionAction.action != null)
            pointerPositionAction.action.Disable();

        pointerActionEnabledByThisScript = false;
    }

    private IEnumerator FindMovingObject()
    {
        while (movingObject == null)
        {
            movingObject = TryFindGameObjectWithTag(movingObjectTag, ref warnedMissingMovingTag);

            if (movingObject != null)
                movingObject.TryGetComponent(out movingAgent);

            yield return null;
        }

        StartCoroutine(FindFollowObject());
    }

    private IEnumerator FindFollowObject()
    {
        while (followObject == null && movingObject != null)
        {
            followObject = FindChildRecursive(movingObject.transform, followObjectName);
            yield return null;
        }
    }

    private GameObject FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child.gameObject;

            GameObject found = FindChildRecursive(child, childName);

            if (found != null)
                return found;
        }

        return null;
    }

    private void Update()
    {
        if (movingObject == null)
        {
            SetFollowObjectVisible(false);
            return;
        }

        if (movingAgent == null && !movingObject.TryGetComponent(out movingAgent))
        {
            SetFollowObjectVisible(false);
            return;
        }

        if (!TryGetTargetPoint(out Vector3 targetPoint))
        {
            SetFollowObjectVisible(false);
            return;
        }

        Vector3 directionToTarget = targetPoint - movingObject.transform.position;

        if (directionToTarget.sqrMagnitude <= Mathf.Epsilon)
        {
            SetFollowObjectVisible(false);
            return;
        }

        float distanceToTarget = directionToTarget.magnitude;
        Vector3 normalizedDirection = directionToTarget / distanceToTarget;

        Vector3 rotatedForward = Quaternion.Euler(0f, activeZoneRotation, 0f) * movingObject.transform.forward;

        bool isTargetInsideActiveZone = Vector3.Angle(rotatedForward, normalizedDirection) <= activeZoneAngle;
        bool isTargetTooClose = distanceToTarget < minDistance;
        bool canShowWhileMoving = showWhileMoving || movingAgent.velocity.sqrMagnitude <= StoppedVelocityThreshold;

        if (followObject != null)
        {
            if (canShowWhileMoving && isTargetInsideActiveZone && !isTargetTooClose)
            {
                followObject.transform.position = targetPoint;
                followObject.SetActive(true);
            }
            else
            {
                followObject.SetActive(false);
            }
        }
    }

    private bool TryGetTargetPoint(out Vector3 targetPoint)
    {
        targetPoint = Vector3.zero;

        if (followCursor)
        {
            if (!TryReadPointerPosition(out Vector2 screenPosition))
                return false;

            Camera cam = raycastCamera != null ? raycastCamera : Camera.main;

            if (cam == null)
                return false;

            Ray ray = cam.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
                return false;

            targetPoint = hit.point;
        }
        else
        {
            GameObject taggedObject = GetTaggedFollowObject();

            if (taggedObject == null)
                return false;

            targetPoint = taggedObject.transform.position;
        }

        if (fixedYPosition && movingObject != null)
            targetPoint.y = movingObject.transform.position.y + yOffset;

        return true;
    }

    private bool TryReadPointerPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (pointerPositionAction != null && pointerPositionAction.action != null && pointerPositionAction.action.enabled)
        {
            screenPosition = pointerPositionAction.action.ReadValue<Vector2>();
            return true;
        }

        if (Pointer.current != null)
        {
            screenPosition = Pointer.current.position.ReadValue();
            return true;
        }

        return false;
    }

    private GameObject GetTaggedFollowObject()
    {
        if (cachedTaggedFollowObject != null)
            return cachedTaggedFollowObject;

        cachedTaggedFollowObject = TryFindGameObjectWithTag(FollowTagName, ref warnedMissingFollowTag);
        return cachedTaggedFollowObject;
    }

    private GameObject TryFindGameObjectWithTag(string tagName, ref bool warnedMissingTag)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return null;

        try
        {
            return GameObject.FindGameObjectWithTag(tagName);
        }
        catch (UnityException)
        {
            if (!warnedMissingTag)
            {
                Debug.LogWarning($"{nameof(MouseFollower)}: Tag '{tagName}' is not defined in the project.", this);
                warnedMissingTag = true;
            }

            return null;
        }
    }

    private void SetFollowObjectVisible(bool visible)
    {
        if (followObject != null)
            followObject.SetActive(visible);
    }

    private void OnDrawGizmos()
    {
        if (movingObject != null)
        {
            Gizmos.color = activeZoneColor;

            Vector3 rotatedForward = Quaternion.Euler(0f, activeZoneRotation, 0f) * movingObject.transform.forward;
            Vector3 activeZoneDirection1 = Quaternion.Euler(0f, activeZoneAngle, 0f) * rotatedForward;
            Vector3 activeZoneDirection2 = Quaternion.Euler(0f, -activeZoneAngle, 0f) * rotatedForward;

            Gizmos.DrawRay(movingObject.transform.position, activeZoneDirection1 * maxDistance);
            Gizmos.DrawRay(movingObject.transform.position, activeZoneDirection2 * maxDistance);

            DrawCircle(movingObject.transform.position, minDistance, activeZoneColor, false);
            DrawCircle(movingObject.transform.position, maxDistance, activeZoneColor, true);
        }
    }

    private void DrawCircle(Vector3 position, float radius, Color color, bool drawSegments)
    {
        Gizmos.color = color;

        Vector3 prevPos = position;
        float segmentTheta = 2f * Mathf.PI / segmentCount;
        bool wasInsideActiveZone = false;

        for (int i = 0; i <= segmentCount; i++)
        {
            float theta = segmentTheta * i;
            float x = radius * Mathf.Cos(theta);
            float z = radius * Mathf.Sin(theta);

            Vector3 direction = new Vector3(x, 0f, z);
            direction = Quaternion.Euler(0f, activeZoneRotation, 0f) * direction;

            Vector3 newPos = position + direction;

            bool isInsideActiveZone =
                Vector3.Angle(
                    Quaternion.Euler(0f, activeZoneRotation, 0f) * movingObject.transform.forward,
                    direction
                ) <= activeZoneAngle;

            if (isInsideActiveZone)
            {
                if (wasInsideActiveZone)
                    Gizmos.DrawLine(prevPos, newPos);

                prevPos = newPos;

                if (drawSegments)
                {
                    Vector3 segmentStart = position + (newPos - position).normalized * minDistance;
                    Gizmos.DrawLine(segmentStart, newPos);
                }
            }

            wasInsideActiveZone = isInsideActiveZone;
        }
    }
}