using UnityEngine;

public class GrabPose : MonoBehaviour
{
    [Range(0, 1)] public float fingers = 0.6f; // medio, anular, meñique y pulgar
    [Range(0, 1)] public float index = 0.6f;   // índice
}