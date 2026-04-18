using UnityEngine;

public class RotateOverTime : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float speed = 90f;

    private float angle = 0f;

    void Update()
    {
        angle += speed * Time.deltaTime;
        transform.rotation = Quaternion.AngleAxis(angle, axis);
    }
}
