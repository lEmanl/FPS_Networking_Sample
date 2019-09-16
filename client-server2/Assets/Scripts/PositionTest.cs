using UnityEngine;
using System.Collections;

public class PositionTest : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log($"Player position regular, x: {transform.position.x}, y: {transform.position.y}");
        Debug.Log($"Player position float, x: {(float)transform.position.x}, y: {(float)transform.position.y}");
    }
}
