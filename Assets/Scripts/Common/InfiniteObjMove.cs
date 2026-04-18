using UnityEngine;
using System.Collections.Generic;

public class InfiniteObjMove : MonoBehaviour
{
    public enum MoveDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public MoveDirection direction = MoveDirection.Up;
    public float speed = 1f;
    public float threshold = 10f;
    public float distance = 10f;

    private List<Transform> children = new List<Transform>();
    private Vector3 moveDir;

    void Start()
    {
        // 取得所有子物件
        foreach (Transform child in transform)
        {
            children.Add(child);
        }

        // 轉換方向
        moveDir = GetDirectionVector(direction);
    }

    void Update()
    {
        MoveChildren();
        CheckAndLoop();
    }

    void MoveChildren()
    {
        Vector3 offset = moveDir * speed * Time.deltaTime;

        foreach (var child in children)
        {
            child.position += offset;
        }
    }

    void CheckAndLoop()
    {
        foreach (var child in children)
        {
            if (IsOutOfBound(child.position))
            {
                Vector3 pos = child.position;

                if (direction == MoveDirection.Up)
                    pos.y -= distance * children.Count;
                else if (direction == MoveDirection.Down)
                    pos.y += distance * children.Count;
                else if (direction == MoveDirection.Right)
                    pos.x -= distance * children.Count;
                else if (direction == MoveDirection.Left)
                    pos.x += distance * children.Count;

                child.position = pos;
            }
        }
    }

    bool IsOutOfBound(Vector3 pos)
    {
        if (direction == MoveDirection.Up)
            return pos.y > threshold;

        if (direction == MoveDirection.Down)
            return pos.y < -threshold;

        if (direction == MoveDirection.Right)
            return pos.x > threshold;

        if (direction == MoveDirection.Left)
            return pos.x < -threshold;

        return false;
    }

    Vector3 GetDirectionVector(MoveDirection dir)
    {
        switch (dir)
        {
            case MoveDirection.Up: return Vector3.up;
            case MoveDirection.Down: return Vector3.down;
            case MoveDirection.Left: return Vector3.left;
            case MoveDirection.Right: return Vector3.right;
        }
        return Vector3.up;
    }
}
