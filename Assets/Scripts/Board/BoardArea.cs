using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BoardArea : MonoBehaviour
{
    private BoxCollider2D _collider;

    public Bounds Bounds => Collider.bounds;

    public Vector2 Size => Collider.bounds.size;

    private BoxCollider2D Collider
    {
        get
        {
            if (_collider == null)
                _collider = GetComponent<BoxCollider2D>();

            return _collider;
        }
    }
}