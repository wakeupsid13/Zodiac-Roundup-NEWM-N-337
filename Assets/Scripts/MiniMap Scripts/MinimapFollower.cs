using UnityEngine;

public class MinimapFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 50f, 0f); // height above player

    public void SetTarget(Transform t)
    {
        target = t;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Simple follow: sit above the player
        transform.position = target.position + offset;
    }
}
